using Microsoft.EntityFrameworkCore;
using Vinan.Api.Data;
using Vinan.Api.Models;

namespace Vinan.Api.Services;

public sealed class ConversationService
{
    private readonly VinanDbContext _database;
    private readonly IAssistantModel _model;
    private readonly AuditService _audit;

    public ConversationService(VinanDbContext database, IAssistantModel model, AuditService audit)
    {
        _database = database;
        _model = model;
        _audit = audit;
    }

    public async Task<ConversationResponse> HandleAsync(
        ConversationRequest request,
        CancellationToken cancellationToken = default)
    {
        var message = request.Message.Trim();
        var now = DateTimeOffset.UtcNow;
        var session = await GetOrCreateSessionAsync(request.ConversationId, message, now, cancellationToken);

        _database.ConversationMessages.Add(new ConversationMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = session.Id,
            Role = "user",
            Text = message,
            Provider = "Owner",
            CreatedAt = now,
        });

        var outcome = await RouteAsync(message, cancellationToken);
        session.UpdatedAt = DateTimeOffset.UtcNow;
        _database.ConversationMessages.Add(new ConversationMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = session.Id,
            Role = "assistant",
            Text = outcome.Reply,
            Provider = outcome.Provider,
            CreatedAt = session.UpdatedAt,
        });

        await _database.SaveChangesAsync(cancellationToken);
        return new ConversationResponse(
            outcome.Reply,
            outcome.PendingAction,
            outcome.Memory,
            outcome.Reminder,
            session.Id,
            outcome.Provider);
    }

    private async Task<ConversationSession> GetOrCreateSessionAsync(
        Guid? requestedId,
        string firstMessage,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (requestedId is not null)
        {
            var existing = await _database.Conversations.FindAsync(new object[] { requestedId.Value }, cancellationToken);
            if (existing is not null)
            {
                return existing;
            }
        }

        var session = new ConversationSession
        {
            Id = Guid.NewGuid(),
            Title = BuildTitle(firstMessage),
            CreatedAt = now,
            UpdatedAt = now,
        };
        _database.Conversations.Add(session);
        return session;
    }

    private async Task<ConversationOutcome> RouteAsync(string message, CancellationToken cancellationToken)
    {
        if (IntentParser.TryParseMemory(message, out var memoryText))
        {
            var memory = new MemoryItem
            {
                Id = Guid.NewGuid(),
                Text = memoryText,
                Category = "Preference",
                CreatedAt = DateTimeOffset.UtcNow,
            };
            _database.Memories.Add(memory);
            _audit.Add("Approved memory stored", RiskLevel.Level2);
            return new ConversationOutcome($"I saved this as approved memory: {memoryText}", "VINAN Memory", Memory: memory);
        }

        if (IntentParser.TryParseReminder(message, out var reminderTitle, out var reminderWhen))
        {
            var reminder = new ReminderItem
            {
                Id = Guid.NewGuid(),
                Title = reminderTitle,
                When = reminderWhen,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            _database.Reminders.Add(reminder);
            _audit.Add("Reminder created", RiskLevel.Level2);
            return new ConversationOutcome($"I created a reminder: {reminderTitle} ({reminderWhen}).", "VINAN Reminders", Reminder: reminder);
        }

        var riskLevel = RiskClassifier.Classify(message);
        if (riskLevel is RiskLevel.Level3 or RiskLevel.Level4)
        {
            var isHighRisk = riskLevel == RiskLevel.Level4;
            var action = new PendingAction
            {
                Id = Guid.NewGuid(),
                Summary = isHighRisk
                    ? $"High-risk request blocked pending strong confirmation: \"{message}\"."
                    : $"Prepare action from request: \"{message}\". Confirmation is required before execution.",
                RiskLevel = riskLevel,
                Status = ActionStatus.Pending,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            _database.PendingActions.Add(action);
            _audit.Add("Action queued for approval", riskLevel);
            var reply = isHighRisk
                ? "That is a high-risk action. I queued it for strong confirmation and will not execute it automatically."
                : "I prepared that as a pending action and paused for confirmation.";
            return new ConversationOutcome(reply, "VINAN Safety", PendingAction: action);
        }

        if (IntentParser.IsMemoryReview(message))
        {
            var memories = await LoadMemoriesAsync(cancellationToken);
            _audit.Add("Memory reviewed", RiskLevel.Level1);
            var reply = memories.Count == 0
                ? "I do not have approved long-term memories yet."
                : $"Here is what I currently remember: {string.Join("; ", memories.Select(item => item.Text))}.";
            return new ConversationOutcome(reply, "VINAN Memory");
        }

        if (IntentParser.TryCalculate(message, out var expression, out var calculation))
        {
            _audit.Add("Calculator used", RiskLevel.Level1);
            return new ConversationOutcome($"{expression} = {calculation}", "VINAN Calculator");
        }

        if (IntentParser.IsDateOrTimeRequest(message))
        {
            _audit.Add("Date and time checked", RiskLevel.Level1);
            return new ConversationOutcome($"It is {DateTimeOffset.Now:F}.", "VINAN Clock");
        }

        var approvedMemories = await LoadMemoriesAsync(cancellationToken);
        var modelReply = await _model.GenerateAsync(message, approvedMemories, cancellationToken);
        _audit.Add($"Conversation response generated via {modelReply.Provider}", RiskLevel.Level1);
        if (modelReply.UsedFallback)
        {
            _audit.Add("Cloud model unavailable; local fallback used", RiskLevel.Level1);
        }

        return new ConversationOutcome(modelReply.Text, modelReply.Provider);
    }

    private async Task<List<MemoryItem>> LoadMemoriesAsync(CancellationToken cancellationToken)
    {
        return await _database.Memories.AsNoTracking()
            .OrderByDescending(item => item.CreatedAt)
            .Take(20)
            .ToListAsync(cancellationToken);
    }

    private static string BuildTitle(string message)
    {
        return message.Length <= 60 ? message : $"{message[..57]}...";
    }

    private sealed record ConversationOutcome(
        string Reply,
        string Provider,
        PendingAction? PendingAction = null,
        MemoryItem? Memory = null,
        ReminderItem? Reminder = null);
}
