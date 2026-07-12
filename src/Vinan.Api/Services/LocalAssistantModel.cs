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
        var reply = BuildReply(message, memoryContext);
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

    private static string BuildReply(string message, string memoryContext)
    {
        if (message.Contains("plan", StringComparison.OrdinalIgnoreCase))
        {
            return $"Here is a practical starting plan for \"{message}\": define the outcome, list constraints, break the work into verifiable steps, execute safe local steps, and pause before any external or high-risk action. {memoryContext}";
        }

        return $"The advanced model is not connected yet. {memoryContext} I can still execute local memory, notes, tasks, reminders, weather, calculations, task prioritization, and approval-gated actions. Connect an OpenAI API key in AI Settings for deeper reasoning and current web research.";
    }
}
