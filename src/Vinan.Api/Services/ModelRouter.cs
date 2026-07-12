using System.Runtime.CompilerServices;
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
    private readonly AiConfigurationService _configuration;

    public ModelRouter(
        LocalAssistantModel local,
        OpenAiAssistantModel openAi,
        IOptions<ModelOptions> options,
        AiConfigurationService configuration)
    {
        _local = local;
        _openAi = openAi;
        _options = options.Value;
        _configuration = configuration;
    }

    public async Task<AiStatusResponse> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        if (!ShouldAttemptOpenAi)
        {
            return new AiStatusResponse(false, "Local", _options.Model, _options.ReasoningEffort, false);
        }

        return await _configuration.GetStatusAsync(cancellationToken);
    }

    public async Task<ModelReply> GenerateAsync(
        string message,
        IReadOnlyCollection<MemoryItem> memories,
        IReadOnlyCollection<ConversationTurn> history,
        CancellationToken cancellationToken)
    {
        if (!ShouldAttemptOpenAi || !await _openAi.IsConfiguredAsync(cancellationToken))
        {
            return await _local.GenerateAsync(message, memories, history, cancellationToken);
        }

        try
        {
            return await _openAi.GenerateAsync(message, memories, history, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (IsProviderFailure(exception))
        {
            var fallback = await _local.GenerateAsync(message, memories, history, cancellationToken);
            return fallback with { Provider = "Local fallback", UsedFallback = true };
        }
    }

    public async IAsyncEnumerable<ModelStreamChunk> StreamAsync(
        string message,
        IReadOnlyCollection<MemoryItem> memories,
        IReadOnlyCollection<ConversationTurn> history,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (!ShouldAttemptOpenAi || !await _openAi.IsConfiguredAsync(cancellationToken))
        {
            await foreach (var chunk in _local.StreamAsync(message, memories, history, cancellationToken))
            {
                yield return chunk;
            }
            yield break;
        }

        var emitted = false;
        var failedBeforeOutput = false;
        await using (var enumerator = _openAi.StreamAsync(message, memories, history, cancellationToken)
            .GetAsyncEnumerator(cancellationToken))
        {
            while (true)
            {
                ModelStreamChunk chunk;
                try
                {
                    if (!await enumerator.MoveNextAsync())
                    {
                        break;
                    }
                    chunk = enumerator.Current;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception) when (!emitted && IsProviderFailure(exception))
                {
                    failedBeforeOutput = true;
                    break;
                }

                emitted = true;
                yield return chunk;
            }
        }

        if (failedBeforeOutput)
        {
            await foreach (var chunk in _local.StreamAsync(message, memories, history, cancellationToken))
            {
                yield return chunk with { Provider = "Local fallback", UsedFallback = true };
            }
        }
    }

    private bool ShouldAttemptOpenAi => !_options.Provider.Equals("Local", StringComparison.OrdinalIgnoreCase);

    private static bool IsProviderFailure(Exception exception) => exception is
        HttpRequestException or JsonException or InvalidOperationException or TaskCanceledException;
}
