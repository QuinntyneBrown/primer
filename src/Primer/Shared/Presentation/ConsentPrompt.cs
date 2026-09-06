namespace Primer.Shared.Presentation;

/// <summary>The answer to a request for consent.</summary>
internal enum ConsentOutcome
{
    /// <summary>The operator approved the change.</summary>
    Granted,

    /// <summary>The operator declined the change.</summary>
    Declined,

    /// <summary>Nobody was present to ask, and --yes was not passed.</summary>
    RequiresYesFlag,
}

/// <summary>
/// Asks before a change is applied. A session with nobody present is refused rather than
/// answered on the operator's behalf, and input is never read in that case.
/// </summary>
internal sealed class ConsentPrompt(
    IPrimerConsole console,
    ITerminalCapabilities capabilities,
    TextReader input)
{
    internal ConsentOutcome Confirm(string question, bool assumeYes)
    {
        if (assumeYes)
        {
            return ConsentOutcome.Granted;
        }

        if (!capabilities.IsInteractive)
        {
            console.WriteErrorLine(
                "error: this session is not interactive. Pass --yes to apply these changes.");
            return ConsentOutcome.RequiresYesFlag;
        }

        console.WriteErrorLine($"{question} [y/N]");

        var answer = input.ReadLine();

        return string.Equals(answer?.Trim(), "y", StringComparison.OrdinalIgnoreCase)
            ? ConsentOutcome.Granted
            : ConsentOutcome.Declined;
    }
}
