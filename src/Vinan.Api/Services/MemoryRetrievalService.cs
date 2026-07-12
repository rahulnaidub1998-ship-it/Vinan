using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Vinan.Api.Data;
using Vinan.Api.Models;

namespace Vinan.Api.Services;

public sealed partial class MemoryRetrievalService
{
    private readonly VinanDbContext _database;

    public MemoryRetrievalService(VinanDbContext database)
    {
        _database = database;
    }

    public async Task<List<MemoryItem>> FindRelevantAsync(
        string query,
        int limit = 12,
        CancellationToken cancellationToken = default)
    {
        var memories = await _database.Memories.AsNoTracking()
            .OrderByDescending(item => item.CreatedAt)
            .Take(200)
            .ToListAsync(cancellationToken);
        if (memories.Count <= limit)
        {
            return memories;
        }

        var queryVector = Vectorize(query);
        return memories
            .Select((memory, index) => new
            {
                Memory = memory,
                Score = Cosine(queryVector, Vectorize(memory.Text)) + (0.08 / (index + 1)),
            })
            .OrderByDescending(item => item.Score)
            .Take(limit)
            .Select(item => item.Memory)
            .ToList();
    }

    internal static double Similarity(string first, string second) =>
        Cosine(Vectorize(first), Vectorize(second));

    private static Dictionary<string, double> Vectorize(string value)
    {
        return Tokens().Matches(value.ToLowerInvariant())
            .Select(match => match.Value)
            .Where(token => token.Length > 1)
            .GroupBy(token => token)
            .ToDictionary(group => group.Key, group => 1d + Math.Log(group.Count()));
    }

    private static double Cosine(IReadOnlyDictionary<string, double> first, IReadOnlyDictionary<string, double> second)
    {
        if (first.Count == 0 || second.Count == 0)
        {
            return 0;
        }

        var dot = first.Sum(item => item.Value * second.GetValueOrDefault(item.Key));
        var firstLength = Math.Sqrt(first.Sum(item => item.Value * item.Value));
        var secondLength = Math.Sqrt(second.Sum(item => item.Value * item.Value));
        return firstLength == 0 || secondLength == 0 ? 0 : dot / (firstLength * secondLength);
    }

    [GeneratedRegex(@"[a-z0-9']+", RegexOptions.IgnoreCase)]
    private static partial Regex Tokens();
}
