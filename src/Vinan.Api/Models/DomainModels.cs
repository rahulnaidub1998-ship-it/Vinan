namespace Vinan.Api.Models;

public enum RiskLevel
{
    Level1,
    Level2,
    Level3,
    Level4,
}

public enum ActionStatus
{
    Pending,
    Approved,
    Denied,
}

public sealed class MemoryItem
{
    public Guid Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public string Category { get; set; } = "Preference";
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class ReminderItem
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string When { get; set; } = "Scheduled";
    public bool IsComplete { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class PendingAction
{
    public Guid Id { get; set; }
    public string Summary { get; set; } = string.Empty;
    public RiskLevel RiskLevel { get; set; }
    public ActionStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
}

public sealed class AuditEvent
{
    public Guid Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public RiskLevel RiskLevel { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class ConversationSession
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "New conversation";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class ConversationMessage
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
