using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Vinan.Api.Data;
using Vinan.Api.Models;
using Vinan.Api.Services;
using Vinan.Api.Security;

namespace Vinan.Api.Tests;

public sealed class ConversationServiceTests
{
    [Fact]
    public async Task PersistsMemoryAndConversationMessages()
    {
        await using var fixture = await ConversationFixture.CreateAsync();

        var response = await fixture.Service.HandleAsync(new ConversationRequest("Remember that I prefer concise answers"));

        Assert.NotNull(response.Memory);
        Assert.Equal(1, await fixture.Database.Memories.CountAsync());
        Assert.Equal(2, await fixture.Database.ConversationMessages.CountAsync());
        Assert.NotEqual(Guid.Empty, response.ConversationId);
    }

    [Fact]
    public async Task HighRiskActionNeverReachesTheModel()
    {
        await using var fixture = await ConversationFixture.CreateAsync();

        var response = await fixture.Service.HandleAsync(new ConversationRequest("Deploy the production API"));

        Assert.Equal(RiskLevel.Level4, response.PendingAction?.RiskLevel);
        Assert.Equal(0, fixture.Model.CallCount);
        Assert.Equal(1, await fixture.Database.PendingActions.CountAsync());
    }

    [Fact]
    public async Task GeneralConversationUsesConfiguredModelBoundary()
    {
        await using var fixture = await ConversationFixture.CreateAsync();

        var response = await fixture.Service.HandleAsync(new ConversationRequest("Help me plan my week"));

        Assert.Equal("Model response", response.Reply);
        Assert.Equal("Test model", response.Provider);
        Assert.Equal(1, fixture.Model.CallCount);
    }

    [Fact]
    public async Task CarriesPriorConversationTurnsIntoTheModel()
    {
        await using var fixture = await ConversationFixture.CreateAsync();
        var first = await fixture.Service.HandleAsync(new ConversationRequest("Help me plan my week"));

        await fixture.Service.HandleAsync(new ConversationRequest("What did I just ask?", first.ConversationId));

        Assert.Equal(2, fixture.Model.LastHistory.Count);
        Assert.Equal("Help me plan my week", fixture.Model.LastHistory.First().Text);
        Assert.Equal("Model response", fixture.Model.LastHistory.Last().Text);
    }

    [Fact]
    public async Task StreamsAndPersistsAssistantResponse()
    {
        await using var fixture = await ConversationFixture.CreateAsync();
        var events = new List<ConversationStreamEvent>();

        await foreach (var item in fixture.Service.HandleStreamAsync(new ConversationRequest("Think through this")))
        {
            events.Add(item);
        }

        Assert.Equal("Model response", string.Concat(events.Where(item => item.Type == "delta").Select(item => item.Delta)));
        Assert.Equal("Model response", events.Single(item => item.Type == "completed").Response?.Reply);
        Assert.Equal(2, await fixture.Database.ConversationMessages.CountAsync());
    }

    [Fact]
    public async Task NaturalLanguageCreatesEncryptedNoteAndTaskRecords()
    {
        await using var fixture = await ConversationFixture.CreateAsync();

        var note = await fixture.Service.HandleAsync(new ConversationRequest("Note that the renewal code is private"));
        var task = await fixture.Service.HandleAsync(new ConversationRequest("Create an urgent task tomorrow renew passport"));

        Assert.Equal("the renewal code is private", note.Note?.Text);
        Assert.Equal("renew passport", task.Task?.Title);
        Assert.Equal(1, task.Task?.Priority);
        Assert.Single(await fixture.Database.Notes.ToListAsync());
        Assert.Single(await fixture.Database.Tasks.ToListAsync());
        Assert.Equal(0, fixture.Model.CallCount);
    }

    private sealed class ConversationFixture : IAsyncDisposable
    {
        private ConversationFixture(
            SqliteConnection connection,
            VinanDbContext database,
            FakeAssistantModel model,
            ConversationService service)
        {
            Connection = connection;
            Database = database;
            Model = model;
            Service = service;
        }

        public SqliteConnection Connection { get; }
        public VinanDbContext Database { get; }
        public FakeAssistantModel Model { get; }
        public ConversationService Service { get; }

        public static async Task<ConversationFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<VinanDbContext>()
                .UseSqlite(connection)
                .Options;
            var database = new VinanDbContext(options, new PersonalDataProtector(new EphemeralDataProtectionProvider()));
            await database.Database.EnsureCreatedAsync();
            var model = new FakeAssistantModel();
            var service = new ConversationService(
                database,
                model,
                new AuditService(database),
                new MemoryRetrievalService(database),
                new WeatherService(new HttpClient()),
                new ClassicalTaskOptimizationEngine(database));
            return new ConversationFixture(connection, database, model, service);
        }

        public async ValueTask DisposeAsync()
        {
            await Database.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }

    private sealed class FakeAssistantModel : IAssistantModel
    {
        public int CallCount { get; private set; }
        public IReadOnlyCollection<ConversationTurn> LastHistory { get; private set; } = Array.Empty<ConversationTurn>();

        public Task<ModelReply> GenerateAsync(
            string message,
            IReadOnlyCollection<MemoryItem> memories,
            IReadOnlyCollection<ConversationTurn> history,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LastHistory = history;
            return Task.FromResult(new ModelReply("Model response", "Test model"));
        }

        public async IAsyncEnumerable<ModelStreamChunk> StreamAsync(
            string message,
            IReadOnlyCollection<MemoryItem> memories,
            IReadOnlyCollection<ConversationTurn> history,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            CallCount++;
            LastHistory = history;
            await Task.CompletedTask;
            yield return new ModelStreamChunk("Model ", "Test model");
            yield return new ModelStreamChunk("response", "Test model");
        }
    }
}
