using Vinan.Api.Models;

namespace Vinan.Api.Services;

public sealed class LocalAssistantModel : IAssistantModel
{
    public Task<ModelReply> GenerateAsync(
        string message,
        IReadOnlyCollection<MemoryItem> memories,
        IReadOnlyCollection<ConversationTurn> history,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var memoryContext = memories.Count == 0
            ? "I do not have approved personal context for this yet."
            : $"I currently have {memories.Count} approved memory item{(memories.Count == 1 ? string.Empty : "s")} available.";
        var reply = BuildReply(message, memoryContext, history);
        return Task.FromResult(new ModelReply(reply, "Local"));
    }

    public async IAsyncEnumerable<ModelStreamChunk> StreamAsync(
        string message,
        IReadOnlyCollection<MemoryItem> memories,
        IReadOnlyCollection<ConversationTurn> history,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var reply = await GenerateAsync(message, memories, history, cancellationToken);
        foreach (var part in reply.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new ModelStreamChunk(part + " ", reply.Provider);
        }
    }

    private static string BuildReply(
        string message,
        string memoryContext,
        IReadOnlyCollection<ConversationTurn> history)
    {
        var text = message.Trim();
        var normalized = text.TrimEnd('.', '!', '?').ToLowerInvariant();
        if (normalized is "hi" or "hello" or "hey" or "hi vinan" or "hello vinan" or "hey vinan")
        {
            return "Hi. I'm here and ready. What would you like to work on?";
        }

        if (normalized is "how are you" or "how are you doing")
        {
            return "I'm online and ready to help. What is on your mind?";
        }

        if (normalized is "thanks" or "thank you" or "thank you vinan")
        {
            return "You're welcome.";
        }

        if (normalized.Contains("what can you do", StringComparison.Ordinal)
            || normalized.Contains("your capabilities", StringComparison.Ordinal))
        {
            return "I can manage encrypted memory, notes, tasks, reminders, live weather, calculations, task priorities, conversation history, and approval-gated actions. A connected AI provider adds deeper analysis and current web research.";
        }

        if (normalized.Contains("what did i", StringComparison.Ordinal)
            || normalized.Contains("what was my last", StringComparison.Ordinal))
        {
            var priorUserMessage = history.LastOrDefault(turn => turn.Role.Equals("user", StringComparison.OrdinalIgnoreCase));
            return priorUserMessage is null
                ? "There is no earlier user message in this conversation yet."
                : $"Your previous message was: \"{priorUserMessage.Text}\"";
        }

        if (message.Contains("plan", StringComparison.OrdinalIgnoreCase))
        {
            return $"Here is a practical starting plan for \"{message}\": define the outcome, list constraints, break the work into verifiable steps, execute safe local steps, and pause before any external or high-risk action. {memoryContext}";
        }

        if (RequiresAdvancedReasoning(normalized))
        {
            return $"That request needs the advanced reasoning provider, which is not connected yet. {memoryContext} Connect an OpenAI API key under Intelligence, then ask again.";
        }

        return "I'm ready to help. Ask me to create or review memory, notes, tasks, reminders, weather, calculations, or a safe plan.";
    }

    private static bool RequiresAdvancedReasoning(string message)
    {
        var intents = new[] { "analyze", "compare", "explain", "research", "summarize", "write", "debug", "code" };
        return intents.Any(intent => message.Contains(intent, StringComparison.Ordinal));
    }
}
