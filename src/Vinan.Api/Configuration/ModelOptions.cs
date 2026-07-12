namespace Vinan.Api.Configuration;

public sealed class ModelOptions
{
    public const string SectionName = "Models";

    public string Provider { get; set; } = "Auto";
    public string Model { get; set; } = "gpt-5.6-sol";
    public string? ApiKey { get; set; }
    public string ReasoningEffort { get; set; } = "medium";
    public string Verbosity { get; set; } = "medium";
    public bool WebSearchEnabled { get; set; } = true;
}
