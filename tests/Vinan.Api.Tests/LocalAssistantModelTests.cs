using Vinan.Api.Models;
using Vinan.Api.Services;

namespace Vinan.Api.Tests;

public sealed class LocalAssistantModelTests
{
    private readonly LocalAssistantModel _model = new();

    [Theory]
    [InlineData("hi")]
    [InlineData("Hello VINAN!")]
    [InlineData("hey")]
    public async Task GreetsNaturallyWithoutProviderWarning(string message)
    {
        var reply = await _model.GenerateAsync(message, Array.Empty<MemoryItem>(), Array.Empty<ConversationTurn>(), default);

        Assert.StartsWith("Hi.", reply.Text);
        Assert.DoesNotContain("not connected", reply.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RecallsPriorUserTurnLocally()
    {
        var history = new[]
        {
            new ConversationTurn("user", "Plan my passport renewal"),
            new ConversationTurn("assistant", "Let us begin"),
        };

        var reply = await _model.GenerateAsync("What did I just ask?", Array.Empty<MemoryItem>(), history, default);

        Assert.Contains("Plan my passport renewal", reply.Text);
    }

    [Fact]
    public async Task AdvancedRequestExplainsConnectionRequirement()
    {
        var reply = await _model.GenerateAsync(
            "Analyze this architecture",
            Array.Empty<MemoryItem>(),
            Array.Empty<ConversationTurn>(),
            default);

        Assert.Contains("advanced reasoning provider", reply.Text);
        Assert.Contains("Intelligence", reply.Text);
    }
}
