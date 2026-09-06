namespace Primer.Shared.Presentation;

/// <summary>
/// Shortens a path to fit a width by removing interior segments. The leading and trailing
/// segments survive, because those are what identify a path to a reader.
/// </summary>
internal static class PathElider
{
    /// <summary>The character marking where interior segments were removed.</summary>
    internal const char Ellipsis = '\u2026';

    internal static string Elide(string path, int budget)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (path.Length <= budget)
        {
            return path;
        }

        if (budget <= 1)
        {
            return Ellipsis.ToString();
        }

        var available = budget - 1;
        var tail = FinalSegment(path);

        if (tail.Length > available)
        {
            tail = tail[^available..];
        }

        var headLength = available - tail.Length;
        var head = headLength > 0 ? path[..headLength] : string.Empty;

        return string.Concat(head, Ellipsis.ToString(), tail);
    }

    private static string FinalSegment(string path)
    {
        var separator = path.LastIndexOfAny(['/', '\\']);
        return separator >= 0 ? path[(separator + 1)..] : path;
    }
}
