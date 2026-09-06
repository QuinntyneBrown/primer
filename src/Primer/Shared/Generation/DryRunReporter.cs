using System.Text;
using Primer.Shared.Presentation;

namespace Primer.Shared.Generation;

/// <summary>Renders a write plan without applying it.</summary>
internal interface IDryRunReporter
{
    void Report(WritePlan plan);
}

/// <summary>
/// Shows what a run would do. A new file is shown in full; an existing one is shown as the
/// difference, because that is the part a reviewer needs to judge.
/// </summary>
internal sealed class DryRunReporter(IPrimerConsole console) : IDryRunReporter
{
    public void Report(WritePlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var report = new StringBuilder();

        foreach (var entry in plan.Entries)
        {
            switch (entry.Action)
            {
                case FileAction.Create:
                    report.Append("create: ").AppendLine(entry.RelativePath);
                    report.AppendLine(entry.Content.TrimEnd('\n'));
                    break;

                case FileAction.Update:
                    report.Append("update: ").AppendLine(entry.RelativePath);
                    report.AppendLine(Diff(entry));
                    break;

                default:
                    report.Append("unchanged: ").AppendLine(entry.RelativePath);
                    break;
            }
        }

        // A diff wrapped to the terminal width is no longer a diff.
        console.WritePreformatted(report.ToString().TrimEnd('\n'));
    }

    /// <summary>
    /// A unified difference with common leading and trailing lines left as context, so the
    /// changed lines are the ones that stand out.
    /// </summary>
    private static string Diff(GeneratedFile entry)
    {
        var existing = entry.ExistingContent ?? string.Empty;
        var updated = entry.Content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var original = existing.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

        var prefix = 0;
        while (prefix < original.Length && prefix < updated.Length
            && string.Equals(original[prefix], updated[prefix], StringComparison.Ordinal))
        {
            prefix++;
        }

        var suffix = 0;
        while (suffix < original.Length - prefix && suffix < updated.Length - prefix
            && string.Equals(
                original[^(suffix + 1)], updated[^(suffix + 1)], StringComparison.Ordinal))
        {
            suffix++;
        }

        var difference = new StringBuilder();

        foreach (var line in original[prefix..(original.Length - suffix)])
        {
            difference.Append('-').AppendLine(line);
        }

        foreach (var line in updated[prefix..(updated.Length - suffix)])
        {
            difference.Append('+').AppendLine(line);
        }

        return difference.ToString().TrimEnd('\n');
    }
}
