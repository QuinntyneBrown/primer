using System.Text;
using System.Text.RegularExpressions;
using Primer.Shared.Hosting;

namespace Primer.Shared.Analysis;

/// <summary>Decides whether a repository-relative path is excluded from analysis.</summary>
internal interface IIgnoreMatcher
{
    bool IsIgnored(string relativePath);
}

/// <summary>
/// Evaluates the repository's own ignore rules, so a path the repository excludes is
/// neither listed nor read. Supports the subset that decides real exclusions: comments,
/// anchors, directory-only patterns, negation, and glob wildcards.
/// </summary>
internal sealed class GitIgnoreMatcher : IIgnoreMatcher
{
    private readonly List<(Regex Pattern, bool Negated)> _rules = [];

    public GitIgnoreMatcher(RepositoryLocation location)
    {
        ArgumentNullException.ThrowIfNull(location);

        var ignoreFile = Path.Combine(location.Path, ".gitignore");

        if (!File.Exists(ignoreFile))
        {
            return;
        }

        foreach (var raw in File.ReadLines(ignoreFile))
        {
            var line = raw.Trim();

            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var negated = line.StartsWith('!');

            if (negated)
            {
                line = line[1..];
            }

            _rules.Add((new Regex(ToRegex(line), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), negated));
        }
    }

    public bool IsIgnored(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);

        var path = relativePath.Replace('\\', '/').TrimStart('/');
        var ignored = false;

        // Later rules win, which is how a negation re-includes a path an earlier rule excluded.
        foreach (var (pattern, negated) in _rules)
        {
            if (pattern.IsMatch(path))
            {
                ignored = !negated;
            }
        }

        return ignored;
    }

    private static string ToRegex(string pattern)
    {
        var directoryOnly = pattern.EndsWith('/');
        var body = directoryOnly ? pattern[..^1] : pattern;
        var anchored = body.StartsWith('/');

        if (anchored)
        {
            body = body[1..];
        }

        var builder = new StringBuilder();
        builder.Append(anchored || body.Contains('/', StringComparison.Ordinal) ? "^" : "^(?:.*/)?");

        for (var index = 0; index < body.Length; index++)
        {
            var character = body[index];

            if (character == '*' && index + 1 < body.Length && body[index + 1] == '*')
            {
                builder.Append(".*");
                index++;
            }
            else if (character == '*')
            {
                builder.Append("[^/]*");
            }
            else if (character == '?')
            {
                builder.Append("[^/]");
            }
            else
            {
                builder.Append(Regex.Escape(character.ToString()));
            }
        }

        // A directory pattern excludes the directory and everything beneath it.
        builder.Append(directoryOnly ? "(?:/.*)?$" : "(?:/.*)?$");

        return builder.ToString();
    }
}
