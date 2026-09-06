// Acceptance Test
// Traces to: L2-036, L2-037
// Description: Verify the service container composes and validates at startup, that its
//              registrations can be substituted under test, and that a repository template
//              override replaces the built-in template.

using Microsoft.Extensions.DependencyInjection;
using Primer.IntegrationTests.TestSupport;
using Primer.Shared.Hosting;
using Primer.Shared.Presentation;

namespace Primer.IntegrationTests.ResolveConfiguration;

public sealed class CompositionAndTemplateTests
{
    private static PrimerHostOptions Options(TemporaryRepository repository) => new()
    {
        RepositoryRoot = repository.Path,
        Output = TextWriter.Null,
        Error = TextWriter.Null,
    };

    // Given the service container, when it is built and validated at startup, then no
    // service has an unresolvable dependency and no captive dependency is present.
    [Fact]
    public void Given_the_container_When_it_is_built_Then_it_validates_with_no_unresolvable_dependency()
    {
        using var repository = new TemporaryRepository();

        // Build validates the container; a broken graph fails here rather than at first use.
        using var result = PrimerHostBuilder.Build(Options(repository));

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Host.Services.GetService<IPrimerConsole>());
        Assert.NotNull(result.Host.Services.GetService<ITemplateLocator>());
        Assert.NotNull(result.Host.Services.GetService<ITerminalCapabilities>());
        Assert.NotNull(result.Host.Services.GetService<ErrorPresenter>());
    }

    // Given a handler under test, when a test substitutes the console abstraction in the
    // container, then the handler executes with no real console access.
    [Fact]
    public void Given_a_substituted_console_When_resolved_Then_the_substitute_is_used()
    {
        using var repository = new TemporaryRepository();
        var substitute = new RecordingConsole();

        var options = Options(repository) with
        {
            ConfigureServices = services => services.AddSingleton<IPrimerConsole>(substitute),
        };

        using var result = PrimerHostBuilder.Build(options);

        Assert.True(result.Succeeded);
        Assert.Same(substitute, result.Host.Services.GetRequiredService<IPrimerConsole>());
    }

    // Given no template override present, when a template is located,
    // then the built-in template is used.
    [Fact]
    public void Given_no_override_When_a_template_is_located_Then_the_built_in_is_used()
    {
        using var repository = new TemporaryRepository();

        using var result = PrimerHostBuilder.Build(Options(repository));
        Assert.True(result.Succeeded);

        var located = result.Host.Services.GetRequiredService<ITemplateLocator>()
            .Locate(TemplateNames.AgentsFile);

        Assert.False(located.IsOverride);
        Assert.False(string.IsNullOrWhiteSpace(located.Content));
    }

    // Given a repository containing a template override at the configured template path,
    // when a template is located, then the override is used in place of the built-in.
    [Fact]
    public void Given_an_override_When_a_template_is_located_Then_the_override_replaces_the_built_in()
    {
        using var repository = new TemporaryRepository();
        repository.Write($"templates/{TemplateNames.AgentsFile}", "# {{ProjectOverview}}\n");

        using var result = PrimerHostBuilder.Build(Options(repository));
        Assert.True(result.Succeeded);

        var located = result.Host.Services.GetRequiredService<ITemplateLocator>()
            .Locate(TemplateNames.AgentsFile);

        Assert.True(located.IsOverride);
        Assert.Contains("{{ProjectOverview}}", located.Content, StringComparison.Ordinal);
    }

    // Given a template override referencing an unknown token, when it is located,
    // then the failure names the unknown token and reports exit 3.
    [Fact]
    public void Given_an_override_with_an_unknown_token_When_located_Then_it_fails_naming_the_token()
    {
        using var repository = new TemporaryRepository();
        repository.Write($"templates/{TemplateNames.AgentsFile}", "# {{NotARealToken}}\n");

        using var result = PrimerHostBuilder.Build(Options(repository));
        Assert.True(result.Succeeded);

        var locator = result.Host.Services.GetRequiredService<ITemplateLocator>();

        var failure = Assert.Throws<TemplateTokenException>(
            () => locator.Locate(TemplateNames.AgentsFile));

        Assert.Contains("NotARealToken", failure.Message, StringComparison.Ordinal);
        Assert.Equal(ExitCode.Configuration, failure.ExitCode);
    }

    /// <summary>Captures writes so a test can prove the real console was never touched.</summary>
    private sealed class RecordingConsole : IPrimerConsole
    {
        public ITerminalCapabilities Capabilities { get; } = new TerminalCapabilities(80, false, true, false);

        public OutputFormat Format => OutputFormat.Text;

        public VerbosityLevel Verbosity => VerbosityLevel.Normal;

        public List<string> Written { get; } = [];

        public void WriteResult(object payload) => Written.Add(payload.ToString() ?? string.Empty);

        public void WriteWarning(string message) => Written.Add(message);

        public void WriteDiagnostic(string message) => Written.Add(message);

        public void WriteLog(DateTimeOffset timestamp, string level, string category, string message) =>
            Written.Add(message);

        public void ReportProgress(string message) => Written.Add(message);

        public void WriteErrorLine(string line) => Written.Add(line);

        public ExitCode WriteFailure(string message, ExitCode exitCode)
        {
            Written.Add(message);
            return exitCode;
        }
    }
}
