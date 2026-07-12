using Vinan.Api.Models;

namespace Vinan.Api.Services;

public interface IAssistantModel
{
    Task<ModelReply> GenerateAsync(
        string message,
        IReadOnlyCollection<MemoryItem> memories,
        IReadOnlyCollection<ConversationTurn> history,
        CancellationToken cancellationToken);

    IAsyncEnumerable<ModelStreamChunk> StreamAsync(
        string message,
        IReadOnlyCollection<MemoryItem> memories,
        IReadOnlyCollection<ConversationTurn> history,
        CancellationToken cancellationToken);
}

public sealed record ModelReply(string Text, string Provider, bool UsedFallback = false);
public sealed record ModelStreamChunk(string Text, string Provider, bool UsedFallback = false);
