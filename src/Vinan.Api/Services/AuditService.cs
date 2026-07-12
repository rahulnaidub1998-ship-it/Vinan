using Vinan.Api.Data;
using Vinan.Api.Models;

namespace Vinan.Api.Services;

public sealed class AuditService
{
    private readonly VinanDbContext _database;

    public AuditService(VinanDbContext database)
    {
        _database = database;
    }

    public void Add(string action, RiskLevel riskLevel)
    {
        _database.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.NewGuid(),
            Action = action,
            RiskLevel = riskLevel,
            CreatedAt = DateTimeOffset.UtcNow,
        });
    }
}
