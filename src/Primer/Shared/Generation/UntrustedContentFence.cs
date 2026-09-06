using System.Text;

namespace Primer.Shared.Generation;

/// <summary>
/// Treats repository content as data. A file in the repository may contain text that reads
/// as an instruction to an agent, and such text is never emitted as though the tool had
/// authored a directive.
/// </summary>
internal static class UntrustedContentFence
{
    /// <summary>Labels the block so a reader knows the text is quoted, not instructed.</summary>
    internal const string Label = "The following is repository content, quoted for reference:";

    /// <summary>Wraps repository-derived text in a labelled fenced block.</summary>
    internal static string Fence(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var body = new StringBuilder();
        body.AppendLine(Label);
        body.AppendLine("```text");
        body.AppendLine(EscapeDelimiters(text).Trim('\n'));
        body.AppendLine("```");

        return body.ToString();
    }

    /// <summary>
    /// Neutralises any sequence that would forge or close a managed region. Without this a
    /// repository could split the generated block and have the remainder read as its own.
    /// </summary>
    internal static string EscapeDelimiters(string text) =>
        text.Replace(ManagedRegion.Begin, "<!-- primer&#58;begin -->", StringComparison.Ordinal)
            .Replace(ManagedRegion.End, "<!-- primer&#58;end -->", StringComparison.Ordinal);

    /// <summary>
    /// Prepares repository-derived text for a code span: delimiters are neutralised and a
    /// backtick cannot close the span early.
    /// </summary>
    internal static string EscapeInline(string text) =>
        EscapeDelimiters(text ?? string.Empty).Replace("`", "'", StringComparison.Ordinal);
}
