using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Vinan.Api.Services;

public static partial class IntentParser
{
    public static bool TryParseMemory(string message, out string text)
    {
        var match = MemoryIntent().Match(message.Trim());
        text = match.Success ? match.Groups["text"].Value.Trim(' ', ':', '.', ',') : string.Empty;
        return !string.IsNullOrWhiteSpace(text);
    }

    public static bool TryParseReminder(string message, out string title, out string when)
    {
        var match = ReminderIntent().Match(message.Trim());
        if (!match.Success)
        {
            title = string.Empty;
            when = string.Empty;
            return false;
        }

        title = match.Groups["text"].Value.Trim(' ', ':', '.', ',');
        when = Tomorrow().IsMatch(message)
            ? "Tomorrow"
            : Today().IsMatch(message)
                ? "Today"
                : Tonight().IsMatch(message)
                    ? "Tonight"
                    : "Scheduled";

        title = LeadingDate().Replace(title, string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            title = "Reminder";
        }

        return true;
    }

    public static bool IsMemoryReview(string message)
    {
        return MemoryReview().IsMatch(message.Trim());
    }

    public static bool IsDateOrTimeRequest(string message)
    {
        return DateOrTime().IsMatch(message.Trim());
    }

    public static bool TryParseNote(string message, out string text)
    {
        var match = NoteIntent().Match(message.Trim());
        text = match.Success ? match.Groups["text"].Value.Trim(' ', ':', '.', ',') : string.Empty;
        return !string.IsNullOrWhiteSpace(text);
    }

    public static bool IsNoteReview(string message) => NoteReview().IsMatch(message.Trim());

    public static bool TryParseTask(string message, out string title, out DateTimeOffset? dueAt, out int priority)
    {
        var match = TaskIntent().Match(message.Trim());
        title = match.Success ? match.Groups["text"].Value.Trim(' ', ':', '.', ',') : string.Empty;
        dueAt = null;
        priority = HighPriority().IsMatch(message) ? 1 : LowPriority().IsMatch(message) ? 5 : 3;
        if (!match.Success || string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        if (Tomorrow().IsMatch(message))
        {
            dueAt = DateTimeOffset.Now.Date.AddDays(1).AddHours(9);
        }
        else if (Today().IsMatch(message) || Tonight().IsMatch(message))
        {
            dueAt = DateTimeOffset.Now.Date.AddHours(Tonight().IsMatch(message) ? 18 : 17);
        }

        title = TaskTiming().Replace(title, string.Empty).Trim();
        return !string.IsNullOrWhiteSpace(title);
    }

    public static bool IsTaskReview(string message) => TaskReview().IsMatch(message.Trim());

    public static bool IsTaskPrioritization(string message) => TaskPrioritization().IsMatch(message.Trim());

    public static bool IsWeatherRequest(string message) => WeatherRequest().IsMatch(message.Trim());

    public static bool TryParseWeatherLocation(string message, out string location)
    {
        var match = WeatherLocation().Match(message.Trim());
        location = match.Success ? match.Groups["location"].Value.Trim(' ', '?', '.', ',') : string.Empty;
        return !string.IsNullOrWhiteSpace(location);
    }

    public static bool TryCalculate(string message, out string expression, out string result)
    {
        var match = CalculatorIntent().Match(message.Trim());
        expression = match.Success ? match.Groups["expression"].Value.Trim() : string.Empty;
        result = string.Empty;
        if (!match.Success || !SafeExpression().IsMatch(expression))
        {
            return false;
        }

        try
        {
            var value = new DataTable().Compute(expression, string.Empty);
            result = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            return !string.IsNullOrWhiteSpace(result);
        }
        catch (Exception exception) when (exception is EvaluateException or SyntaxErrorException or InvalidCastException)
        {
            return false;
        }
    }

    [GeneratedRegex(@"^(?:vinan,?\s*)?(?:please\s+)?remember(?:\s+that)?\s+(?<text>.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex MemoryIntent();

    [GeneratedRegex(@"^(?:vinan,?\s*)?(?:(?:create|add|set)\s+)?(?:a\s+)?reminder(?:\s+(?:to|for))?\s*(?<text>.*)$", RegexOptions.IgnoreCase)]
    private static partial Regex ReminderIntent();

    [GeneratedRegex(@"^(?:vinan,?\s*)?(?:what do you (?:know|remember) about me|show (?:my )?memor(?:y|ies))\??$", RegexOptions.IgnoreCase)]
    private static partial Regex MemoryReview();

    [GeneratedRegex(@"\b(?:date|time)\b", RegexOptions.IgnoreCase)]
    private static partial Regex DateOrTime();

    [GeneratedRegex(@"^(?:vinan,?\s*)?(?:calculate|calc)\s+(?<expression>.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex CalculatorIntent();

    [GeneratedRegex(@"^[\d\s().+\-*/%]+$")]
    private static partial Regex SafeExpression();

    [GeneratedRegex(@"\btomorrow\b", RegexOptions.IgnoreCase)]
    private static partial Regex Tomorrow();

    [GeneratedRegex(@"\btoday\b", RegexOptions.IgnoreCase)]
    private static partial Regex Today();

    [GeneratedRegex(@"\btonight\b", RegexOptions.IgnoreCase)]
    private static partial Regex Tonight();

    [GeneratedRegex(@"^(?:today|tomorrow|tonight)\b\s*", RegexOptions.IgnoreCase)]
    private static partial Regex LeadingDate();

    [GeneratedRegex(@"^(?:vinan,?\s*)?(?:please\s+)?(?:take\s+(?:a\s+)?note|create\s+(?:a\s+)?note|note|write\s+down)(?:\s+that)?\s+(?<text>.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex NoteIntent();

    [GeneratedRegex(@"^(?:vinan,?\s*)?(?:show|list|read)(?:\s+my)?\s+notes\??$", RegexOptions.IgnoreCase)]
    private static partial Regex NoteReview();

    [GeneratedRegex(@"^(?:vinan,?\s*)?(?:please\s+)?(?:add|create|make)(?:\s+(?:a|an|new))?(?:\s+(?:urgent|high\s+priority|low\s+priority))?\s+task(?:\s+to)?\s+(?<text>.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex TaskIntent();

    [GeneratedRegex(@"^(?:vinan,?\s*)?(?:show|list|read)(?:\s+my)?\s+(?:open\s+)?tasks\??$", RegexOptions.IgnoreCase)]
    private static partial Regex TaskReview();

    [GeneratedRegex(@"\b(?:prioritize|rank|optimize)\b.*\btasks?\b|\btasks?\b.*\b(?:priority|order)\b", RegexOptions.IgnoreCase)]
    private static partial Regex TaskPrioritization();

    [GeneratedRegex(@"\b(?:weather|forecast|temperature)\b", RegexOptions.IgnoreCase)]
    private static partial Regex WeatherRequest();

    [GeneratedRegex(@"\b(?:weather|forecast|temperature)(?:\s+(?:today|tomorrow))?\s+(?:in|for|at)\s+(?<location>[^?]+)", RegexOptions.IgnoreCase)]
    private static partial Regex WeatherLocation();

    [GeneratedRegex(@"\b(?:high|urgent|important)\s+priority\b|\burgent\b", RegexOptions.IgnoreCase)]
    private static partial Regex HighPriority();

    [GeneratedRegex(@"\blow\s+priority\b", RegexOptions.IgnoreCase)]
    private static partial Regex LowPriority();

    [GeneratedRegex(@"\b(?:today|tomorrow|tonight|high\s+priority|low\s+priority|urgent)\b", RegexOptions.IgnoreCase)]
    private static partial Regex TaskTiming();
}
