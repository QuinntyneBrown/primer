// Acceptance Test
// Traces to: L2-055, L2-056, L2-057, L2-061
// Description: Verify command results and diagnostics travel on separate streams, that
//              failures are actionable with stack traces reserved for diagnostic
//              verbosity, and that a non-interactive session never blocks for input.

using Primer.Shared.Hosting;
using Primer.Shared.Presentation;

namespace Primer.IntegrationTests.PresentOutput;

public sealed class StreamDisciplineTests
{
    private static TerminalCapabilities Capabilities(bool isInteractive = false) =>
        new(80, SupportsColor: false, SupportsUnicode: true, isInteractive);

    private static PrimerConsole Console(
        StringWriter output,
        StringWriter error,
        VerbosityLevel verbosity = VerbosityLevel.Normal,
        OutputFormat format = OutputFormat.Text,
        bool isInteractive = false) =>
        new(output, error, Capabilities(isInteractive), format, verbosity);

    // Given any command, when it emits diagnostic or warning output, then that output is
    // written to stderr and only command results are written to stdout.
    [Fact]
    public void Given_a_run_When_results_and_diagnostics_are_written_Then_they_use_separate_streams()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var console = Console(output, error, VerbosityLevel.Detailed);

        console.WriteResult("AGENTS.md created");
        console.WriteWarning("bin/ could not be read");
        console.WriteDiagnostic("resolved repository root");

        Assert.Contains("AGENTS.md created", output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("could not be read", output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("resolved repository root", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("could not be read", error.ToString(), StringComparison.Ordinal);
        Assert.Contains("resolved repository root", error.ToString(), StringComparison.Ordinal);
    }

    // Given --verbosity quiet, when a command completes successfully,
    // then stdout and stderr are both empty and the exit code alone conveys the outcome.
    [Fact]
    public void Given_quiet_verbosity_When_a_run_succeeds_Then_both_streams_are_empty()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var console = Console(output, error, VerbosityLevel.Quiet);

        console.WriteResult("AGENTS.md created");
        console.WriteWarning("bin/ could not be read");
        console.WriteDiagnostic("resolved repository root");

        Assert.Empty(output.ToString());
        Assert.Empty(error.ToString());
    }

    // Given --verbosity normal, when a command runs successfully,
    // then no debug or trace line appears in any output stream.
    [Fact]
    public void Given_normal_verbosity_When_a_run_succeeds_Then_no_diagnostic_line_is_emitted()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var console = Console(output, error, VerbosityLevel.Normal);

        console.WriteDiagnostic("resolved repository root");

        Assert.Empty(error.ToString());
    }

    // Given --verbosity diagnostic, when a command runs,
    // then each log line includes a timestamp, a level, and a category.
    [Fact]
    public void Given_diagnostic_verbosity_When_a_log_line_is_written_Then_it_carries_timestamp_level_and_category()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var console = Console(output, error, VerbosityLevel.Diagnostic);

        console.WriteLog(DateTimeOffset.Parse("2026-09-05T18:30:00Z", null), "debug", "Primer.Analysis", "scanning");

        var line = error.ToString();
        Assert.Contains("2026-09-05", line, StringComparison.Ordinal);
        Assert.Contains("debug", line, StringComparison.Ordinal);
        Assert.Contains("Primer.Analysis", line, StringComparison.Ordinal);
        Assert.Contains("scanning", line, StringComparison.Ordinal);
    }

    // Given any expected failure, when it occurs, then stderr names what failed, the path
    // or value responsible, and the next action the operator can take.
    [Fact]
    public void Given_an_expected_failure_When_presented_Then_it_names_cause_subject_and_next_action()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var console = Console(output, error);
        var presenter = new ErrorPresenter(console);

        var failure = new PrimerError(
            What: "No repository root was found",
            Subject: "C:/tmp/not-a-repo",
            NextAction: "Run primer from inside a git repository, or pass --path.",
            ExitCode: ExitCode.Configuration);

        var exitCode = presenter.Present(failure, VerbosityLevel.Normal);

        var written = error.ToString();
        Assert.Equal(ExitCode.Configuration, exitCode);
        Assert.Contains("No repository root was found", written, StringComparison.Ordinal);
        Assert.Contains("C:/tmp/not-a-repo", written, StringComparison.Ordinal);
        Assert.Contains("--path", written, StringComparison.Ordinal);
        Assert.Empty(output.ToString());
    }

    // Given a failure at normal verbosity, when it occurs,
    // then no stack trace is written to any output stream.
    [Fact]
    public void Given_normal_verbosity_When_a_failure_carries_an_exception_Then_no_stack_trace_is_written()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var presenter = new ErrorPresenter(Console(output, error));

        presenter.Present(FailureWithCause(), VerbosityLevel.Normal);

        Assert.DoesNotContain("   at ", error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(InvalidOperationException), error.ToString(), StringComparison.Ordinal);
    }

    // Given a failure at diagnostic verbosity, when it occurs,
    // then the full exception detail is written to stderr.
    [Fact]
    public void Given_diagnostic_verbosity_When_a_failure_carries_an_exception_Then_detail_is_written()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var presenter = new ErrorPresenter(Console(output, error, VerbosityLevel.Diagnostic));

        presenter.Present(FailureWithCause(), VerbosityLevel.Diagnostic);

        Assert.Contains(nameof(InvalidOperationException), error.ToString(), StringComparison.Ordinal);
    }

    // Given an unhandled exception anywhere in a command, when it propagates, then the
    // process exits 1 and a single-line summary is written to stderr.
    [Fact]
    public void Given_an_unhandled_exception_When_contained_Then_exit_is_one_and_the_summary_is_one_line()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var presenter = new ErrorPresenter(Console(output, error));

        var exitCode = presenter.PresentUnhandled(new InvalidOperationException("the analyser fell over"));

        var lines = error.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(ExitCode.Unexpected, exitCode);
        Assert.Single(lines);
        Assert.Contains("the analyser fell over", lines[0], StringComparison.Ordinal);
        Assert.Empty(output.ToString());
    }

    // Given a non-interactive session and a command that would prompt, when it is run
    // without --yes, then it does not wait for input, stderr states that --yes is
    // required, and the process exits 3.
    [Fact]
    public void Given_a_non_interactive_session_without_yes_When_consent_is_sought_Then_it_refuses_without_reading()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var console = Console(output, error, isInteractive: false);
        var prompt = new ConsentPrompt(console, Capabilities(isInteractive: false), new ThrowingTextReader());

        var outcome = prompt.Confirm("Install 2 MCP servers?", assumeYes: false);

        Assert.Equal(ConsentOutcome.RequiresYesFlag, outcome);
        Assert.Contains("--yes", error.ToString(), StringComparison.Ordinal);
    }

    // Given --yes in an interactive session, when a command that would prompt is run,
    // then it proceeds without prompting.
    [Fact]
    public void Given_yes_When_consent_is_sought_Then_it_is_granted_without_prompting()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var console = Console(output, error, isInteractive: true);
        var prompt = new ConsentPrompt(console, Capabilities(isInteractive: true), new ThrowingTextReader());

        Assert.Equal(ConsentOutcome.Granted, prompt.Confirm("Install 2 MCP servers?", assumeYes: true));
    }

    // Given a non-interactive session, when any command runs, then no spinner, progress
    // bar, or cursor-repositioning escape sequence is emitted.
    [Fact]
    public void Given_a_non_interactive_session_When_progress_is_reported_Then_no_control_sequence_is_emitted()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var console = Console(output, error, isInteractive: false);

        console.ReportProgress("scanning 1200 files");

        var written = output.ToString() + error.ToString();
        Assert.DoesNotContain('\u001b', written);
        Assert.DoesNotContain('\r', written);
    }

    // Given the environment variable CI is set, when capabilities are detected,
    // then the session is treated as non-interactive regardless of terminal detection.
    [Fact]
    public void Given_CI_is_set_When_capabilities_are_detected_Then_the_session_is_non_interactive()
    {
        var capabilities = TerminalCapabilities.Detect(
            environmentVariable: name => name == "CI" ? "true" : null,
            detectedWidth: 120,
            outputRedirected: false,
            consoleSupportsUnicode: true);

        Assert.False(capabilities.IsInteractive);
    }

    private static PrimerError FailureWithCause() => new(
        What: "The template could not be rendered",
        Subject: "templates/agents.md",
        NextAction: "Correct the template override, or remove it to use the built-in template.",
        ExitCode: ExitCode.Configuration,
        Cause: new InvalidOperationException("unknown token"));

    /// <summary>Proves a code path never reads input by throwing if it tries.</summary>
    private sealed class ThrowingTextReader : TextReader
    {
        public override int Read() => throw new InvalidOperationException("Input was read in a non-interactive session.");

        public override string? ReadLine() => throw new InvalidOperationException("Input was read in a non-interactive session.");
    }
}
