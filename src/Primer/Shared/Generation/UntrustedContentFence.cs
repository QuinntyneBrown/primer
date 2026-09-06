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
    internal static string Fence(string text) => Fence(text, Label);

    /// <summary>
    /// Wraps supplied text in a fenced block under a caller-chosen label. The label is what
    /// tells a reading agent the block is quoted data rather than an instruction, so text
    /// from a source other than the repository states its own provenance.
    /// </summary>
    internal static string Fence(string text, string label)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(label);

        var body = new StringBuilder();
        body.AppendLine(label);
        body.AppendLine("```text");
        body.AppendLine(text.Trim('\n'));
        body.AppendLine("```");

        return body.ToString();
    }

    /// <summary>
    /// Prepares repository-derived text for a code span, where a backtick would otherwise
    /// close the span early and let what follows read as guidance the tool authored.
    /// </summary>
    internal static string EscapeInline(string text) =>
        (text ?? string.Empty).Replace("`", "'", StringComparison.Ordinal);
}
