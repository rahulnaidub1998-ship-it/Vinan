using Vinan.Api.Models;

namespace Vinan.Api.Services;

public sealed class ToolRegistryService
{
    private readonly AiConfigurationService _ai;

    public ToolRegistryService(AiConfigurationService ai)
    {
        _ai = ai;
    }

    public async Task<IReadOnlyList<ToolCapability>> GetCapabilitiesAsync(CancellationToken cancellationToken = default)
    {
        var ai = await _ai.GetStatusAsync(cancellationToken);
        return new List<ToolCapability>
        {
            Ready("memory", "Adaptive Memory", "Intelligence", "Execute", "Stores approved facts and ranks relevant context.", "VINAN vector retrieval"),
            Ready("notes", "Private Notes", "Productivity", "Execute", "Creates, lists, and deletes encrypted notes.", "VINAN local"),
            Ready("tasks", "Task Manager", "Productivity", "Execute", "Creates, prioritizes, and completes encrypted tasks.", "VINAN local"),
            Ready("reminders", "Reminders", "Productivity", "Execute", "Creates and completes persistent reminders.", "VINAN local"),
            Ready("weather", "Live Weather", "Information", "Read", "Reads current conditions and a three-day forecast.", "Open-Meteo"),
            Ready("calculator", "Calculator", "Utility", "Execute", "Evaluates restricted arithmetic expressions locally.", "VINAN local"),
            Ready("clock", "Date and Time", "Utility", "Read", "Reads the current local date and time.", "VINAN local"),
            new ToolCapability(
                "web-search",
                "Web Search",
                "Information",
                "Read",
                ai.WebSearchEnabled ? "Ready" : "Needs AI connection",
                "Searches current public information when the model decides it is needed.",
                ai.WebSearchEnabled ? ai.Model : "OpenAI Responses API"),
            Ready("task-optimizer", "Task Optimizer", "Optimization", "Read", "Ranks current tasks by due date and owner priority.", "Classical exact engine"),
            new ToolCapability(
                "quantum-optimizer",
                "Quantum Optimizer",
                "Research",
                "Restricted",
                "Needs Azure Quantum workspace",
                "Reserved for suitable optimization experiments after classical benchmarking.",
                "Azure Quantum / Q#"),
        };
    }

    private static ToolCapability Ready(
        string id,
        string name,
        string category,
        string permission,
        string description,
        string provider) => new(id, name, category, permission, "Ready", description, provider);
}
