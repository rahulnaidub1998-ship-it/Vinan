using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Vinan.Api.Configuration;
using Vinan.Api.Data;
using Vinan.Api.Models;

namespace Vinan.Api.Services;

public sealed class AiConfigurationService
{
    private const string ProviderName = "OpenAI";

    private readonly VinanDbContext _database;
    private readonly ModelOptions _options;

    public AiConfigurationService(VinanDbContext database, IOptions<ModelOptions> options)
    {
        _database = database;
        _options = options.Value;
    }

    public async Task<ResolvedAiConfiguration> ResolveAsync(CancellationToken cancellationToken = default)
    {
        var configuredKey = string.IsNullOrWhiteSpace(_options.ApiKey)
            ? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
            : _options.ApiKey;
        var credential = await _database.ProviderCredentials.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Provider == ProviderName, cancellationToken);
        var ownerId = await _database.OwnerProfiles.AsNoTracking()
            .Select(item => (Guid?)item.Id)
            .SingleOrDefaultAsync(cancellationToken);
        var key = string.IsNullOrWhiteSpace(configuredKey) ? credential?.Secret : configuredKey;
        var model = string.IsNullOrWhiteSpace(credential?.Model) ? _options.Model : credential.Model;

        return new ResolvedAiConfiguration(
            !string.IsNullOrWhiteSpace(key),
            key,
            model,
            _options.ReasoningEffort,
            _options.Verbosity,
            _options.WebSearchEnabled,
            BuildSafetyIdentifier(ownerId));
    }

    public async Task<AiStatusResponse> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var configuration = await ResolveAsync(cancellationToken);
        return new AiStatusResponse(
            configuration.Configured,
            configuration.Configured ? "OpenAI" : "Local",
            configuration.Model,
            configuration.ReasoningEffort,
            configuration.WebSearchEnabled && configuration.Configured);
    }

    public async Task<AiStatusResponse> ConfigureAsync(ConfigureAiRequest request, CancellationToken cancellationToken = default)
    {
        var key = request.ApiKey.Trim();
        if (key.Length < 20)
        {
            throw new ArgumentException("Enter a valid OpenAI API key.", nameof(request));
        }

        var credential = await _database.ProviderCredentials
            .SingleOrDefaultAsync(item => item.Provider == ProviderName, cancellationToken);
        if (credential is null)
        {
            credential = new ProviderCredential
            {
                Id = Guid.NewGuid(),
                Provider = ProviderName,
            };
            _database.ProviderCredentials.Add(credential);
        }

        credential.Secret = key;
        credential.Model = string.IsNullOrWhiteSpace(request.Model) ? _options.Model : request.Model.Trim();
        credential.UpdatedAt = DateTimeOffset.UtcNow;
        await _database.SaveChangesAsync(cancellationToken);
        return await GetStatusAsync(cancellationToken);
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        var credential = await _database.ProviderCredentials
            .SingleOrDefaultAsync(item => item.Provider == ProviderName, cancellationToken);
        if (credential is null)
        {
            return;
        }

        _database.ProviderCredentials.Remove(credential);
        await _database.SaveChangesAsync(cancellationToken);
    }

    private static string BuildSafetyIdentifier(Guid? ownerId)
    {
        if (ownerId is null)
        {
            return "vinan_unconfigured";
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"VINAN:{ownerId.Value:N}"));
        return Convert.ToHexString(hash).ToLowerInvariant()[..32];
    }
}

public sealed record ResolvedAiConfiguration(
    bool Configured,
    string? ApiKey,
    string Model,
    string ReasoningEffort,
    string Verbosity,
    bool WebSearchEnabled,
    string SafetyIdentifier);
