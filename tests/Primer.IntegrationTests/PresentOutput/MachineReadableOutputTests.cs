// Acceptance Test
// Traces to: L2-058
// Description: Verify --format json emits one stable, escape-free JSON envelope carrying
//              a schema version, and carrying error and exitCode on failure.

using System.Text.Json;
using Primer.Shared.Hosting;
using Primer.Shared.Presentation;

namespace Primer.IntegrationTests.PresentOutput;

public sealed class MachineReadableOutputTests
{
    private static TerminalCapabilities Capabilities() =>
        new(80, SupportsColor: true, SupportsUnicode: true, IsInteractive: true);

    private static PrimerConsole JsonConsole(StringWriter output, StringWriter error) =>
        new(output, error, Capabilities(), OutputFormat.Json, VerbosityLevel.Normal);

    // Given --format json on any command, when it succeeds,
    // then stdout is a single JSON object containing a schemaVersion property.
    [Fact]
    public void Given_json_format_When_a_command_succeeds_Then_stdout_is_one_object_with_a_schema_version()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        JsonConsole(output, error).WriteResult(new { created = new[] { "AGENTS.md", "CLAUDE.md" } });

        using var document = JsonDocument.Parse(output.ToString());
        Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
        Assert.True(document.RootElement.TryGetProperty("schemaVersion", out var schemaVersion));
        Assert.False(string.IsNullOrWhiteSpace(schemaVersion.GetString()));
    }

    // Given --format json on any command, when it fails, then stdout is a single JSON
    // object containing schemaVersion, error, and exitCode, and the process exit code
    // matches the reported exitCode.
    [Fact]
    public void Given_json_format_When_a_command_fails_Then_the_envelope_carries_error_and_matching_exit_code()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var console = JsonConsole(output, error);

        var reported = console.WriteFailure("No repository root was found", ExitCode.Configuration);

        using var document = JsonDocument.Parse(output.ToString());
        Assert.True(document.RootElement.TryGetProperty("schemaVersion", out _));
        Assert.Equal(
            "No repository root was found",
            document.RootElement.GetProperty("error").GetString());
        Assert.Equal((int)ExitCode.Configuration, document.RootElement.GetProperty("exitCode").GetInt32());
        Assert.Equal(ExitCode.Configuration, reported);
    }

    // Given the same command run twice against an unchanged repository with --format json,
    // when the outputs are compared, then they are byte-identical.
    [Fact]
    public void Given_json_format_When_the_same_result_is_rendered_twice_Then_the_output_is_identical()
    {
        static string Render()
        {
            using var output = new StringWriter();
            using var error = new StringWriter();
            new PrimerConsole(output, error, Capabilities(), OutputFormat.Json, VerbosityLevel.Normal)
                .WriteResult(new { unchanged = new[] { "AGENTS.md" } });
            return output.ToString();
        }

        Assert.Equal(Render(), Render());
    }

    // Given --format json, when the output is parsed,
    // then no property value contains an ANSI escape sequence.
    [Fact]
    public void Given_json_format_When_rendered_on_a_colour_terminal_Then_no_ansi_escape_appears()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var console = JsonConsole(output, error);

        console.WriteResult(new { created = new[] { "AGENTS.md" } });
        console.WriteFailure("something went wrong", ExitCode.Unexpected);

        Assert.DoesNotContain('\u001b', output.ToString());
    }

    // Given --format json, when a command completes,
    // then stdout carries no human-oriented prose line alongside the document.
    [Fact]
    public void Given_json_format_When_diagnostics_are_written_Then_they_do_not_reach_stdout()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var console = new PrimerConsole(
            output, error, Capabilities(), OutputFormat.Json, VerbosityLevel.Detailed);

        console.WriteDiagnostic("resolved repository root");
        console.WriteWarning("bin/ could not be read");
        console.WriteResult(new { created = Array.Empty<string>() });

        using var document = JsonDocument.Parse(output.ToString());
        Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
        Assert.Contains("resolved repository root", error.ToString(), StringComparison.Ordinal);
    }

    // Given a value containing characters JSON would ordinarily escape aggressively,
    // when rendered, then the value round-trips to exactly what was supplied.
    [Fact]
    public void Given_a_value_with_reserved_characters_When_rendered_Then_it_round_trips_unchanged()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        JsonConsole(output, error).WriteResult(new { version = "0.1.0+6864ca1" });

        using var document = JsonDocument.Parse(output.ToString());
        Assert.Equal(
            "0.1.0+6864ca1",
            document.RootElement.GetProperty("payload").GetProperty("version").GetString());
    }
}
