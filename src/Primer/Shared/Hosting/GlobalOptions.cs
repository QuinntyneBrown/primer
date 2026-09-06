using System.CommandLine;

namespace Primer.Shared.Hosting;

/// <summary>
/// Options accepted by every command with identical semantics. Each is defined once and
/// attached to the root as a recursive option, so no command can drift into a different
/// spelling or meaning.
/// </summary>
internal static class GlobalOptions
{
    internal static Option<DirectoryInfo> Path { get; } = new("--path")
    {
        Description = "The directory to operate on. Defaults to the current directory.",
        Recursive = true,
    };

    internal static Option<VerbosityLevel> Verbosity { get; } = new("--verbosity", "-v")
    {
        Description = "How much to say: quiet, minimal, normal, detailed, or diagnostic.",
        Recursive = true,
        DefaultValueFactory = _ => VerbosityLevel.Normal,
    };

    internal static Option<OutputFormat> Format { get; } = new("--format")
    {
        Description = "Render results as human-oriented text or as a single JSON document.",
        Recursive = true,
        DefaultValueFactory = _ => OutputFormat.Text,
    };

    internal static Option<bool> NoColor { get; } = new("--no-color")
    {
        Description = "Never emit colour, whatever the terminal supports.",
        Recursive = true,
    };

    internal static Option<bool> DryRun { get; } = new("--dry-run")
    {
        Description = "Report what would change without changing anything.",
        Recursive = true,
    };

    internal static Option<bool> Yes { get; } = new("--yes")
    {
        Description = "Approve changes without prompting. Required in a non-interactive session.",
        Recursive = true,
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
    internal static IReadOnlyList<Option> All { get; } =
        [Path, Verbosity, Format, NoColor, DryRun, Yes];
}

/// <summary>The global option values one invocation was given.</summary>
internal sealed record GlobalOptionValues(
    string TargetPath,
    VerbosityLevel Verbosity,
    OutputFormat Format,
    bool NoColor,
    bool DryRun,
    bool Yes)
{
    internal static GlobalOptionValues From(ParseResult parseResult)
    {
        ArgumentNullException.ThrowIfNull(parseResult);

        var path = parseResult.GetValue(GlobalOptions.Path);

        return new GlobalOptionValues(
            path?.FullName ?? Directory.GetCurrentDirectory(),
            parseResult.GetValue(GlobalOptions.Verbosity),
            parseResult.GetValue(GlobalOptions.Format),
            parseResult.GetValue(GlobalOptions.NoColor),
            parseResult.GetValue(GlobalOptions.DryRun),
            parseResult.GetValue(GlobalOptions.Yes));
    }
}
