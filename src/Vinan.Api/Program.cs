using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Vinan.Api.Configuration;
using Vinan.Api.Data;
using Vinan.Api.Models;
using Vinan.Api.Services;

var builder = WebApplication.CreateBuilder(args);

var configuredConnection = builder.Configuration.GetConnectionString("Vinan");
var dataDirectory = Path.Combine(builder.Environment.ContentRootPath, ".data");
Directory.CreateDirectory(dataDirectory);
var defaultDatabasePath = Path.Combine(dataDirectory, "vinan.db");
var connectionString = string.IsNullOrWhiteSpace(configuredConnection)
    ? $"Data Source={defaultDatabasePath}"
    : configuredConnection;

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<VinanDbContext>(options => options.UseSqlite(connectionString));
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
    await database.Database.EnsureCreatedAsync();
    if (string.IsNullOrWhiteSpace(configuredConnection) && !OperatingSystem.IsWindows())
    {
        File.SetUnixFileMode(dataDirectory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
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

app.MapGet("/api/health", (ModelRouter model) => Results.Ok(new HealthResponse(
    "VINAN API online",
    DateTimeOffset.UtcNow,
    "SQLite",
    model.ActiveProvider)));

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

app.MapFallbackToFile("index.html");

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
