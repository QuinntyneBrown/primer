// Acceptance Test
// Traces to: L2-059, L2-060, L2-062
// Description: Verify output adapts to terminal width, emits colour only where it is
//              supported and meaningful, and falls back to ASCII when the console
//              encoding cannot carry a character.

using Primer.Shared.Hosting;
using Primer.Shared.Presentation;

namespace Primer.IntegrationTests.PresentOutput;

public sealed class TerminalAdaptationTests
{
    private static TerminalCapabilities Capabilities(
        int width = 80,
        bool supportsColor = false,
        bool supportsUnicode = true,
        bool isInteractive = false) =>
        new(width, supportsColor, supportsUnicode, isInteractive);

    // Given a terminal 40 columns wide, when any command renders output,
    // then no line exceeds 40 characters.
    [Fact]
    public void Given_a_forty_column_terminal_When_text_is_rendered_Then_no_line_exceeds_the_width()
    {
        var formatter = new TextOutputFormatter(Capabilities(width: 40));

        var rendered = formatter.Render(
            "The repository context was derived from marker files, convention files, and workflow definitions.");

        Assert.All(
            rendered.Split('\n', StringSplitOptions.RemoveEmptyEntries),
            line => Assert.True(line.TrimEnd('\r').Length <= 40, $"Line exceeded 40 columns: '{line}'"));
    }

    // Given a terminal 200 columns wide, when output is rendered,
    // then it expands to the available width rather than wrapping at a narrower default.
    [Fact]
    public void Given_a_wide_terminal_When_text_is_rendered_Then_it_is_not_wrapped_at_the_default_width()
    {
        var text = string.Join(' ', Enumerable.Repeat("word", 30));
        var formatter = new TextOutputFormatter(Capabilities(width: 200));

        var rendered = formatter.Render(text).TrimEnd();

        Assert.DoesNotContain('\n', rendered);
    }

    // Given output redirected with no detectable terminal width,
    // when any command runs, then a default width of 80 columns is assumed.
    [Fact]
    public void Given_no_detectable_width_When_capabilities_are_detected_Then_eighty_columns_are_assumed()
    {
        var capabilities = TerminalCapabilities.Detect(
            environmentVariable: _ => null,
            detectedWidth: null,
            outputRedirected: true,
            consoleSupportsUnicode: true);

        Assert.Equal(80, capabilities.Width);
    }

    // Given a long repository path that exceeds the available width, when it is rendered,
    // then it is elided in the middle so both the leading and trailing segments remain visible.
    [Fact]
    public void Given_a_path_longer_than_the_budget_When_elided_Then_both_ends_remain_visible()
    {
        const string Path = "src/Primer/Shared/Generation/Sections/BoundariesSection.cs";

        var elided = PathElider.Elide(Path, budget: 30);

        Assert.True(elided.Length <= 30, $"Elided path was {elided.Length} characters: '{elided}'");
        Assert.StartsWith("src/", elided, StringComparison.Ordinal);
        Assert.EndsWith("BoundariesSection.cs", elided, StringComparison.Ordinal);
        Assert.Contains('…', elided);
    }

    // Given a path that already fits the budget, when elided, then it is returned unchanged.
    [Fact]
    public void Given_a_path_within_the_budget_When_elided_Then_it_is_unchanged()
    {
        const string Path = "src/Primer/Program.cs";

        Assert.Equal(Path, PathElider.Elide(Path, budget: 80));
    }

    // Given the environment variable NO_COLOR is set to any value,
    // when capabilities are detected, then colour is not supported.
    [Fact]
    public void Given_NO_COLOR_is_set_When_capabilities_are_detected_Then_colour_is_off()
    {
        var capabilities = TerminalCapabilities.Detect(
            environmentVariable: name => name == "NO_COLOR" ? "1" : null,
            detectedWidth: 120,
            outputRedirected: false,
            consoleSupportsUnicode: true);

        Assert.False(capabilities.SupportsColor);
    }

    // Given stdout redirected to a file, when any command runs,
    // then the output contains no ANSI escape sequence.
    [Fact]
    public void Given_redirected_output_When_a_result_is_written_Then_no_ansi_escape_is_emitted()
    {
        var capabilities = TerminalCapabilities.Detect(
            environmentVariable: _ => null,
            detectedWidth: null,
            outputRedirected: true,
            consoleSupportsUnicode: true);

        using var output = new StringWriter();
        using var error = new StringWriter();
        var console = new PrimerConsole(output, error, capabilities, OutputFormat.Text, VerbosityLevel.Normal);

        console.WriteResult("generation complete");
        console.WriteWarning("a path could not be read");

        Assert.False(capabilities.SupportsColor);
        Assert.DoesNotContain('', output.ToString());
        Assert.DoesNotContain('', error.ToString());
    }

    // Given an interactive colour-capable terminal, when a failure is reported, then the
    // failure is distinguished by a text marker carrying the same meaning as the colour.
    [Fact]
    public void Given_a_colour_terminal_When_a_failure_is_reported_Then_a_text_marker_carries_the_same_meaning()
    {
        var capabilities = Capabilities(supportsColor: true, isInteractive: true);
        using var output = new StringWriter();
        using var error = new StringWriter();
        var console = new PrimerConsole(output, error, capabilities, OutputFormat.Text, VerbosityLevel.Normal);

        console.WriteWarning("a path could not be read");

        var written = StripAnsi(error.ToString());
        Assert.Contains("warning:", written, StringComparison.Ordinal);
    }

    // Given a console whose encoding cannot represent non-ASCII characters, when output is
    // rendered, then ASCII substitutes are used and no replacement character is emitted.
    [Fact]
    public void Given_an_ascii_only_console_When_text_is_rendered_Then_ascii_substitutes_are_used()
    {
        var formatter = new TextOutputFormatter(Capabilities(width: 80, supportsUnicode: false));

        var rendered = formatter.Render("elided… done");

        Assert.DoesNotContain('…', rendered);
        Assert.DoesNotContain('�', rendered);
        Assert.Contains("...", rendered, StringComparison.Ordinal);
    }

    // Given a repository path containing non-ASCII characters, when it is rendered,
    // then the path is preserved exactly.
    [Fact]
    public void Given_a_unicode_path_When_rendered_on_a_unicode_console_Then_it_is_preserved_exactly()
    {
        const string Path = "src/Primer/Ressourcen/Prüfung.cs";
        var formatter = new TextOutputFormatter(Capabilities(width: 80, supportsUnicode: true));

        Assert.Contains(Path, formatter.Render(Path), StringComparison.Ordinal);
    }

    private static string StripAnsi(string text)
    {
        var builder = new System.Text.StringBuilder(text.Length);
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] != '')
            {
                builder.Append(text[index]);
                continue;
            }

            while (index < text.Length && text[index] != 'm')
            {
                index++;
            }
        }

        return builder.ToString();
    }
}
