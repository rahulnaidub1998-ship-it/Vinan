using System.Text.RegularExpressions;
using Vinan.Api.Models;

namespace Vinan.Api.Services;

public static partial class RiskClassifier
{
    public static RiskLevel Classify(string message)
    {
        var text = message.Trim();
        if (InformationalIntent().IsMatch(text))
        {
            return RiskLevel.Level1;
        }

        if (HighRiskAction().IsMatch(text))
        {
            return RiskLevel.Level4;
        }

        if (ExternalAction().IsMatch(text))
        {
            return RiskLevel.Level3;
        }

        return RiskLevel.Level1;
    }

    [GeneratedRegex(@"^(?:what|why|how|explain|describe|summarize|tell me about|can you explain)\b", RegexOptions.IgnoreCase)]
    private static partial Regex InformationalIntent();

    [GeneratedRegex(@"\b(?:transfer|wire|pay|buy|purchase|trade|sell|deploy(?:ment)?|unlock|shutdown|erase production|delete production)\b", RegexOptions.IgnoreCase)]
    private static partial Regex HighRiskAction();

    [GeneratedRegex(@"\b(?:send|email|message|post|publish|schedule|book|order|invite|create a meeting|add to calendar)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ExternalAction();
}
