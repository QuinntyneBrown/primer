// Acceptance Test
// Traces to: L2-031, L2-032, L2-050
// Description: Verify installation obtains consent before changing anything, edits a client
//              configuration surgically after backing it up, and refuses an artefact that is
//              not pinned, not HTTPS, redirected off-host, or of the wrong checksum.

using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Primer.IntegrationTests.TestSupport;
using Primer.Shared.Generation;
using Primer.Shared.Hosting;
using Primer.Shared.Mcp;
using Primer.Shared.Presentation;

namespace Primer.IntegrationTests.Mcp;

public sealed class McpInstallTests
{
    private static readonly byte[] Artifact = Encoding.UTF8.GetBytes("mcp-server-payload");

    private static string ArtifactChecksum => Convert.ToHexStringLower(SHA256.HashData(Artifact));

    private static McpRequirement Requirement(string source, string? checksum = null) => new(
        Name: "docs",
        VersionRange: "1.2.0",
        InstallMethod: "npx",
        SourceUrl: new Uri(source),
        ExpectedChecksum: checksum ?? ArtifactChecksum);

    private static ArtifactVerifier Verifier(HttpMessageHandler handler, TimeSpan? timeout = null) =>
        new(new HttpClient(handler) { Timeout = timeout ?? TimeSpan.FromSeconds(10) });

    private static TerminalCapabilities Capabilities(bool interactive) =>
        new(80, false, true, interactive);

    private static PrimerConsole Console(StringWriter output, StringWriter error, bool interactive = false) =>
        new(output, error, Capabilities(interactive), OutputFormat.Text, VerbosityLevel.Normal);

    // Given missing required servers and a non-interactive session without --yes, when
    // installation is attempted, then no change is applied and the outcome is exit 3.
    [Fact]
    public void Given_a_non_interactive_session_without_yes_When_installing_Then_it_refuses_with_exit_three()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var console = Console(output, error, interactive: false);
        var prompt = new ConsentPrompt(console, Capabilities(interactive: false), TextReader.Null);

        var outcome = prompt.Confirm("Install 1 MCP server?", assumeYes: false);

        Assert.Equal(ConsentOutcome.RequiresYesFlag, outcome);
        Assert.Contains("--yes", error.ToString(), StringComparison.Ordinal);
    }

    // Given a client configuration containing unrelated entries, when it is edited,
    // then every unrelated entry is preserved with its original value.
    [Fact]
    public void Given_unrelated_entries_When_the_config_is_edited_Then_they_are_preserved()
    {
        using var repository = new TemporaryRepository();
        var path = repository.Write(
            "client.json",
            """
            {
              "theme": "dark",
              "mcpServers": {
                "existing": { "command": "node", "version": "3.0.0" }
              },
              "telemetry": { "enabled": false }
            }
            """);

        ClientConfigEditor.Register(path, Requirement("https://registry.example.com/docs.tgz"));

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;

        Assert.Equal("dark", root.GetProperty("theme").GetString());
        Assert.False(root.GetProperty("telemetry").GetProperty("enabled").GetBoolean());
        Assert.Equal("3.0.0", root.GetProperty("mcpServers").GetProperty("existing").GetProperty("version").GetString());
        Assert.Equal("1.2.0", root.GetProperty("mcpServers").GetProperty("docs").GetProperty("version").GetString());
    }

    // Given a client configuration, when it is modified, then a timestamped backup of the
    // prior content is written first and its path is reported.
    [Fact]
    public void Given_a_config_about_to_change_When_backed_up_Then_the_prior_content_is_recoverable()
    {
        using var repository = new TemporaryRepository();
        const string Original = """{ "mcpServers": { } }""";
        repository.Write("client.json", Original);

        var backup = new BackupWriter(new RepositoryLocation(repository.Path)).Backup("client.json");

        Assert.True(File.Exists(backup));
        Assert.Equal(Original, File.ReadAllText(backup));
        Assert.EndsWith(".bak", backup, StringComparison.Ordinal);
    }

    // Given a required server already present at an acceptable version, when installation
    // runs, then that entry is left unchanged.
    [Fact]
    public void Given_an_already_satisfied_entry_When_the_config_is_edited_Then_it_is_left_unchanged()
    {
        using var repository = new TemporaryRepository();
        var path = repository.Write(
            "client.json",
            """{ "mcpServers": { "docs": { "command": "npx", "version": "1.2.0" } } }""");
        var before = File.ReadAllText(path);

        var changed = ClientConfigEditor.Register(path, Requirement("https://registry.example.com/docs.tgz"));

        Assert.False(changed);
        Assert.Equal(before, File.ReadAllText(path));
    }

    // Given an install source that is not HTTPS, when verification runs,
    // then it is refused and the outcome is exit 3.
    [Fact]
    public async Task Given_a_non_https_source_When_verified_Then_it_is_refused_with_exit_three()
    {
        using var handler = new StubHandler(_ => Respond(HttpStatusCode.OK, Artifact));

        var failure = await Assert.ThrowsAsync<InsecureSourceException>(
            () => Verifier(handler).VerifyAsync(
                Requirement("http://registry.example.com/docs.tgz"), TestContext.Current.CancellationToken));

        Assert.Equal(ExitCode.Configuration, failure.ExitCode);
    }

    // Given a response redirecting to a host other than the declared one, when verification
    // runs, then the redirect is not followed and the outcome is exit 1.
    [Fact]
    public async Task Given_a_cross_host_redirect_When_verified_Then_it_is_not_followed()
    {
        var followed = false;

        using var handler = new StubHandler(request =>
        {
            if (request.RequestUri!.Host == "elsewhere.example.net")
            {
                followed = true;
                return Respond(HttpStatusCode.OK, Artifact);
            }

            var redirect = Respond(HttpStatusCode.Redirect, []);
            redirect.Headers.Location = new Uri("https://elsewhere.example.net/docs.tgz");
            return redirect;
        });

        var failure = await Assert.ThrowsAsync<RedirectRefusedException>(
            () => Verifier(handler).VerifyAsync(
                Requirement("https://registry.example.com/docs.tgz"), TestContext.Current.CancellationToken));

        Assert.Equal(ExitCode.Unexpected, failure.ExitCode);
        Assert.False(followed, "The cross-host redirect was followed.");
    }

    // Given a downloaded artefact whose checksum does not match the declared value,
    // when verification runs, then nothing is installed and the outcome is exit 1.
    [Fact]
    public async Task Given_a_checksum_mismatch_When_verified_Then_nothing_is_installed()
    {
        using var handler = new StubHandler(_ => Respond(HttpStatusCode.OK, Encoding.UTF8.GetBytes("tampered")));

        var failure = await Assert.ThrowsAsync<ChecksumMismatchException>(
            () => Verifier(handler).VerifyAsync(
                Requirement("https://registry.example.com/docs.tgz"), TestContext.Current.CancellationToken));

        Assert.Equal(ExitCode.Unexpected, failure.ExitCode);
    }

    // Given a declared component, when it is verified,
    // then it resolves at a pinned version rather than a floating tag.
    [Fact]
    public async Task Given_a_pinned_requirement_When_verified_Then_the_artefact_is_accepted()
    {
        using var handler = new StubHandler(_ => Respond(HttpStatusCode.OK, Artifact));

        var verified = await Verifier(handler).VerifyAsync(
            Requirement("https://registry.example.com/docs-1.2.0.tgz"), TestContext.Current.CancellationToken);

        Assert.Equal("1.2.0", verified.PinnedVersion);
        Assert.Equal(ArtifactChecksum, verified.Checksum);
    }

    // Given a floating version rather than a pinned one, when verification runs,
    // then it is refused before anything is downloaded.
    [Theory]
    [InlineData("latest")]
    [InlineData("*")]
    public async Task Given_a_floating_version_When_verified_Then_it_is_refused(string version)
    {
        var downloaded = false;

        using var handler = new StubHandler(_ =>
        {
            downloaded = true;
            return Respond(HttpStatusCode.OK, Artifact);
        });

        var requirement = Requirement("https://registry.example.com/docs.tgz") with { VersionRange = version };

        await Assert.ThrowsAsync<UnpinnedVersionException>(
            () => Verifier(handler).VerifyAsync(requirement, TestContext.Current.CancellationToken));

        Assert.False(downloaded, "A floating version was downloaded before being refused.");
    }

    private static HttpResponseMessage Respond(HttpStatusCode status, byte[] body) =>
        new(status) { Content = new ByteArrayContent(body) };

    /// <summary>Answers requests from a canned function so no test reaches a network.</summary>
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
