namespace Primer.Shared.Hosting;

/// <summary>
/// How a command result is rendered on standard output.
/// </summary>
internal enum OutputFormat
{
    /// <summary>Human-oriented text, adapted to the terminal.</summary>
    Text = 0,

    /// <summary>A single machine-readable JSON document.</summary>
    Json = 1,
}
