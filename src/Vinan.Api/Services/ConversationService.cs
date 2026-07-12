using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Vinan.Api.Data;
using Vinan.Api.Models;

namespace Vinan.Api.Services;

public sealed class ConversationService
{
    private readonly VinanDbContext _database;
    private readonly IAssistantModel _model;
    private readonly AuditService _audit;
    private readonly MemoryRetrievalService _memory;
    private readonly WeatherService _weather;
    private readonly ITaskOptimizationEngine _taskOptimizer;

    public ConversationService(
        VinanDbContext database,
        IAssistantModel model,
        AuditService audit,
        MemoryRetrievalService memory,
        WeatherService weather,
        ITaskOptimizationEngine taskOptimizer)
    {
        _database = database;
        _model = model;
        _audit = audit;
        _memory = memory;
        _weather = weather;
        _taskOptimizer = taskOptimizer;
    }

    public async Task<ConversationResponse> HandleAsync(
        ConversationRequest request,
        CancellationToken cancellationToken = default)
    {
        var turn = await PrepareTurnAsync(request, cancellationToken);
        var outcome = await RouteToolAsync(turn.Message, cancellationToken);
        if (outcome is null)
        {
            var memories = await _memory.FindRelevantAsync(turn.Message, cancellationToken: cancellationToken);
            var modelReply = await _model.GenerateAsync(turn.Message, memories, turn.History, cancellationToken);
            _audit.Add($"Conversation response generated via {modelReply.Provider}", RiskLevel.Level1);
            if (modelReply.UsedFallback)
            {
                _audit.Add("Cloud model unavailable; local fallback used", RiskLevel.Level1);
            }
            outcome = new ConversationOutcome(modelReply.Text, modelReply.Provider);
        }

        return await CompleteTurnAsync(turn.Session, outcome, cancellationToken);
    }

    public async IAsyncEnumerable<ConversationStreamEvent> HandleStreamAsync(
        ConversationRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var turn = await PrepareTurnAsync(request, cancellationToken);
        var outcome = await RouteToolAsync(turn.Message, cancellationToken);
        if (outcome is not null)
        {
            yield return new ConversationStreamEvent("delta", outcome.Reply);
            var response = await CompleteTurnAsync(turn.Session, outcome, cancellationToken);
            yield return new ConversationStreamEvent("completed", Response: response);
            yield break;
        }

        var memories = await _memory.FindRelevantAsync(turn.Message, cancellationToken: cancellationToken);
        var reply = new StringBuilder();
        var provider = "Local";
        var usedFallback = false;
        await foreach (var chunk in _model.StreamAsync(turn.Message, memories, turn.History, cancellationToken))
        {
            reply.Append(chunk.Text);
            provider = chunk.Provider;
            usedFallback |= chunk.UsedFallback;
            yield return new ConversationStreamEvent("delta", chunk.Text);
        }

        if (reply.Length == 0)
        {
            throw new InvalidOperationException("VINAN received an empty response from the intelligence layer.");
        }

        _audit.Add($"Conversation response streamed via {provider}", RiskLevel.Level1);
        if (usedFallback)
        {
            _audit.Add("Cloud model unavailable; local fallback used", RiskLevel.Level1);
        }

        outcome = new ConversationOutcome(reply.ToString().Trim(), provider);
        var completed = await CompleteTurnAsync(turn.Session, outcome, cancellationToken);
        yield return new ConversationStreamEvent("completed", Response: completed);
    }

    private async Task<PreparedTurn> PrepareTurnAsync(ConversationRequest request, CancellationToken cancellationToken)
    {
        var message = request.Message.Trim();
        var now = DateTimeOffset.UtcNow;
        var session = await GetOrCreateSessionAsync(request.ConversationId, message, now, cancellationToken);
        var history = await _database.ConversationMessages.AsNoTracking()
            .Where(item => item.ConversationId == session.Id)
            .OrderByDescending(item => item.CreatedAt)
            .Take(20)
            .OrderBy(item => item.CreatedAt)
            .Select(item => new ConversationTurn(item.Role, item.Text))
            .ToListAsync(cancellationToken);

        _database.ConversationMessages.Add(new ConversationMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = session.Id,
            Role = "user",
            Text = message,
            Provider = "Owner",
            CreatedAt = now,
        });
        session.UpdatedAt = now;
        await _database.SaveChangesAsync(cancellationToken);
        return new PreparedTurn(message, session, history);
    }

    private async Task<ConversationResponse> CompleteTurnAsync(
        ConversationSession session,
        ConversationOutcome outcome,
        CancellationToken cancellationToken)
    {
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
            outcome.Provider,
            outcome.Note,
            outcome.Task,
            outcome.ToolExecutions);
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

    private async Task<ConversationOutcome?> RouteToolAsync(string message, CancellationToken cancellationToken)
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
            return ToolOutcome(
                $"I saved this as approved memory: {memoryText}",
                "VINAN Memory",
                "memory",
                "Adaptive Memory",
                "Approved memory stored",
                Memory: memory);
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
            return ToolOutcome(
                $"I created a reminder: {reminderTitle} ({reminderWhen}).",
                "VINAN Reminders",
                "reminders",
                "Reminders",
                "Reminder created",
                Reminder: reminder);
        }

        if (IntentParser.TryParseNote(message, out var noteText))
        {
            var now = DateTimeOffset.UtcNow;
            var note = new NoteItem { Id = Guid.NewGuid(), Text = noteText, CreatedAt = now, UpdatedAt = now };
            _database.Notes.Add(note);
            _audit.Add("Private note created", RiskLevel.Level2);
            return ToolOutcome(
                $"I saved an encrypted note: {noteText}",
                "VINAN Notes",
                "notes",
                "Private Notes",
                "Encrypted note saved",
                Note: note);
        }

        if (IntentParser.TryParseTask(message, out var taskTitle, out var dueAt, out var priority))
        {
            var task = new TaskItem
            {
                Id = Guid.NewGuid(),
                Title = taskTitle,
                DueAt = dueAt,
                Priority = priority,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            _database.Tasks.Add(task);
            _audit.Add("Task created", RiskLevel.Level2);
            var schedule = dueAt is null ? string.Empty : $" Due {dueAt.Value:g}.";
            return ToolOutcome(
                $"I created the task: {taskTitle}.{schedule}",
                "VINAN Tasks",
                "tasks",
                "Task Manager",
                "Task created",
                Task: task);
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
            return new ConversationOutcome(
                isHighRisk
                    ? "That is a high-risk action. I queued it for strong confirmation and will not execute it automatically."
                    : "I prepared that as a pending action and paused for confirmation.",
                "VINAN Safety",
                PendingAction: action,
                ToolExecutions: new[] { Receipt("safety", "Safety Engine", "Action paused for owner approval") });
        }

        if (IntentParser.IsMemoryReview(message))
        {
            var memories = await _database.Memories.AsNoTracking()
                .OrderByDescending(item => item.CreatedAt)
                .Take(30)
                .ToListAsync(cancellationToken);
            _audit.Add("Memory reviewed", RiskLevel.Level1);
            var reply = memories.Count == 0
                ? "I do not have approved long-term memories yet."
                : $"Here is what I currently remember: {string.Join("; ", memories.Select(item => item.Text))}.";
            return ToolOutcome(reply, "VINAN Memory", "memory", "Adaptive Memory", $"Reviewed {memories.Count} memories");
        }

        if (IntentParser.IsNoteReview(message))
        {
            var notes = await _database.Notes.AsNoTracking().OrderByDescending(item => item.UpdatedAt).Take(20).ToListAsync(cancellationToken);
            _audit.Add("Private notes reviewed", RiskLevel.Level1);
            var reply = notes.Count == 0 ? "You do not have any saved notes." : $"Your latest notes: {string.Join("; ", notes.Select(item => item.Text))}.";
            return ToolOutcome(reply, "VINAN Notes", "notes", "Private Notes", $"Reviewed {notes.Count} notes");
        }

        if (IntentParser.IsTaskPrioritization(message))
        {
            var tasks = await _taskOptimizer.RankAsync(cancellationToken);
            _audit.Add("Tasks prioritized", RiskLevel.Level1);
            var reply = tasks.Count == 0
                ? "You do not have open tasks to prioritize."
                : "Priority order: " + string.Join("; ", tasks.Select((task, index) => $"{index + 1}. {task.Title}")) + ".";
            return ToolOutcome(reply, _taskOptimizer.Provider, "task-optimizer", "Task Optimizer", $"Ranked {tasks.Count} tasks");
        }

        if (IntentParser.IsTaskReview(message))
        {
            var tasks = await _database.Tasks.AsNoTracking().Where(item => !item.IsComplete)
                .OrderBy(item => item.DueAt == null).ThenBy(item => item.DueAt).ThenBy(item => item.Priority)
                .Take(30).ToListAsync(cancellationToken);
            _audit.Add("Tasks reviewed", RiskLevel.Level1);
            var reply = tasks.Count == 0 ? "You do not have any open tasks." : $"Your open tasks: {string.Join("; ", tasks.Select(item => item.Title))}.";
            return ToolOutcome(reply, "VINAN Tasks", "tasks", "Task Manager", $"Reviewed {tasks.Count} tasks");
        }

        if (IntentParser.IsWeatherRequest(message))
        {
            if (!IntentParser.TryParseWeatherLocation(message, out var location))
            {
                return ToolOutcome("Which city or place should I check? Try: weather in San Francisco.", "VINAN Weather", "weather", "Live Weather", "Location required");
            }

            try
            {
                var report = await _weather.GetAsync(location, cancellationToken);
                _audit.Add("Live weather checked", RiskLevel.Level1);
                return report is null
                    ? ToolOutcome($"I could not find a weather location matching {location}.", "Open-Meteo", "weather", "Live Weather", "Location not found")
                    : ToolOutcome(WeatherService.Format(report), "Open-Meteo", "weather", "Live Weather", $"Forecast read for {report.Location}");
            }
            catch (HttpRequestException)
            {
                _audit.Add("Weather provider unavailable", RiskLevel.Level1);
                return ToolOutcome("The live weather service is temporarily unavailable. I did not invent a forecast.", "VINAN Weather", "weather", "Live Weather", "Provider unavailable");
            }
        }

        if (IntentParser.TryCalculate(message, out var expression, out var calculation))
        {
            _audit.Add("Calculator used", RiskLevel.Level1);
            return ToolOutcome($"{expression} = {calculation}", "VINAN Calculator", "calculator", "Calculator", "Calculation completed");
        }

        if (IntentParser.IsDateOrTimeRequest(message))
        {
            _audit.Add("Date and time checked", RiskLevel.Level1);
            return ToolOutcome($"It is {DateTimeOffset.Now:F}.", "VINAN Clock", "clock", "Date and Time", "Local time read");
        }

        return null;
    }

    private static ConversationOutcome ToolOutcome(
        string reply,
        string provider,
        string toolId,
        string toolName,
        string summary,
        PendingAction? PendingAction = null,
        MemoryItem? Memory = null,
        ReminderItem? Reminder = null,
        NoteItem? Note = null,
        TaskItem? Task = null) => new(
            reply,
            provider,
            PendingAction,
            Memory,
            Reminder,
            Note,
            Task,
            new[] { Receipt(toolId, toolName, summary) });

    private static ToolExecution Receipt(string id, string name, string summary) =>
        new(id, name, summary, DateTimeOffset.UtcNow);

    private static string BuildTitle(string message) => message.Length <= 60 ? message : $"{message[..57]}...";

    private sealed record PreparedTurn(
        string Message,
        ConversationSession Session,
        IReadOnlyCollection<ConversationTurn> History);

    private sealed record ConversationOutcome(
        string Reply,
        string Provider,
        PendingAction? PendingAction = null,
        MemoryItem? Memory = null,
        ReminderItem? Reminder = null,
        NoteItem? Note = null,
        TaskItem? Task = null,
        IReadOnlyList<ToolExecution>? ToolExecutions = null);
}
