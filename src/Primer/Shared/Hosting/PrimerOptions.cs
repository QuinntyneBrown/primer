namespace Primer.Shared.Hosting;

/// <summary>
/// Everything a run can be configured with, bound from the Primer configuration section.
/// </summary>
internal sealed class PrimerOptions
{
    /// <summary>The configuration section these options bind from.</summary>
    internal const string SectionName = "Primer";

    /// <summary>Caps bounding the cost of analysis.</summary>
    public AnalysisBudget Budget { get; set; } = new();

    /// <summary>Agent targets to generate instruction files for.</summary>
    public IList<string> Agents { get; set; } = [];

    /// <summary>Repository-relative directory searched for template overrides.</summary>
    public string TemplatePath { get; set; } = "templates";

    /// <summary>The MCP servers this repository depends on.</summary>
    public IList<McpRequirementOptions> McpRequirements { get; set; } = [];

    /// <summary>
    /// The agent clients whose registries are probed. Left empty here on purpose:
    /// configuration binds a collection by index and never clears what is already in it, so
    /// a declared list would merge with a seeded one instead of replacing it. The built-in
    /// clients are applied only when a repository declares none.
    /// </summary>
    public IList<McpClientOptions> McpClients { get; set; } = [];
}
