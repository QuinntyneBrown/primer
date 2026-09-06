using Primer.Shared.Presentation;

namespace Primer.Shared.Hosting;

/// <summary>
/// A failure the tool anticipated, carrying everything needed to report it: what failed,
/// the value responsible, what to do next, and the outcome it maps to.
/// </summary>
internal class PrimerException : Exception
{
    internal PrimerException(PrimerError error)
        : base(error?.What ?? "An expected failure occurred.")
    {
        Error = error ?? throw new ArgumentNullException(nameof(error));
    }

    internal PrimerException(PrimerError error, Exception innerException)
        : base(error?.What ?? "An expected failure occurred.", innerException)
    {
        Error = error ?? throw new ArgumentNullException(nameof(error));
    }

    /// <summary>The reportable failure.</summary>
    internal PrimerError Error { get; }

    /// <summary>The outcome this failure maps to.</summary>
    internal ExitCode ExitCode => Error.ExitCode;
}
