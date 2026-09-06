namespace Primer.Shared.Hosting;

/// <summary>
/// The outcomes the process returns. Scripts and continuous integration branch on these
/// values, so the mapping is fixed and additions are breaking changes.
/// </summary>
internal enum ExitCode
{
    /// <summary>The command completed.</summary>
    Success = 0,

    /// <summary>An unhandled failure escaped the command.</summary>
    Unexpected = 1,

    /// <summary>The command line could not be parsed, or named something unrecognised.</summary>
    Usage = 2,

    /// <summary>Configuration or input was rejected before work began.</summary>
    Configuration = 3,

    /// <summary>Verification found drift, or a required component was missing.</summary>
    Verification = 4,
}
