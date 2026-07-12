using Microsoft.EntityFrameworkCore;
using Vinan.Api.Data;
using Vinan.Api.Models;

namespace Vinan.Api.Services;

public interface ITaskOptimizationEngine
{
    string Provider { get; }
    Task<IReadOnlyList<TaskItem>> RankAsync(CancellationToken cancellationToken = default);
}

public sealed class ClassicalTaskOptimizationEngine : ITaskOptimizationEngine
{
    private readonly VinanDbContext _database;

    public ClassicalTaskOptimizationEngine(VinanDbContext database)
    {
        _database = database;
    }

    public string Provider => "VINAN classical exact engine";

    public async Task<IReadOnlyList<TaskItem>> RankAsync(CancellationToken cancellationToken = default)
    {
        var tasks = await _database.Tasks.AsNoTracking()
            .Where(item => !item.IsComplete)
            .ToListAsync(cancellationToken);

        return tasks
            .OrderBy(item => item.DueAt is null)
            .ThenBy(item => item.DueAt)
            .ThenBy(item => item.Priority)
            .ThenBy(item => item.CreatedAt)
            .ToList();
    }
}
