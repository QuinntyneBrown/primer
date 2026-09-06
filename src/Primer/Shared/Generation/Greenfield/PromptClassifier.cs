using System.Text;

namespace Primer.Shared.Generation.Greenfield;

/// <summary>
/// Decides which archetype a description asks for. Returning null is a real answer: it
/// means the description settles nothing, and the caller shall ask rather than guess.
/// </summary>
internal interface IPromptClassifier
{
    SolutionArchetype? Classify(string description);
}

/// <summary>
/// Matches a fixed table of signal phrases. Deterministic and offline by construction: the
/// same description always yields the same archetype, and no service is consulted.
///
/// A model reading the description for meaning would classify far more of them, and this
/// interface is where that implementation goes. Until then a description the table cannot
/// settle is refused rather than guessed at, so nobody is told the wrong shape quietly.
/// </summary>
internal sealed class KeywordPromptClassifier : IPromptClassifier
{
    /// <summary>Phrases that indicate something rendered in a browser.</summary>
    private static readonly string[] FrontEndSignals =
    [
        "web app", "webapp", "web application", "web client", "website", "web site",
        "browser", "frontend", "front end", "single page", "spa",
        "angular", "react", "vue", "svelte", "blazor",
        "user interface", "web portal", "web ui",
    ];

    /// <summary>Phrases that indicate something served over the network.</summary>
    private static readonly string[] BackEndSignals =
    [
        "api", "backend", "back end", "server", "service", "rest", "endpoint",
        "database", "dotnet", "asp net", "web api",
    ];

    /// <summary>Phrases that indicate a tool run from a shell.</summary>
    private static readonly string[] CommandLineSignals =
    [
        "cli", "command line", "commandline", "console app", "console application",
        "terminal", "shell", "dotnet tool", "global tool",
    ];

    public SolutionArchetype? Classify(string description)
    {
        ArgumentNullException.ThrowIfNull(description);

        var normalized = Normalize(description);

        var frontEnd = Matches(normalized, FrontEndSignals);
        var backEnd = Matches(normalized, BackEndSignals);
        var commandLine = Matches(normalized, CommandLineSignals);

        // A browser front end with something serving it is a web application, whatever
        // else it also ships. A command-line tool inside one is a project under
        // backend/src, not a different shape of solution.
        if (frontEnd && backEnd)
        {
            return SolutionArchetype.WebApplication;
        }

        // A front end alone says nothing about what serves it, so it settles nothing.
        if (commandLine && !frontEnd)
        {
            return SolutionArchetype.CommandLineTool;
        }

        return null;
    }

    /// <summary>
    /// Reduces the description to lowercase words separated by single spaces, padded at
    /// both ends. Matching a padded phrase against this is what keeps "api" from matching
    /// inside "rapid", without needing a word-boundary expression per signal.
    /// </summary>
    private static string Normalize(string description)
    {
        var builder = new StringBuilder(description.Length + 2);
        builder.Append(' ');

        var lastWasSpace = true;

        foreach (var character in description)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                lastWasSpace = false;
            }
            else if (!lastWasSpace)
            {
                builder.Append(' ');
                lastWasSpace = true;
            }
        }

        if (!lastWasSpace)
        {
            builder.Append(' ');
        }

        return builder.ToString();
    }

    private static bool Matches(string normalized, string[] signals) =>
        signals.Any(signal => normalized.Contains($" {signal} ", StringComparison.Ordinal));
}
