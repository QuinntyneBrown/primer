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
}
