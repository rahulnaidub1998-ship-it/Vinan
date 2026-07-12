using Vinan.Api.Services;

namespace Vinan.Api.Tests;

public sealed class IntentParserTests
{
    [Fact]
    public void ParsesApprovedMemory()
    {
        var parsed = IntentParser.TryParseMemory("VINAN, remember that I prefer concise answers.", out var text);

        Assert.True(parsed);
        Assert.Equal("I prefer concise answers", text);
    }

    [Fact]
    public void ParsesReminderAndSchedule()
    {
        var parsed = IntentParser.TryParseReminder("Create a reminder for tomorrow call Mom", out var title, out var when);

        Assert.True(parsed);
        Assert.Equal("call Mom", title);
        Assert.Equal("Tomorrow", when);
    }

    [Fact]
    public void CalculatesRestrictedArithmeticExpression()
    {
        var parsed = IntentParser.TryCalculate("calculate 42 * 12", out var expression, out var result);

        Assert.True(parsed);
        Assert.Equal("42 * 12", expression);
        Assert.Equal("504", result);
    }

    [Fact]
    public void ParsesNotesTasksAndWeather()
    {
        Assert.True(IntentParser.TryParseNote("Write down that passport renewal is important", out var note));
        Assert.Equal("passport renewal is important", note);

        Assert.True(IntentParser.TryParseTask("Create an urgent task tomorrow renew passport", out var task, out var dueAt, out var priority));
        Assert.Equal("renew passport", task);
        Assert.NotNull(dueAt);
        Assert.Equal(1, priority);

        Assert.True(IntentParser.TryParseWeatherLocation("What is the weather in San Diego?", out var location));
        Assert.Equal("San Diego", location);
    }
}
