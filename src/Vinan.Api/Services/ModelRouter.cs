using System.Text.Json;
using Microsoft.Extensions.Options;
using Vinan.Api.Configuration;
using Vinan.Api.Models;

namespace Vinan.Api.Services;

public sealed class ModelRouter : IAssistantModel
{
    private readonly LocalAssistantModel _local;
    private readonly OpenAiAssistantModel _openAi;
    private readonly ModelOptions _options;

    public ModelRouter(
        LocalAssistantModel local,
        OpenAiAssistantModel openAi,
        IOptions<ModelOptions> options)
    {
        _local = local;
        _openAi = openAi;
        _options = options.Value;
    }

    public string ActiveProvider => ShouldUseOpenAi ? _openAi.ProviderName : "Local";

    public async Task<ModelReply> GenerateAsync(
        string message,
        IReadOnlyCollection<MemoryItem> memories,
        CancellationToken cancellationToken)
    {
        if (!ShouldUseOpenAi)
        {
            return await _local.GenerateAsync(message, memories, cancellationToken);
        }

        try
        {
            return await _openAi.GenerateAsync(message, memories, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is HttpRequestException
                or JsonException
                or InvalidOperationException
                or TaskCanceledException)
        {
            var fallback = await _local.GenerateAsync(message, memories, cancellationToken);
            return fallback with { Provider = "Local fallback", UsedFallback = true };
        }
    }

    private bool ShouldUseOpenAi =>
        !_options.Provider.Equals("Local", StringComparison.OrdinalIgnoreCase)
        && _openAi.IsConfigured;
}
