using Microsoft.Extensions.DependencyInjection;
using Primer.Shared.Presentation;

namespace Primer.Shared.Hosting;

/// <summary>
/// What the host needs in order to compose itself. Streams and capabilities are supplied
/// rather than discovered, so a test builds the same host without touching the console.
/// </summary>
internal sealed record PrimerHostOptions
{
    /// <summary>The resolved repository root the run operates on.</summary>
    public required string RepositoryRoot { get; init; }

    /// <summary>Environment variables to consider, already captured.</summary>
    public IReadOnlyDictionary<string, string?> Environment { get; init; } =
        new Dictionary<string, string?>(StringComparer.Ordinal);

    /// <summary>Configuration keys parsed from the command line, the highest precedence.</summary>
    public IReadOnlyDictionary<string, string?> CommandLineOverrides { get; init; } =
        new Dictionary<string, string?>(StringComparer.Ordinal);

    /// <summary>Where command results are written.</summary>
    public TextWriter Output { get; init; } = TextWriter.Null;

    /// <summary>Where diagnostics and failures are written.</summary>
    public TextWriter Error { get; init; } = TextWriter.Null;

    /// <summary>How results are rendered.</summary>
    public OutputFormat Format { get; init; } = OutputFormat.Text;

    /// <summary>How much the run says while it works.</summary>
    public VerbosityLevel Verbosity { get; init; } = VerbosityLevel.Normal;

    /// <summary>Terminal capabilities; detected from the process when not supplied.</summary>
    public ITerminalCapabilities? Capabilities { get; init; }

    /// <summary>Applied last, so a test can substitute any registration.</summary>
    public Action<IServiceCollection>? ConfigureServices { get; init; }
}
