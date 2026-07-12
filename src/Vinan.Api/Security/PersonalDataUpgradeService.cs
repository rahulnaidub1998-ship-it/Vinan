using System.Data;
using Microsoft.EntityFrameworkCore;
using Vinan.Api.Data;

namespace Vinan.Api.Security;

public sealed class PersonalDataUpgradeService
{
    private static readonly (string Table, string Column)[] ProtectedColumns =
    {
        ("Memories", "Text"),
        ("Reminders", "Title"),
        ("Reminders", "When"),
        ("PendingActions", "Summary"),
        ("AuditEvents", "Action"),
        ("Conversations", "Title"),
        ("ConversationMessages", "Text"),
        ("OwnerProfiles", "DisplayName"),
        ("DeviceEnrollments", "Name"),
    };

    private readonly VinanDbContext _database;
    private readonly PersonalDataProtector _protector;

    public PersonalDataUpgradeService(VinanDbContext database, PersonalDataProtector protector)
    {
        _database = database;
        _protector = protector;
    }

    public async Task UpgradeLegacyRowsAsync(CancellationToken cancellationToken = default)
    {
        if (!_database.Database.IsSqlite())
        {
            return;
        }

        var connection = _database.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            foreach (var (table, column) in ProtectedColumns)
            {
                var legacyRows = new List<(string Id, string Value)>();
                await using (var select = connection.CreateCommand())
                {
                    select.CommandText = $"SELECT \"Id\", \"{column}\" FROM \"{table}\" WHERE \"{column}\" NOT LIKE $prefix AND \"{column}\" <> '';";
                    var prefix = select.CreateParameter();
                    prefix.ParameterName = "$prefix";
                    prefix.Value = PersonalDataProtector.Prefix + "%";
                    select.Parameters.Add(prefix);
                    await using var reader = await select.ExecuteReaderAsync(cancellationToken);
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        legacyRows.Add((reader.GetString(0), reader.GetString(1)));
                    }
                }

                foreach (var (id, value) in legacyRows)
                {
                    await using var update = connection.CreateCommand();
                    update.CommandText = $"UPDATE \"{table}\" SET \"{column}\" = $value WHERE \"Id\" = $id;";
                    var protectedValue = update.CreateParameter();
                    protectedValue.ParameterName = "$value";
                    protectedValue.Value = _protector.Protect(value);
                    update.Parameters.Add(protectedValue);
                    var rowId = update.CreateParameter();
                    rowId.ParameterName = "$id";
                    rowId.Value = id;
                    update.Parameters.Add(rowId);
                    await update.ExecuteNonQueryAsync(cancellationToken);
                }
            }
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }
}
