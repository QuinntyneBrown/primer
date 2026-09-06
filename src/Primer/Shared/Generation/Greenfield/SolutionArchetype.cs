using Primer.Shared.Hosting;
using Primer.Shared.Presentation;

namespace Primer.Shared.Generation.Greenfield;

/// <summary>
/// The shape of solution a description shall be built into. The archetype decides the
/// folder outline and the conventions emitted, so exactly one is chosen per run.
/// </summary>
internal enum SolutionArchetype
{
    /// <summary>A served back end and a browser front end.</summary>
    WebApplication,

    /// <summary>A tool that runs on a machine, with no browser front end.</summary>
    CommandLineTool,
}

/// <summary>Raised when a value names no supported archetype.</summary>
internal sealed class UnsupportedArchetypeException(string value)
    : PrimerException(new PrimerError(
        What: "That archetype is not supported",
        Subject: value,
        NextAction: $"Choose one of: {string.Join(", ", ArchetypeNames.Supported)}.",
        ExitCode: ExitCode.Usage));

/// <summary>
/// Raised when the description determines no archetype. Guessing here would silently
/// produce the wrong folder structure, which the reader has no way to detect, so the run
/// stops and asks instead.
/// </summary>
internal sealed class UndecidableDescriptionException()
    : PrimerException(new PrimerError(
        What: "The description does not determine a solution archetype",
        Subject: "the supplied description",
        NextAction: $"Re-run with --archetype naming one of: {string.Join(", ", ArchetypeNames.Supported)}.",
        ExitCode: ExitCode.Usage));

/// <summary>The names the archetypes are chosen by on the command line.</summary>
internal static class ArchetypeNames
{
    internal const string Web = "web";

    internal const string Cli = "cli";

    internal static IReadOnlyList<string> Supported { get; } = [Web, Cli];

    internal static SolutionArchetype Resolve(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return value.Trim().ToLowerInvariant() switch
        {
            Web => SolutionArchetype.WebApplication,
            Cli => SolutionArchetype.CommandLineTool,
            _ => throw new UnsupportedArchetypeException(value),
        };
    }
}
