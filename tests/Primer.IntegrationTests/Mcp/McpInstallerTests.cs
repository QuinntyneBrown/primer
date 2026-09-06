// Acceptance Test
// Traces to: L2-031, L2-032
// Description: Verify installation displays the exact changes, applies nothing without
//              consent or under --dry-run, and backs up a client configuration before
//              editing it.

using System.Net;
using System.Text;
using Primer.IntegrationTests.TestSupport;
using Primer.Shared.Generation;
using Primer.Shared.Hosting;
using Primer.Shared.Mcp;
using Primer.Shared.Presentation;

namespace Primer.IntegrationTests.Mcp;

public sealed class McpInstallerTests
{
    private const string Requirements = """
        {
          "Primer": {
            "McpRequirements": [
              { "Name": "docs", "VersionRange": "1.2.0", "InstallMethod": "npx" }
            ],
            "McpClients": [
              { "Name": "claude-code", "ConfigPath": "client.json", "Format": "Json" }
            ]
          }
        }
        """;

    private sealed record Rig(
        McpInstaller Installer,
        StringWriter Output,
        StringWriter Error,
        TemporaryRepository Repository) : IDisposable
    {
        public void Dispose()
        {
            Output.Dispose();
            Error.Dispose();
            Repository.Dispose();
        }
    }

    private static Rig Build(bool interactive, TextReader? input = null)
    {
        var repository = new TemporaryRepository();
        repository.Write("primer.json", Requirements);
        repository.Write("client.json", """{ "theme": "dark", "mcpServers": { } }""");

        var output = new StringWriter();
        var error = new StringWriter();
        var capabilities = new TerminalCapabilities(80, false, true, interactive);
        var console = new PrimerConsole(output, error, capabilities, OutputFormat.Text, VerbosityLevel.Normal);
        var location = new RepositoryLocation(repository.Path);

        var options = Microsoft.Extensions.Options.Options.Create(new PrimerOptions
        {
            McpRequirements = { new McpRequirementOptions { Name = "docs", VersionRange = "1.2.0", InstallMethod = "npx" } },
            McpClients = { new McpClientOptions { Name = "claude-code", ConfigPath = "client.json" } },
        });

        var probe = new ConfiguredMcpClientProbe(options, location);
        var analyzer = new McpGapAnalyzer(new ConfigurationMcpRequirementSource(options), probe);

        using var handler = new NeverCalledHandler();

        var installer = new McpInstaller(
            analyzer,
            new ArtifactVerifier(new HttpClient(handler)),
            new ConsentPrompt(console, capabilities, input ?? TextReader.Null),
            new BackupWriter(location),
            console);

        return new Rig(installer, output, error, repository);
    }

    // Given missing required servers, when installation runs,
    // then the exact changes are displayed before anything is applied.
    [Fact]
    public async Task Given_a_gap_When_installing_Then_the_exact_changes_are_displayed_first()
    {
        using var rig = Build(interactive: true, input: new StringReader("y\n"));

        await rig.Installer.InstallAsync(assumeYes: false, dryRun: true, TestContext.Current.CancellationToken);

        var shown = rig.Output.ToString();
        Assert.Contains("docs", shown, StringComparison.Ordinal);
        Assert.Contains("1.2.0", shown, StringComparison.Ordinal);
        Assert.Contains("client.json", shown, StringComparison.Ordinal);
    }

    // Given --dry-run, when installation runs, then no configuration file is modified.
    [Fact]
    public async Task Given_dry_run_When_installing_Then_nothing_is_applied()
    {
        using var rig = Build(interactive: true, input: new StringReader("y\n"));
        var before = rig.Repository.Read("client.json");

        var exit = await rig.Installer.InstallAsync(
            assumeYes: false, dryRun: true, TestContext.Current.CancellationToken);

        Assert.Equal(ExitCode.Success, exit);
        Assert.Equal(before, rig.Repository.Read("client.json"));
    }

    // Given the confirmation prompt and an operator who declines,
    // then no configuration file is modified.
    [Fact]
    public async Task Given_a_declined_prompt_When_installing_Then_nothing_is_applied()
    {
        using var rig = Build(interactive: true, input: new StringReader("n\n"));
        var before = rig.Repository.Read("client.json");

        var exit = await rig.Installer.InstallAsync(
            assumeYes: false, dryRun: false, TestContext.Current.CancellationToken);

        Assert.Equal(ExitCode.Success, exit);
        Assert.Equal(before, rig.Repository.Read("client.json"));
        Assert.Contains("Nothing was changed", rig.Output.ToString(), StringComparison.Ordinal);
    }

    // Given a non-interactive session without --yes, when installation runs,
    // then no change is applied and the outcome is exit 3.
    [Fact]
    public async Task Given_a_non_interactive_session_without_yes_When_installing_Then_it_exits_three()
    {
        using var rig = Build(interactive: false);
        var before = rig.Repository.Read("client.json");

        var exit = await rig.Installer.InstallAsync(
            assumeYes: false, dryRun: false, TestContext.Current.CancellationToken);

        Assert.Equal(ExitCode.Configuration, exit);
        Assert.Equal(before, rig.Repository.Read("client.json"));
        Assert.Contains("--yes", rig.Error.ToString(), StringComparison.Ordinal);
    }

    // Given consent, when installation runs, then a backup is written before the edit, its
    // path is reported, unrelated entries survive, and the server is registered.
    [Fact]
    public async Task Given_consent_When_installing_Then_it_backs_up_then_edits_surgically()
    {
        using var rig = Build(interactive: false);

        var exit = await rig.Installer.InstallAsync(
            assumeYes: true, dryRun: false, TestContext.Current.CancellationToken);

        Assert.Equal(ExitCode.Success, exit);

        var updated = rig.Repository.Read("client.json");
        Assert.Contains("\"docs\"", updated, StringComparison.Ordinal);
        Assert.Contains("\"theme\": \"dark\"", updated, StringComparison.Ordinal);

        var backups = Directory.GetFiles(rig.Repository.Path, "*.bak");
        Assert.Single(backups);
        Assert.Contains("mcpServers", File.ReadAllText(backups[0]), StringComparison.Ordinal);
        Assert.Contains("Backed up", rig.Output.ToString(), StringComparison.Ordinal);
    }

    // Given every requirement already satisfied, when installation runs,
    // then it reports so and changes nothing.
    [Fact]
    public async Task Given_nothing_to_do_When_installing_Then_it_reports_and_changes_nothing()
    {
        using var rig = Build(interactive: false);
        rig.Repository.Write(
            "client.json",
            """{ "mcpServers": { "docs": { "command": "npx", "version": "1.2.0" } } }""");
        var before = rig.Repository.Read("client.json");

        var exit = await rig.Installer.InstallAsync(
            assumeYes: true, dryRun: false, TestContext.Current.CancellationToken);

        Assert.Equal(ExitCode.Success, exit);
        Assert.Equal(before, rig.Repository.Read("client.json"));
        Assert.Contains("already installed", rig.Output.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Fails the test if installation reaches the network for a local requirement.</summary>
    private sealed class NeverCalledHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Installation contacted the network unexpectedly.");
    }
}
