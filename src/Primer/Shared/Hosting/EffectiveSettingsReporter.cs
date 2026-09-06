using Microsoft.Extensions.Configuration;
using Primer.Shared.Presentation;

namespace Primer.Shared.Hosting;

/// <summary>
/// Writes each setting with its value and the source it came from. A developer diagnosing
/// unexpected behaviour can see which layer supplied a value rather than inferring it.
/// </summary>
internal sealed class EffectiveSettingsReporter(IConfigurationRoot configuration, IPrimerConsole console)
{
    internal void Report()
    {
        if (console.Verbosity < VerbosityLevel.Diagnostic)
        {
            return;
        }

        foreach (var line in configuration.GetDebugView().Split('\n'))
        {
            var trimmed = line.TrimEnd();

            if (trimmed.Length > 0)
            {
                console.WriteErrorLine(trimmed);
            }
        }
    }
}
