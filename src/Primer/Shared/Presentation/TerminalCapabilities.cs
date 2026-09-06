namespace Primer.Shared.Presentation;

/// <summary>
/// What the attached terminal can carry. Rendering consults this rather than probing the
/// console directly, so every decision is testable without a terminal.
/// </summary>
internal interface ITerminalCapabilities
{
    /// <summary>Columns available for output.</summary>
    int Width { get; }

    /// <summary>Whether ANSI colour reaches a reader who can see it.</summary>
    bool SupportsColor { get; }

    /// <summary>Whether the console encoding can represent non-ASCII characters.</summary>
    bool SupportsUnicode { get; }

    /// <summary>Whether a person is present to answer a prompt.</summary>
    bool IsInteractive { get; }
}

/// <inheritdoc cref="ITerminalCapabilities" />
internal sealed record TerminalCapabilities(
    int Width,
    bool SupportsColor,
    bool SupportsUnicode,
    bool IsInteractive) : ITerminalCapabilities
{
    /// <summary>Assumed width when none can be detected.</summary>
    internal const int DefaultWidth = 80;

    /// <summary>
    /// Derives capabilities from the environment. Colour is withheld from a redirected
    /// stream, from a session that set NO_COLOR, and from continuous integration.
    /// </summary>
    internal static TerminalCapabilities Detect(
        Func<string, string?> environmentVariable,
        int? detectedWidth,
        bool outputRedirected,
        bool consoleSupportsUnicode)
    {
        ArgumentNullException.ThrowIfNull(environmentVariable);

        var continuousIntegration = !string.IsNullOrEmpty(environmentVariable("CI"));
        var colourSuppressed = environmentVariable("NO_COLOR") is not null;
        var interactive = !outputRedirected && !continuousIntegration;

        return new TerminalCapabilities(
            Width: detectedWidth is > 0 ? detectedWidth.Value : DefaultWidth,
            SupportsColor: interactive && !colourSuppressed,
            SupportsUnicode: consoleSupportsUnicode,
            IsInteractive: interactive);
    }

    /// <summary>Derives capabilities from the running process.</summary>
    internal static TerminalCapabilities FromCurrentProcess()
    {
        int? width = null;
        var redirected = System.Console.IsOutputRedirected;

        if (!redirected)
        {
            try
            {
                width = System.Console.WindowWidth;
            }
            catch (IOException)
            {
                // No console is attached; the default width applies.
            }
        }

        return Detect(
            Environment.GetEnvironmentVariable,
            width,
            redirected,
            CanRepresentNonAscii(System.Console.OutputEncoding));
    }

    private static bool CanRepresentNonAscii(System.Text.Encoding encoding)
    {
        const string Probe = "\u2026";
        return encoding.GetString(encoding.GetBytes(Probe)) == Probe;
    }
}
