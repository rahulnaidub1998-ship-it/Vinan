using Vinan.Api.Models;

namespace Vinan.Api.Services;

public interface IAssistantModel
{
    Task<ModelReply> GenerateAsync(
        string message,
        IReadOnlyCollection<MemoryItem> memories,
        CancellationToken cancellationToken);
}

public sealed record ModelReply(string Text, string Provider, bool UsedFallback = false);
