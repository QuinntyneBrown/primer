using Primer.Shared.Hosting;

namespace Primer.Shared.Presentation;

/// <summary>
/// An expected failure, carrying everything a reader needs to act: what failed, the value
/// responsible for it, and what to do next.
/// </summary>
internal sealed record PrimerError(
    string What,
    string Subject,
    string NextAction,
    ExitCode ExitCode,
    Exception? Cause = null);

/// <summary>
/// Renders failures. Exception detail is withheld below diagnostic verbosity, because a
/// stack trace displaces the message that tells a reader what to do.
/// </summary>
internal sealed class ErrorPresenter(IPrimerConsole console)
{
    internal ExitCode Present(PrimerError error, VerbosityLevel verbosity)
    {
        ArgumentNullException.ThrowIfNull(error);

        if (console.Format is OutputFormat.Json)
        {
            return console.WriteFailure(error.What, error.ExitCode);
        }

        console.WriteFailure(error.What, error.ExitCode);

        if (!string.IsNullOrWhiteSpace(error.Subject))
        {
            console.WriteErrorLine($"  subject: {error.Subject}");
        }

        console.WriteErrorLine($"  next:    {error.NextAction}");

        if (verbosity >= VerbosityLevel.Diagnostic && error.Cause is not null)
        {
            console.WriteErrorLine(error.Cause.ToString());
        }

        return error.ExitCode;
    }

    /// <summary>
    /// Contains a failure that escaped every expected path. The summary is one line: an
    /// unexpected failure is a defect to report, not a condition to explain.
    /// </summary>
    internal ExitCode PresentUnhandled(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return console.WriteFailure(exception.Message, ExitCode.Unexpected);
    }
}
