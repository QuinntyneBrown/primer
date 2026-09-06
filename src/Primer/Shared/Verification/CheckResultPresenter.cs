using Primer.Shared.Hosting;
using Primer.Shared.Presentation;

namespace Primer.Shared.Verification;

/// <summary>
/// Renders a drift report. Naming the divergent paths is the whole value: a failing check
/// that does not say what drifted leaves the reader to regenerate and diff by hand.
/// </summary>
internal sealed class CheckResultPresenter(IPrimerConsole console)
{
    internal ExitCode Present(DriftReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        if (console.Format is OutputFormat.Json)
        {
            console.WriteResult(new
            {
                drift = report.HasDrift,
                entries = report.Entries.Select(entry => new
                {
                    path = entry.RelativePath,
                    status = entry.Kind.ToString().ToLowerInvariant(),
                }),
            });

            return report.ExitCode;
        }

        if (!report.HasDrift)
        {
            console.WriteResult("Generated agent files are current.");
            return ExitCode.Success;
        }

        var lines = report.Entries
            .Where(entry => entry.Kind is not DriftKind.Current)
            .Select(entry => $"{entry.Kind.ToString().ToLowerInvariant()}: {entry.RelativePath}")
            .Prepend("Generated agent files have drifted. Run `primer init` to bring them current.")
            .ToList();

        console.WriteResult(string.Join('\n', lines));
        return ExitCode.Verification;
    }
}
