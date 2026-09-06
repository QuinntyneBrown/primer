using Primer.Shared.Hosting;

namespace Primer.Shared.Presentation;

/// <summary>
/// The single path through which a handler writes. Owns stream selection, so a command
/// result cannot reach the diagnostic stream and a warning cannot reach the result stream.
/// </summary>
internal interface IPrimerConsole
{
    ITerminalCapabilities Capabilities { get; }

    OutputFormat Format { get; }

    VerbosityLevel Verbosity { get; }

    void WriteResult(object payload);

    /// <summary>
    /// Writes text whose own line structure carries meaning. A diff or a file preview is
    /// corrupted by wrapping, so it is emitted exactly as composed.
    /// </summary>
    void WritePreformatted(string text);

    void WriteWarning(string message);

    void WriteDiagnostic(string message);

    void WriteLog(DateTimeOffset timestamp, string level, string category, string message);

    void ReportProgress(string message);

    void WriteErrorLine(string line);

    ExitCode WriteFailure(string message, ExitCode exitCode);
}

/// <inheritdoc cref="IPrimerConsole" />
internal sealed class PrimerConsole : IPrimerConsole
{
    private const string Reset = "\u001b[0m";
    private const string Yellow = "\u001b[33m";
    private const string Red = "\u001b[31m";

    private readonly TextWriter _output;
    private readonly TextWriter _error;
    private readonly IOutputFormatter _formatter;
    private readonly JsonOutputFormatter _envelopeFormatter = new();

    internal PrimerConsole(
        TextWriter output,
        TextWriter error,
        ITerminalCapabilities capabilities,
        OutputFormat format,
        VerbosityLevel verbosity)
    {
        _output = output ?? throw new ArgumentNullException(nameof(output));
        _error = error ?? throw new ArgumentNullException(nameof(error));
        Capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
        Format = format;
        Verbosity = verbosity;

        _formatter = format is OutputFormat.Json
            ? _envelopeFormatter
            : new TextOutputFormatter(capabilities);
    }

    public ITerminalCapabilities Capabilities { get; }

    public OutputFormat Format { get; }

    public VerbosityLevel Verbosity { get; }

    public void WriteResult(object payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        if (Verbosity is VerbosityLevel.Quiet)
        {
            return;
        }

        var rendered = Format is OutputFormat.Json
            ? _envelopeFormatter.Render(new OutputEnvelope { Payload = payload })
            : _formatter.Render(payload);

        _output.WriteLine(rendered);
    }

    public void WritePreformatted(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (Verbosity is VerbosityLevel.Quiet)
        {
            return;
        }

        if (Format is OutputFormat.Json)
        {
            _output.WriteLine(_envelopeFormatter.Render(new OutputEnvelope { Payload = text }));
            return;
        }

        _output.WriteLine(text);
    }

    public void WriteWarning(string message)
    {
        if (Verbosity < VerbosityLevel.Normal)
        {
            return;
        }

        // The marker carries the meaning on its own, so colour adds emphasis rather
        // than information a colourless reading would lose.
        WriteErrorLine($"{Decorate("warning:", Yellow)} {message}");
    }

    public void WriteDiagnostic(string message)
    {
        if (Verbosity < VerbosityLevel.Detailed)
        {
            return;
        }

        WriteErrorLine(message);
    }

    public void WriteLog(DateTimeOffset timestamp, string level, string category, string message)
    {
        if (Verbosity < VerbosityLevel.Diagnostic)
        {
            return;
        }

        WriteErrorLine($"{timestamp:O} {level} {category} {message}");
    }

    public void ReportProgress(string message)
    {
        // A progress indicator repositions the cursor, which corrupts a log file. It is
        // emitted only where a person is watching it happen.
        if (!Capabilities.IsInteractive || Verbosity < VerbosityLevel.Detailed)
        {
            return;
        }

        _error.Write($"\r{message}");
    }

    public void WriteErrorLine(string line) => _error.WriteLine(line);

    public ExitCode WriteFailure(string message, ExitCode exitCode)
    {
        if (Format is OutputFormat.Json)
        {
            _output.WriteLine(_envelopeFormatter.Render(
                new OutputEnvelope { Error = message, ExitCode = (int)exitCode }));
        }
        else
        {
            WriteErrorLine($"{Decorate("error:", Red)} {message}");
        }

        return exitCode;
    }

    private string Decorate(string marker, string colour) =>
        Capabilities.SupportsColor ? $"{colour}{marker}{Reset}" : marker;
}
