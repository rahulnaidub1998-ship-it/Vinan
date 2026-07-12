using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Vinan.Api.Configuration;
using Vinan.Api.Data;
using Vinan.Api.Models;
using Vinan.Api.Security;
using Vinan.Api.Services;

var builder = WebApplication.CreateBuilder(args);

var configuredConnection = builder.Configuration.GetConnectionString("Vinan");
var configuredDataDirectory = builder.Configuration["Storage:DataDirectory"];
var dataDirectory = string.IsNullOrWhiteSpace(configuredDataDirectory)
    ? Path.Combine(builder.Environment.ContentRootPath, ".data")
    : Path.GetFullPath(configuredDataDirectory, builder.Environment.ContentRootPath);
Directory.CreateDirectory(dataDirectory);
var keyDirectory = Path.Combine(dataDirectory, "keys");
Directory.CreateDirectory(keyDirectory);
if (!OperatingSystem.IsWindows())
{
    File.SetUnixFileMode(dataDirectory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    File.SetUnixFileMode(keyDirectory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
}
var defaultDatabasePath = Path.Combine(dataDirectory, "vinan.db");
var connectionString = string.IsNullOrWhiteSpace(configuredConnection)
    ? $"Data Source={defaultDatabasePath}"
    : configuredConnection;

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDataProtection()
    .SetApplicationName("VINAN")
    .PersistKeysToFileSystem(new DirectoryInfo(keyDirectory))
    .SetDefaultKeyLifetime(TimeSpan.FromDays(90));
builder.Services.AddSingleton<PersonalDataProtector>();
builder.Services.AddDbContext<VinanDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddScoped<IPasswordHasher<OwnerProfile>, PasswordHasher<OwnerProfile>>();
builder.Services.AddScoped<OwnerAuthService>();
builder.Services.AddScoped<PersonalDataUpgradeService>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "vinan.owner";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.Events.OnValidatePrincipal = async context =>
        {
            var auth = context.HttpContext.RequestServices.GetRequiredService<OwnerAuthService>();
            if (!await auth.ValidateDeviceAsync(context.Principal!, context.HttpContext.RequestAborted))
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
builder.Services.Configure<ModelOptions>(builder.Configuration.GetSection(ModelOptions.SectionName));
builder.Services.AddSingleton<LocalAssistantModel>();
builder.Services.AddHttpClient<OpenAiAssistantModel>(client =>
{
    client.BaseAddress = new Uri("https://api.openai.com/");
    client.Timeout = TimeSpan.FromSeconds(45);
});
builder.Services.AddScoped<ModelRouter>();
builder.Services.AddScoped<IAssistantModel>(services => services.GetRequiredService<ModelRouter>());
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<ConversationService>();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var database = scope.ServiceProvider.GetRequiredService<VinanDbContext>();
    await MigrationBootstrapper.PrepareAndMigrateAsync(database);
    await scope.ServiceProvider.GetRequiredService<PersonalDataUpgradeService>().UpgradeLegacyRowsAsync();
    if (string.IsNullOrWhiteSpace(configuredConnection) && !OperatingSystem.IsWindows())
    {
        File.SetUnixFileMode(defaultDatabasePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.Use(async (context, next) =>
{
    var unsafeApiRequest = context.Request.Path.StartsWithSegments("/api")
        && !HttpMethods.IsGet(context.Request.Method)
        && !HttpMethods.IsHead(context.Request.Method)
        && !HttpMethods.IsOptions(context.Request.Method);
    if (unsafeApiRequest && context.Request.Headers["X-Vinan-Request"] != "1")
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new { error = "VINAN request verification failed." });
        return;
    }

    await next();
});
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/health", (ModelRouter model) => Results.Ok(new HealthResponse(
    "VINAN API online",
    DateTimeOffset.UtcNow,
    "SQLite",
    model.ActiveProvider))).AllowAnonymous();

var authEndpoints = app.MapGroup("/api/auth");
authEndpoints.MapGet("/status", async (HttpContext context, OwnerAuthService auth, CancellationToken cancellationToken) =>
    Results.Ok(await auth.GetStatusAsync(context.User, cancellationToken))).AllowAnonymous();
authEndpoints.MapPost("/setup", async (SetupOwnerRequest request, HttpContext context, OwnerAuthService auth, CancellationToken cancellationToken) =>
{
    var result = await auth.SetupAsync(request, cancellationToken);
    if (!result.Succeeded)
    {
        return Results.BadRequest(new { error = result.Error });
    }

    await auth.SignInAsync(context, result.Device!, true);
    return Results.Ok(result.Device);
}).AllowAnonymous();
authEndpoints.MapPost("/login", async (LoginRequest request, HttpContext context, OwnerAuthService auth, CancellationToken cancellationToken) =>
{
    var result = await auth.LoginAsync(request, cancellationToken);
    if (!result.Succeeded)
    {
        return Results.Json(new { error = result.Error }, statusCode: StatusCodes.Status401Unauthorized);
    }

    await auth.SignInAsync(context, result.Device!, request.RememberMe);
    return Results.Ok(result.Device);
}).AllowAnonymous();
authEndpoints.MapPost("/logout", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.NoContent();
});

app.MapGet("/api/devices", async (HttpContext context, OwnerAuthService auth, CancellationToken cancellationToken) =>
{
    var currentDevice = OwnerAuthService.CurrentDeviceId(context.User);
    return currentDevice is null
        ? Results.Unauthorized()
        : Results.Ok(await auth.GetDevicesAsync(currentDevice.Value, cancellationToken));
});
app.MapPost("/api/devices/{id:guid}/revoke", async (Guid id, HttpContext context, OwnerAuthService auth, CancellationToken cancellationToken) =>
{
    if (!await auth.RevokeDeviceAsync(id, cancellationToken))
    {
        return Results.NotFound();
    }

    if (OwnerAuthService.CurrentDeviceId(context.User) == id)
    {
        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }

    return Results.NoContent();
});

var memory = app.MapGroup("/api/memory");
memory.MapGet("/", async (VinanDbContext database, CancellationToken cancellationToken) =>
    await database.Memories.AsNoTracking()
        .OrderByDescending(item => item.CreatedAt)
        .ToListAsync(cancellationToken));
memory.MapPost("/", async (CreateMemoryRequest request, VinanDbContext database, AuditService audit, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Text))
    {
        return Results.BadRequest(new { error = "Memory text is required." });
    }

    var item = new MemoryItem
    {
        Id = Guid.NewGuid(),
        Text = request.Text.Trim(),
        Category = string.IsNullOrWhiteSpace(request.Category) ? "Preference" : request.Category.Trim(),
        CreatedAt = DateTimeOffset.UtcNow,
    };
    database.Memories.Add(item);
    audit.Add("Approved memory stored", RiskLevel.Level2);
    await database.SaveChangesAsync(cancellationToken);
    return Results.Created($"/api/memory/{item.Id}", item);
});
memory.MapDelete("/{id:guid}", async (Guid id, VinanDbContext database, AuditService audit, CancellationToken cancellationToken) =>
{
    var item = await database.Memories.FindAsync(new object[] { id }, cancellationToken);
    if (item is null)
    {
        return Results.NotFound();
    }

    database.Memories.Remove(item);
    audit.Add("Memory deleted", RiskLevel.Level2);
    await database.SaveChangesAsync(cancellationToken);
    return Results.NoContent();
});

var reminders = app.MapGroup("/api/reminders");
reminders.MapGet("/", async (VinanDbContext database, CancellationToken cancellationToken) =>
    await database.Reminders.AsNoTracking()
        .Where(item => !item.IsComplete)
        .OrderByDescending(item => item.CreatedAt)
        .ToListAsync(cancellationToken));
reminders.MapPost("/", async (CreateReminderRequest request, VinanDbContext database, AuditService audit, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Title))
    {
        return Results.BadRequest(new { error = "Reminder title is required." });
    }

    var item = new ReminderItem
    {
        Id = Guid.NewGuid(),
        Title = request.Title.Trim(),
        When = string.IsNullOrWhiteSpace(request.When) ? "Scheduled" : request.When.Trim(),
        CreatedAt = DateTimeOffset.UtcNow,
    };
    database.Reminders.Add(item);
    audit.Add("Reminder created", RiskLevel.Level2);
    await database.SaveChangesAsync(cancellationToken);
    return Results.Created($"/api/reminders/{item.Id}", item);
});
reminders.MapPost("/{id:guid}/complete", async (Guid id, VinanDbContext database, AuditService audit, CancellationToken cancellationToken) =>
{
    var reminder = await database.Reminders.FindAsync(new object[] { id }, cancellationToken);
    if (reminder is null)
    {
        return Results.NotFound();
    }

    reminder.IsComplete = true;
    audit.Add("Reminder completed", RiskLevel.Level2);
    await database.SaveChangesAsync(cancellationToken);
    return Results.Ok(reminder);
});

app.MapGet("/api/permissions", () => VinanPermissions.Default);
app.MapGet("/api/audit", async (VinanDbContext database, CancellationToken cancellationToken) =>
    await database.AuditEvents.AsNoTracking()
        .OrderByDescending(item => item.CreatedAt)
        .Take(100)
        .ToListAsync(cancellationToken));

var actions = app.MapGroup("/api/actions");
actions.MapGet("/", async (VinanDbContext database, CancellationToken cancellationToken) =>
    await database.PendingActions.AsNoTracking()
        .Where(item => item.Status == ActionStatus.Pending)
        .OrderByDescending(item => item.CreatedAt)
        .ToListAsync(cancellationToken));
actions.MapPost("/{id:guid}/approve", async (Guid id, VinanDbContext database, AuditService audit, CancellationToken cancellationToken) =>
    await ResolveActionAsync(id, ActionStatus.Approved, database, audit, cancellationToken));
actions.MapPost("/{id:guid}/deny", async (Guid id, VinanDbContext database, AuditService audit, CancellationToken cancellationToken) =>
    await ResolveActionAsync(id, ActionStatus.Denied, database, audit, cancellationToken));

app.MapPost("/api/conversation/message", async (ConversationRequest request, ConversationService conversation, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Message))
    {
        return Results.BadRequest(new { error = "A message is required." });
    }

    return Results.Ok(await conversation.HandleAsync(request, cancellationToken));
});
app.MapGet("/api/conversations", async (VinanDbContext database, CancellationToken cancellationToken) =>
    await database.Conversations.AsNoTracking()
        .OrderByDescending(item => item.UpdatedAt)
        .Select(item => new ConversationSummary(item.Id, item.Title, item.CreatedAt, item.UpdatedAt))
        .Take(50)
        .ToListAsync(cancellationToken));
app.MapGet("/api/conversations/{id:guid}/messages", async (Guid id, VinanDbContext database, CancellationToken cancellationToken) =>
    await database.ConversationMessages.AsNoTracking()
        .Where(item => item.ConversationId == id)
        .OrderBy(item => item.CreatedAt)
        .ToListAsync(cancellationToken));

app.MapFallbackToFile("index.html").AllowAnonymous();

app.Run();

static async Task<IResult> ResolveActionAsync(
    Guid id,
    ActionStatus status,
    VinanDbContext database,
    AuditService audit,
    CancellationToken cancellationToken)
{
    var action = await database.PendingActions.FindAsync(new object[] { id }, cancellationToken);
    if (action is null || action.Status != ActionStatus.Pending)
    {
        return Results.NotFound();
    }

    action.Status = status;
    action.ResolvedAt = DateTimeOffset.UtcNow;
    audit.Add($"Action {status.ToString().ToLowerInvariant()}: {action.Summary}", action.RiskLevel);
    await database.SaveChangesAsync(cancellationToken);
    return Results.Ok(action);
}

public partial class Program
{
}
