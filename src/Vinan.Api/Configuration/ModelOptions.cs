namespace Vinan.Api.Configuration;

public sealed class ModelOptions
{
    public const string SectionName = "Models";

    public string Provider { get; set; } = "Auto";
    public string Model { get; set; } = "gpt-5.6-terra";
    public string? ApiKey { get; set; }
}
