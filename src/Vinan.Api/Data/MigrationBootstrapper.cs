using System.Data;
using Microsoft.EntityFrameworkCore;

namespace Vinan.Api.Data;

public static class MigrationBootstrapper
{
    public const string BaselineMigration = "20260712195456_PersistenceBaseline";

    public static async Task PrepareAndMigrateAsync(VinanDbContext database, CancellationToken cancellationToken = default)
    {
        if (database.Database.IsSqlite())
        {
            var connection = database.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;
            if (shouldClose)
            {
                await connection.OpenAsync(cancellationToken);
            }

            try
            {
                var hasExistingSchema = await TableExistsAsync(connection, "Memories", cancellationToken);
                var hasMigrationHistory = await TableExistsAsync(connection, "__EFMigrationsHistory", cancellationToken);
                if (hasExistingSchema && !hasMigrationHistory)
                {
                    await using var command = connection.CreateCommand();
                    command.CommandText = """
                        CREATE TABLE "__EFMigrationsHistory" (
                            "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
                            "ProductVersion" TEXT NOT NULL
                        );
                        INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                        VALUES ($migrationId, '7.0.20');
                        """;
                    var migration = command.CreateParameter();
                    migration.ParameterName = "$migrationId";
                    migration.Value = BaselineMigration;
                    command.Parameters.Add(migration);
                    await command.ExecuteNonQueryAsync(cancellationToken);
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

        await database.Database.MigrateAsync(cancellationToken);
    }

    private static async Task<bool> TableExistsAsync(
        System.Data.Common.DbConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;";
        var name = command.CreateParameter();
        name.ParameterName = "$name";
        name.Value = tableName;
        command.Parameters.Add(name);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) > 0;
    }
}
