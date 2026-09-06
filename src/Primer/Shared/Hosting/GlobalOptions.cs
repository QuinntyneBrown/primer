using System.CommandLine;

namespace Primer.Shared.Hosting;

/// <summary>
/// Options accepted by every command with identical semantics. Each is defined once and
/// attached to the root as a recursive option, so no command can drift into a different
/// spelling or meaning.
/// </summary>
internal static class GlobalOptions
{
    internal static Option<OutputFormat> Format { get; } = new("--format")
    {
        Description = "Render results as human-oriented text or as a single JSON document.",
        Recursive = true,
        DefaultValueFactory = _ => OutputFormat.Text,
    };

    /// <summary>
    /// Reports the build version. Primer defines this rather than using the built-in
    /// version option, which refuses to be combined with any other option and so cannot
    /// answer <c>--version --format json</c>.
    /// </summary>
    internal static Option<bool> Version { get; } = new("--version")
    {
        Description = "Show version information.",
    };

    /// <summary>Every recursive global option, in the order help should list them.</summary>
    internal static IReadOnlyList<Option> All { get; } = [Format];
}
