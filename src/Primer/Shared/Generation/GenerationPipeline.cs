using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Primer.Shared.Analysis;
using Primer.Shared.Hosting;

namespace Primer.Shared.Generation;

/// <summary>Composes the ordered sections, dropping those with nothing behind them.</summary>
internal sealed class SectionComposer(IEnumerable<ISection> sections)
{
    internal string Render(TemplateTokenName token, RepositoryContext context)
    {
        var section = sections.FirstOrDefault(candidate => candidate.Token == token);

        if (section is null || !section.HasContent(context))
        {
            return string.Empty;
        }

        var body = section.RenderBody(context).Trim('\n');

        return body.Length == 0
            ? string.Empty
            : $"## {section.Heading}\n\n{body}\n";
    }
}

/// <summary>
/// Drops anything the repository cannot confirm. Guidance an agent cannot act on is worse
/// than absent guidance, because an agent cannot tell the difference.
/// </summary>
internal sealed class GroundingValidator(RepositoryLocation location)
{
    private static readonly string[] BareToolNames = ["dotnet", "npm", "pnpm", "yarn", "go", "python", "cargo"];

    internal string Validate(string draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var kept = new List<string>();

        foreach (var line in draft.Split('\n'))
        {
            if (NamesMissingPath(line) || IsBareToolName(line))
            {
                continue;
            }

            kept.Add(line);
        }

        return string.Join('\n', kept);
    }

    private bool NamesMissingPath(string line)
    {
        foreach (var span in CodeSpans(line))
        {
            var candidate = span.Trim('/', ' ');

            if (!candidate.Contains('/', StringComparison.Ordinal) || candidate.Contains(' ', StringComparison.Ordinal))
            {
                continue;
            }

            var absolute = Path.Combine(location.Path, candidate);

            if (!File.Exists(absolute) && !Directory.Exists(absolute))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsBareToolName(string line)
    {
        foreach (var span in CodeSpans(line))
        {
            if (BareToolNames.Contains(span.Trim(), StringComparer.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<string> CodeSpans(string line)
    {
        var parts = line.Split('`');

        // Odd indices are the spans between backticks.
        for (var index = 1; index < parts.Length; index += 2)
        {
            yield return parts[index];
        }
    }
}

/// <summary>
/// Holds the generated document to a length an agent reads in full. Guidance an agent
/// truncates is guidance that does not apply.
/// </summary>
internal static class LineBudget
{
    /// <summary>The ceiling established practice sets for agent guidance.</summary>
    internal const int MaxLines = 150;

    /// <summary>Lines the managed region wrapper occupies.</summary>
    internal const int ReservedForRegion = 4;

    internal static string Apply(string body)
    {
        ArgumentNullException.ThrowIfNull(body);

        var lines = body.Split('\n');
        var ceiling = MaxLines - ReservedForRegion;

        if (lines.Length <= ceiling)
        {
            return body;
        }

        var note = "_Content was truncated to stay within the "
            + MaxLines.ToString(CultureInfo.InvariantCulture)
            + "-line guidance; the repository itself is the fuller source._";

        return string.Join('\n', lines.Take(ceiling - 2)) + "\n\n" + note;
    }
}

/// <summary>
/// A stable fingerprint of the facts a document was generated from. Two runs over an
/// unchanged repository produce the same hash, which is what makes regeneration idempotent
/// and drift detectable.
/// </summary>
internal static class ContentHash
{
    internal static string Compute(RepositoryContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var canonical = new StringBuilder();

        foreach (var stack in context.Stacks)
        {
            canonical.Append(stack.Name).Append('|').Append(string.Join(',', stack.MarkerFiles))
                .Append('|').Append(stack.PackageManager).Append('|').Append(stack.TestFramework).Append('\n');
        }

        foreach (var command in context.Commands)
        {
            canonical.Append(command.Role).Append('|').Append(command.Invocation).Append('\n');
        }

        foreach (var directory in context.Structure.Directories)
        {
            canonical.Append(directory).Append('\n');
        }

        foreach (var convention in context.Conventions)
        {
            canonical.Append(convention.Kind).Append('|').Append(convention.RelativePath).Append('\n');
        }

        return Digest(canonical.ToString());
    }

    /// <summary>
    /// The fingerprint of a greenfield run. Its inputs are the description and the chosen
    /// archetype, which is what makes the same description regenerate byte-identically.
    /// </summary>
    internal static string Compute(string prompt, Greenfield.SolutionArchetype archetype)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        return Digest($"{archetype}|{prompt}");
    }

    private static string Digest(string canonical) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))[..12];
}

/// <summary>
/// Identifies the independent projects a repository holds. A repository with one project
/// needs no nested guidance; a repository with several gains a file scoped to each.
/// </summary>
internal sealed class NestedProjectPlanner(RepositoryLocation location)
{
    private static readonly string[] ProjectMarkers =
        ["package.json", "*.csproj", "*.fsproj", "go.mod", "pyproject.toml"];

    private const int MaxDepth = 3;

    internal IReadOnlyList<string> Plan()
    {
        var directories = new HashSet<string>(StringComparer.Ordinal);

        foreach (var marker in ProjectMarkers)
        {
            foreach (var path in Enumerate(marker))
            {
                var directory = Path.GetDirectoryName(path);

                if (directory is null)
                {
                    continue;
                }

                var relative = Path.GetRelativePath(location.Path, directory).Replace('\\', '/');

                if (relative is "." or "" || relative.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
                    || relative.Contains("/obj/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                directories.Add(relative);
            }
        }

        // One project is the repository; nesting adds nothing a root file does not say.
        return directories.Count > 1
            ? [.. directories.OrderBy(path => path, StringComparer.Ordinal)]
            : [];
    }

    private List<string> Enumerate(string pattern)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            MaxRecursionDepth = MaxDepth,
            IgnoreInaccessible = true,
        };

        try
        {
            return Directory.EnumerateFiles(location.Path, pattern, options).ToList();
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}
