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
}
