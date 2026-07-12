using System.Net;
using System.Text;
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

public sealed class OpenAiAssistantModelTests
{
    [Fact]
    public async Task StreamsTextWithConversationMemoryReasoningAndWebSearch()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var databaseOptions = new DbContextOptionsBuilder<VinanDbContext>().UseSqlite(connection).Options;
        await using var database = new VinanDbContext(databaseOptions, new PersonalDataProtector(new EphemeralDataProtectionProvider()));
        await database.Database.EnsureCreatedAsync();
        var modelOptions = Options.Create(new ModelOptions());
        var configuration = new AiConfigurationService(database, modelOptions);
        await configuration.ConfigureAsync(new ConfigureAiRequest("sk-test-streaming-key-that-is-valid-length", "gpt-5.6-sol"));
        var handler = new StreamingHandler();
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/") };
        var model = new OpenAiAssistantModel(client, configuration);
        var chunks = new List<ModelStreamChunk>();

        await foreach (var chunk in model.StreamAsync(
            "What next?",
            new[] { new MemoryItem { Text = "I prefer concise answers", Category = "Preference" } },
            new[] { new ConversationTurn("user", "Plan my week"), new ConversationTurn("assistant", "Let us begin") },
            default))
        {
            chunks.Add(chunk);
        }

        Assert.Equal("Hello VINAN", string.Concat(chunks.Select(chunk => chunk.Text)));
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Contains("Plan my week", handler.RequestBody);
        Assert.Contains("I prefer concise answers", handler.RequestBody);
        Assert.Contains("web_search", handler.RequestBody);
        Assert.Contains("medium", handler.RequestBody);
        Assert.Contains("\"stream\":true", handler.RequestBody);
    }

    private sealed class StreamingHandler : HttpMessageHandler
    {
        public string? AuthorizationScheme { get; private set; }
        public string RequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            var events = "data: {\"type\":\"response.output_text.delta\",\"delta\":\"Hello \"}\n\n"
                + "data: {\"type\":\"response.output_text.delta\",\"delta\":\"VINAN\"}\n\n"
                + "data: [DONE]\n\n";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(events, Encoding.UTF8, "text/event-stream"),
            };
        }
    }
}
