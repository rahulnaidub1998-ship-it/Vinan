using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Vinan.Api.Configuration;
using Vinan.Api.Data;
using Vinan.Api.Models;
using Vinan.Api.Security;
using Vinan.Api.Services;

namespace Vinan.Api.Tests;

public sealed class AdaptiveIntelligenceTests
{
    [Fact]
    public async Task RanksRelevantMemoryAheadOfUnrelatedRecentMemory()
    {
        await using var fixture = await IntelligenceFixture.CreateAsync();
        fixture.Database.Memories.AddRange(
            new MemoryItem { Id = Guid.NewGuid(), Text = "I enjoy hiking in the Alps", Category = "Travel", CreatedAt = DateTimeOffset.UtcNow.AddDays(-5) },
            new MemoryItem { Id = Guid.NewGuid(), Text = "My coffee order is an espresso", Category = "Preference", CreatedAt = DateTimeOffset.UtcNow });
        await fixture.Database.SaveChangesAsync();

        var result = await new MemoryRetrievalService(fixture.Database).FindRelevantAsync("Plan an Alps hiking trip", 1);

        Assert.Single(result);
        Assert.Contains("hiking", result[0].Text);
    }

    [Fact]
    public async Task ClassicalOptimizerUsesDueDateThenOwnerPriority()
    {
        await using var fixture = await IntelligenceFixture.CreateAsync();
        fixture.Database.Tasks.AddRange(
            new TaskItem { Id = Guid.NewGuid(), Title = "Later urgent", Priority = 1, DueAt = DateTimeOffset.UtcNow.AddDays(3), CreatedAt = DateTimeOffset.UtcNow },
            new TaskItem { Id = Guid.NewGuid(), Title = "Due first", Priority = 3, DueAt = DateTimeOffset.UtcNow.AddHours(2), CreatedAt = DateTimeOffset.UtcNow },
            new TaskItem { Id = Guid.NewGuid(), Title = "No date", Priority = 1, CreatedAt = DateTimeOffset.UtcNow });
        await fixture.Database.SaveChangesAsync();

        var result = await new ClassicalTaskOptimizationEngine(fixture.Database).RankAsync();

        Assert.Equal(new[] { "Due first", "Later urgent", "No date" }, result.Select(item => item.Title));
    }

    [Fact]
    public async Task SavedProviderCredentialIsEncryptedAtRest()
    {
        await using var fixture = await IntelligenceFixture.CreateAsync();
        var options = Options.Create(new ModelOptions());
        var service = new AiConfigurationService(fixture.Database, options);

        var status = await service.ConfigureAsync(new ConfigureAiRequest("sk-test-secret-value-that-is-long-enough", "gpt-5.6-sol"));

        Assert.True(status.Configured);
        await using var command = fixture.Connection.CreateCommand();
        command.CommandText = "SELECT Secret FROM ProviderCredentials LIMIT 1";
        var raw = Assert.IsType<string>(await command.ExecuteScalarAsync());
        Assert.StartsWith(PersonalDataProtector.Prefix, raw);
        Assert.DoesNotContain("test-secret", raw);
    }

    private sealed class IntelligenceFixture : IAsyncDisposable
    {
        private IntelligenceFixture(SqliteConnection connection, VinanDbContext database)
        {
            Connection = connection;
            Database = database;
        }

        public SqliteConnection Connection { get; }
        public VinanDbContext Database { get; }

        public static async Task<IntelligenceFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<VinanDbContext>().UseSqlite(connection).Options;
            var database = new VinanDbContext(options, new PersonalDataProtector(new EphemeralDataProtectionProvider()));
            await database.Database.EnsureCreatedAsync();
            return new IntelligenceFixture(connection, database);
        }

        public async ValueTask DisposeAsync()
        {
            await Database.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }
}
