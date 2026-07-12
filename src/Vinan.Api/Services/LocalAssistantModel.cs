using Vinan.Api.Models;

namespace Vinan.Api.Services;

public sealed class LocalAssistantModel : IAssistantModel
{
    public Task<ModelReply> GenerateAsync(
        string message,
        IReadOnlyCollection<MemoryItem> memories,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var memoryContext = memories.Count == 0
            ? "I do not have approved personal context for this yet."
            : $"I currently have {memories.Count} approved memory item{(memories.Count == 1 ? string.Empty : "s")} available.";
        var reply = $"I received: \"{message}\". {memoryContext} I can safely manage memory, reminders, calculations, and approval-gated actions while the optional AI provider is offline.";
        return Task.FromResult(new ModelReply(reply, "Local"));
    }
}
