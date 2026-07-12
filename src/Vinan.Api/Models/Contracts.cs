namespace Vinan.Api.Models;

public sealed record HealthResponse(string Status, DateTimeOffset TimeUtc, string Storage, string ModelProvider);
public sealed record ConversationRequest(string Message, Guid? ConversationId = null);
public sealed record ConversationResponse(
    string Reply,
    PendingAction? PendingAction,
    MemoryItem? Memory,
    ReminderItem? Reminder,
    Guid ConversationId,
    string Provider,
    NoteItem? Note = null,
    TaskItem? Task = null,
    IReadOnlyList<ToolExecution>? ToolExecutions = null);
public sealed record ConversationSummary(Guid Id, string Title, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record ConversationTurn(string Role, string Text);
public sealed record ConversationStreamEvent(string Type, string? Delta = null, ConversationResponse? Response = null, string? Error = null);
public sealed record ToolExecution(string ToolId, string Name, string Summary, DateTimeOffset CompletedAt);
public sealed record CreateMemoryRequest(string Text, string? Category);
public sealed record CreateReminderRequest(string Title, string? When);
public sealed record CreateNoteRequest(string Text);
public sealed record CreateTaskRequest(string Title, DateTimeOffset? DueAt = null, int Priority = 3);
public sealed record ToolPermission(string Name, string Level, string Description);
public sealed record ToolCapability(
    string Id,
    string Name,
    string Category,
    string Permission,
    string Status,
    string Description,
    string Provider);
public sealed record AiStatusResponse(bool Configured, string Provider, string Model, string ReasoningEffort, bool WebSearchEnabled);
public sealed record ConfigureAiRequest(string ApiKey, string? Model = null);
public sealed record AuthStatusResponse(
    bool IsConfigured,
    bool IsAuthenticated,
    string? OwnerName,
    Guid? DeviceId);
public sealed record SetupOwnerRequest(string DisplayName, string Passphrase, Guid DeviceId, string DeviceName);
public sealed record LoginRequest(string Passphrase, Guid DeviceId, string DeviceName, bool RememberMe = true);
public sealed record AuthenticatedDevice(Guid OwnerId, string OwnerName, Guid DeviceId);
public sealed record DeviceSummary(
    Guid Id,
    string Name,
    DateTimeOffset EnrolledAt,
    DateTimeOffset LastSeenAt,
    bool IsCurrent);

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
