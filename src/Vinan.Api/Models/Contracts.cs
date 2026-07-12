namespace Vinan.Api.Models;

public sealed record HealthResponse(string Status, DateTimeOffset TimeUtc, string Storage, string ModelProvider);
public sealed record ConversationRequest(string Message, Guid? ConversationId = null);
public sealed record ConversationResponse(
    string Reply,
    PendingAction? PendingAction,
    MemoryItem? Memory,
    ReminderItem? Reminder,
    Guid ConversationId,
    string Provider);
public sealed record ConversationSummary(Guid Id, string Title, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record CreateMemoryRequest(string Text, string? Category);
public sealed record CreateReminderRequest(string Title, string? When);
public sealed record ToolPermission(string Name, string Level, string Description);

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
