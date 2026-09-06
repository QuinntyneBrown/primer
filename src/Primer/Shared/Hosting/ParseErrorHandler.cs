using System.CommandLine;
using System.CommandLine.Parsing;

namespace Primer.Shared.Hosting;

/// <summary>
/// Renders a parse failure. A mistyped token is answered with the nearest thing the tool
/// does understand, because the reader almost always meant that.
/// </summary>
internal static class ParseErrorHandler
{
    /// <summary>How far a token may be from a known name and still be suggested.</summary>
    private const int MaxSuggestionDistance = 3;

    internal static ExitCode Render(ParseResult parseResult, InvocationConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(parseResult);
        ArgumentNullException.ThrowIfNull(configuration);

        var known = KnownNames(parseResult.RootCommandResult.Command).ToList();

        foreach (var error in parseResult.Errors)
        {
            configuration.Error.WriteLine(error.Message);
        }

        foreach (var token in parseResult.UnmatchedTokens)
        {
            configuration.Error.WriteLine($"Unrecognised: '{token}'.");

            if (Nearest(token, known) is { } suggestion)
            {
                configuration.Error.WriteLine($"Did you mean '{suggestion}'?");
            }
        }

        return ExitCode.Usage;
    }

    /// <summary>Every command and option name the tool answers to.</summary>
    private static IEnumerable<string> KnownNames(Command command)
    {
        foreach (var subcommand in command.Subcommands)
        {
            yield return subcommand.Name;

            foreach (var nested in KnownNames(subcommand))
            {
                yield return nested;
            }
        }

        foreach (var option in command.Options)
        {
            yield return option.Name;
        }
    }

    private static string? Nearest(string token, IReadOnlyList<string> known)
    {
        var best = int.MaxValue;
        string? match = null;

        foreach (var candidate in known)
        {
            var distance = Distance(token, candidate);

            if (distance < best)
            {
                best = distance;
                match = candidate;
            }
        }

        return best <= MaxSuggestionDistance ? match : null;
    }

    /// <summary>Levenshtein distance, which is what "nearest" means for a mistyped name.</summary>
    private static int Distance(string left, string right)
    {
        var previous = new int[right.Length + 1];
        var current = new int[right.Length + 1];

        for (var index = 0; index <= right.Length; index++)
        {
            previous[index] = index;
        }

        for (var i = 1; i <= left.Length; i++)
        {
            current[0] = i;

            for (var j = 1; j <= right.Length; j++)
            {
                var substitution = previous[j - 1] + (left[i - 1] == right[j - 1] ? 0 : 1);
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), substitution);
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length];
    }
}
