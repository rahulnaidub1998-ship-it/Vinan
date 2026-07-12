using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<VinanStateStore>();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddCors(options =>
{
    options.AddPolicy("LocalPrototype", policy =>
        policy.AllowAnyHeader()
            .AllowAnyMethod()
            .AllowAnyOrigin());
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("LocalPrototype");
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/health", () => Results.Ok(new HealthResponse("VINAN API online", DateTimeOffset.UtcNow)));

var memory = app.MapGroup("/api/memory");
memory.MapGet("/", (VinanStateStore store) => store.Memories.OrderByDescending(item => item.CreatedAt));
memory.MapPost("/", (CreateMemoryRequest request, VinanStateStore store) =>
{
    if (string.IsNullOrWhiteSpace(request.Text))
    {
        return Results.BadRequest(new { error = "Memory text is required." });
    }

    var category = string.IsNullOrWhiteSpace(request.Category) ? "Preference" : request.Category.Trim();
    var item = new MemoryItem(Guid.NewGuid(), request.Text.Trim(), category, DateTimeOffset.UtcNow);
    store.Memories.Add(item);
    store.AddAudit("Approved memory stored", RiskLevel.Level2);
    return Results.Created($"/api/memory/{item.Id}", item);
});
memory.MapDelete("/{id:guid}", (Guid id, VinanStateStore store) =>
{
    var removed = store.Memories.RemoveAll(item => item.Id == id) > 0;
    if (removed)
    {
        store.AddAudit("Memory deleted", RiskLevel.Level2);
    }

    return removed ? Results.NoContent() : Results.NotFound();
});

var reminders = app.MapGroup("/api/reminders");
reminders.MapGet("/", (VinanStateStore store) => store.Reminders
    .Where(item => !item.IsComplete)
    .OrderByDescending(item => item.CreatedAt));
reminders.MapPost("/", (CreateReminderRequest request, VinanStateStore store) =>
{
    if (string.IsNullOrWhiteSpace(request.Title))
    {
        return Results.BadRequest(new { error = "Reminder title is required." });
    }

    var item = new ReminderItem(Guid.NewGuid(), request.Title.Trim(), request.When ?? "Scheduled", false, DateTimeOffset.UtcNow);
    store.Reminders.Add(item);
    store.AddAudit("Reminder created", RiskLevel.Level2);
    return Results.Created($"/api/reminders/{item.Id}", item);
});
reminders.MapPost("/{id:guid}/complete", (Guid id, VinanStateStore store) =>
{
    var reminder = store.Reminders.FirstOrDefault(item => item.Id == id);
    if (reminder is null)
    {
        return Results.NotFound();
    }

    store.Reminders.Remove(reminder);
    store.Reminders.Add(reminder with { IsComplete = true });
    store.AddAudit("Reminder completed", RiskLevel.Level2);
    return Results.Ok(reminder with { IsComplete = true });
});

app.MapGet("/api/permissions", () => VinanPermissions.Default);
app.MapGet("/api/audit", (VinanStateStore store) => store.Audit.OrderByDescending(item => item.CreatedAt));

var actions = app.MapGroup("/api/actions");
actions.MapGet("/", (VinanStateStore store) => store.PendingActions.OrderByDescending(item => item.CreatedAt));
actions.MapPost("/{id:guid}/approve", (Guid id, VinanStateStore store) =>
{
    var action = store.PendingActions.FirstOrDefault(item => item.Id == id);
    if (action is null)
    {
        return Results.NotFound();
    }

    store.PendingActions.Remove(action);
    store.AddAudit($"Action approved: {action.Summary}", action.RiskLevel);
    return Results.Ok(action with { Status = "Approved" });
});
actions.MapPost("/{id:guid}/deny", (Guid id, VinanStateStore store) =>
{
    var action = store.PendingActions.FirstOrDefault(item => item.Id == id);
    if (action is null)
    {
        return Results.NotFound();
    }

    store.PendingActions.Remove(action);
    store.AddAudit($"Action denied: {action.Summary}", action.RiskLevel);
    return Results.Ok(action with { Status = "Denied" });
});

app.MapPost("/api/conversation/message", (ConversationRequest request, VinanStateStore store) =>
{
    if (string.IsNullOrWhiteSpace(request.Message))
    {
        return Results.BadRequest(new { error = "A message is required." });
    }

    var result = VinanAssistant.Handle(request.Message, store);
    return Results.Ok(result);
});

app.MapFallbackToFile("index.html");

app.Run();

public sealed class VinanStateStore
{
    public List<MemoryItem> Memories { get; } = new();
    public List<ReminderItem> Reminders { get; } = new();
    public List<PendingAction> PendingActions { get; } = new();
    public List<AuditEvent> Audit { get; } = new();

    public void AddAudit(string action, RiskLevel riskLevel)
    {
        Audit.Add(new AuditEvent(Guid.NewGuid(), action, riskLevel.ToString(), DateTimeOffset.UtcNow));
    }
}

public static class VinanAssistant
{
    public static ConversationResponse Handle(string rawMessage, VinanStateStore store)
    {
        var message = rawMessage.Trim();
        var lower = message.ToLowerInvariant();

        if (lower.Contains("remember"))
        {
            var text = StripMemory(message);
            var memory = new MemoryItem(Guid.NewGuid(), text, "Preference", DateTimeOffset.UtcNow);
            store.Memories.Add(memory);
            store.AddAudit("Approved memory stored", RiskLevel.Level2);
            return new ConversationResponse($"I saved this as approved memory: {text}", null, memory, null);
        }

        if (lower.Contains("reminder"))
        {
            var title = StripReminder(message);
            if (IsDateOnlyReminder(title))
            {
                title = "Reminder";
            }

            var reminder = new ReminderItem(Guid.NewGuid(), title, lower.Contains("tomorrow") ? "Tomorrow" : "Scheduled", false, DateTimeOffset.UtcNow);
            store.Reminders.Add(reminder);
            store.AddAudit("Reminder created", RiskLevel.Level2);
            return new ConversationResponse($"I created a local reminder: {title}.", null, null, reminder);
        }

        if (lower.Contains("transfer") || lower.Contains("buy ") || lower.Contains("deploy") || lower.Contains("unlock"))
        {
            var action = new PendingAction(Guid.NewGuid(), $"High-risk request blocked pending strong confirmation: \"{message}\".", RiskLevel.Level4, "Pending", DateTimeOffset.UtcNow);
            store.PendingActions.Add(action);
            store.AddAudit("Action queued for approval", RiskLevel.Level4);
            return new ConversationResponse("That is a high-risk action. I queued it for strong confirmation and will not execute it automatically.", action, null, null);
        }

        if (lower.Contains("calendar") || lower.Contains("meeting") || lower.Contains("send") || lower.Contains("email"))
        {
            var action = new PendingAction(Guid.NewGuid(), $"Prepare action from request: \"{message}\". Confirmation is required before execution.", RiskLevel.Level3, "Pending", DateTimeOffset.UtcNow);
            store.PendingActions.Add(action);
            store.AddAudit("Action queued for approval", RiskLevel.Level3);
            return new ConversationResponse("I prepared that as a pending action and paused for confirmation.", action, null, null);
        }

        store.AddAudit("Conversation response generated", RiskLevel.Level1);
        return new ConversationResponse("I can route that into memory, reminders, or an approval-gated action.", null, null, null);
    }

    private static string StripMemory(string message)
    {
        var result = Regex.Replace(message, @"^(vinan,?\s*)?remember\s+(that\s+)?", string.Empty, RegexOptions.IgnoreCase);
        return string.IsNullOrWhiteSpace(result) ? message : result.Trim(' ', ':', '.', ',');
    }

    private static string StripReminder(string message)
    {
        var result = Regex.Replace(message, @"^(vinan,?\s*)?(create|add|set)?\s*a?\s*reminder\s*(to|for)?\s*", string.Empty, RegexOptions.IgnoreCase);
        return string.IsNullOrWhiteSpace(result) ? message : result.Trim(' ', ':', '.', ',');
    }

    private static bool IsDateOnlyReminder(string title)
    {
        return title.Equals("today", StringComparison.OrdinalIgnoreCase)
            || title.Equals("tomorrow", StringComparison.OrdinalIgnoreCase)
            || title.Equals("tonight", StringComparison.OrdinalIgnoreCase);
    }
}

public static class VinanPermissions
{
    public static IReadOnlyList<ToolPermission> Default { get; } = new List<ToolPermission>
    {
        new("Calendar", "Read", "VINAN may inspect calendar information."),
        new("Gmail", "Prepare", "VINAN may draft but not send."),
        new("Reminders", "Execute", "VINAN may create reminders inside configured limits."),
        new("Files", "Confirm", "VINAN must ask before organizing files."),
        new("Finance", "Restricted", "VINAN must not execute financial actions automatically."),
        new("SmartHome", "Confirm", "VINAN must ask before device actions."),
        new("Production", "Restricted", "VINAN must not execute production changes automatically."),
    };
}

public enum RiskLevel
{
    Level1,
    Level2,
    Level3,
    Level4
}

public sealed record HealthResponse(string Status, DateTimeOffset TimeUtc);
public sealed record ConversationRequest(string Message);
public sealed record ConversationResponse(string Reply, PendingAction? PendingAction, MemoryItem? Memory, ReminderItem? Reminder);
public sealed record CreateMemoryRequest(string Text, string? Category);
public sealed record CreateReminderRequest(string Title, string? When);
public sealed record MemoryItem(Guid Id, string Text, string Category, DateTimeOffset CreatedAt);
public sealed record ReminderItem(Guid Id, string Title, string When, bool IsComplete, DateTimeOffset CreatedAt);
public sealed record PendingAction(Guid Id, string Summary, RiskLevel RiskLevel, string Status, DateTimeOffset CreatedAt);
public sealed record AuditEvent(Guid Id, string Action, string RiskLevel, DateTimeOffset CreatedAt);
public sealed record ToolPermission(string Name, string Level, string Description);
