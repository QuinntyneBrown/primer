// Acceptance Test
// Traces to: L2-028, L2-029, L2-030, L2-033
// Description: Verify required MCP servers are declared as data, that installed servers are
//              detected from the configured client files, that the gap is reported with a
//              remediation command, and that undeterminable facts are reported as unknown
//              rather than guessed.

using Microsoft.Extensions.DependencyInjection;
using Primer.IntegrationTests.TestSupport;
using Primer.Shared.Hosting;
using Primer.Shared.Mcp;

namespace Primer.IntegrationTests.Mcp;

public sealed class McpGapTests
{
    private static string ClientConfig(TemporaryRepository repository, string relativePath, string content)
    {
        repository.Write(relativePath, content);
        return relativePath;
    }

    private static McpGapReport Analyse(TemporaryRepository repository, string primerJson)
    {
        repository.Write("primer.json", primerJson);

        using var result = PrimerHostBuilder.Build(new PrimerHostOptions
        {
            RepositoryRoot = repository.Path,
            Output = TextWriter.Null,
            Error = TextWriter.Null,
        });

        Assert.True(result.Succeeded);
        return result.Host.Services.GetRequiredService<McpGapAnalyzer>().Analyze();
    }

    private static string Requiring(string clientRelativePath, string versionRange = ">=1.0.0") => $$"""
        {
          "Primer": {
            "McpRequirements": [
              {
                "Name": "docs",
                "VersionRange": "{{versionRange}}",
                "InstallMethod": "npx",
                "SourceUrl": "https://registry.example.com/docs-1.2.0.tgz"
              }
            ],
            "McpClients": [
              { "Name": "claude-code", "ConfigPath": "{{clientRelativePath}}", "Format": "Json" }
            ]
          }
        }
        """;

    // Given a repository declaring required MCP servers, when they are listed,
    // then each is reported with its name, version constraint, and install method.
    [Fact]
    public void Given_declared_requirements_When_listed_Then_each_names_its_constraint_and_method()
    {
        using var repository = new TemporaryRepository();
        var report = Analyse(repository, Requiring("client.json"));

        var entry = Assert.Single(report.Entries);
        Assert.Equal("docs", entry.Requirement.Name);
        Assert.Equal(">=1.0.0", entry.Requirement.VersionRange);
        Assert.Equal("npx", entry.Requirement.InstallMethod);
    }

    // Given a repository declaring no MCP requirements, when they are listed,
    // then the built-in defaults are used and the run continues.
    [Fact]
    public void Given_no_declared_requirements_When_listed_Then_the_built_in_defaults_apply()
    {
        using var repository = new TemporaryRepository();

        var report = Analyse(
            repository,
            """
            {
              "Primer": {
                "McpClients": [ { "Name": "claude-code", "ConfigPath": "absent.json", "Format": "Json" } ]
              }
            }
            """);

        Assert.Equal(McpRequirementDefaults.BuiltIn.Count, report.Entries.Count);
    }

    // Given a declaration missing a required field, when the requirements are read,
    // then it fails naming the offending entry and the outcome is exit 3.
    [Fact]
    public void Given_a_declaration_missing_a_field_When_read_Then_it_fails_with_exit_three()
    {
        using var repository = new TemporaryRepository();
        repository.Write(
            "primer.json",
            """{ "Primer": { "McpRequirements": [ { "VersionRange": ">=1.0.0" } ] } }""");

        using var result = PrimerHostBuilder.Build(new PrimerHostOptions
        {
            RepositoryRoot = repository.Path,
            Output = TextWriter.Null,
            Error = TextWriter.Null,
        });

        Assert.False(result.Succeeded);
        Assert.Equal(ExitCode.Configuration, result.Error.ExitCode);
        Assert.Contains("McpRequirements", result.Error.Subject, StringComparison.Ordinal);
    }

    // Given a client configuration that registers a required server at a version inside the
    // constraint, when the gap is analysed, then it is installed and names the file it was
    // found in.
    [Fact]
    public void Given_a_registered_server_When_analysed_Then_it_is_installed_and_names_its_config()
    {
        using var repository = new TemporaryRepository();
        var path = ClientConfig(
            repository,
            "client.json",
            """{ "mcpServers": { "docs": { "command": "npx", "version": "1.2.0" } } }""");

        var entry = Assert.Single(Analyse(repository, Requiring(path)).Entries);

        Assert.Equal(McpServerStatus.Installed, entry.Status);
        Assert.Contains("client.json", entry.FoundIn!, StringComparison.Ordinal);
    }

    // Given a required server registered at a version outside the constraint, when the gap
    // is analysed, then it is reported as outdated with both versions.
    [Fact]
    public void Given_an_out_of_range_version_When_analysed_Then_it_is_outdated_with_both_versions()
    {
        using var repository = new TemporaryRepository();
        var path = ClientConfig(
            repository,
            "client.json",
            """{ "mcpServers": { "docs": { "command": "npx", "version": "0.4.0" } } }""");

        var entry = Assert.Single(Analyse(repository, Requiring(path, ">=1.0.0")).Entries);

        Assert.Equal(McpServerStatus.Outdated, entry.Status);
        Assert.Equal("0.4.0", entry.FoundVersion);
        Assert.Equal(">=1.0.0", entry.Requirement.VersionRange);
    }

    // Given a required server absent from every known client configuration,
    // when the gap is analysed, then it is reported as missing.
    [Fact]
    public void Given_an_unregistered_server_When_analysed_Then_it_is_missing()
    {
        using var repository = new TemporaryRepository();
        var path = ClientConfig(repository, "client.json", """{ "mcpServers": { } }""");

        Assert.Equal(McpServerStatus.Missing, Assert.Single(Analyse(repository, Requiring(path)).Entries).Status);
    }

    // Given a client configuration file that does not exist on the machine, when the gap is
    // analysed, then that client is reported as not configured and the others are still read.
    [Fact]
    public void Given_a_client_that_is_not_installed_When_analysed_Then_it_is_reported_and_the_run_continues()
    {
        using var repository = new TemporaryRepository();

        var report = Analyse(repository, Requiring("never-installed.json"));

        var client = Assert.Single(report.Clients);
        Assert.False(client.IsConfigured);
        Assert.Single(report.Entries);
    }

    // Given a required server registered without a version, when the gap is analysed, then
    // the fact is reported as unknown rather than assumed, and no network is contacted.
    [Fact]
    public void Given_a_server_registered_without_a_version_When_analysed_Then_the_status_is_unknown()
    {
        using var repository = new TemporaryRepository();
        var path = ClientConfig(
            repository,
            "client.json",
            """{ "mcpServers": { "docs": { "command": "npx" } } }""");

        Assert.Equal(McpServerStatus.Unknown, Assert.Single(Analyse(repository, Requiring(path)).Entries).Status);
    }

    // Given a malformed client configuration file, when the gap is analysed, then the parse
    // failure is reported with the file path and the file is left untouched.
    [Fact]
    public void Given_a_malformed_client_config_When_analysed_Then_it_fails_naming_the_path()
    {
        using var repository = new TemporaryRepository();
        const string Malformed = "{ this is not json";
        var path = ClientConfig(repository, "client.json", Malformed);

        using var result = PrimerHostBuilder.Build(new PrimerHostOptions
        {
            RepositoryRoot = repository.Path,
            Output = TextWriter.Null,
            Error = TextWriter.Null,
        });

        repository.Write("primer.json", Requiring(path));

        using var configured = PrimerHostBuilder.Build(new PrimerHostOptions
        {
            RepositoryRoot = repository.Path,
            Output = TextWriter.Null,
            Error = TextWriter.Null,
        });

        var analyzer = configured.Host.Services.GetRequiredService<McpGapAnalyzer>();

        var failure = Assert.Throws<MalformedClientConfigException>(analyzer.Analyze);

        Assert.Equal(ExitCode.Configuration, failure.ExitCode);
        Assert.Contains("client.json", failure.Error.Subject, StringComparison.Ordinal);
        Assert.Equal(Malformed, repository.Read("client.json"));
    }

    // Given one or more missing or outdated required servers, when the gap is analysed,
    // then the outcome is exit 4 and each names the exact remediation command.
    [Fact]
    public void Given_a_gap_When_analysed_Then_the_outcome_is_four_and_remediation_is_named()
    {
        using var repository = new TemporaryRepository();
        var path = ClientConfig(repository, "client.json", """{ "mcpServers": { } }""");

        var report = Analyse(repository, Requiring(path));

        Assert.True(report.HasGap);
        Assert.Equal(ExitCode.Verification, report.ExitCode);
        Assert.Contains("docs", Assert.Single(report.Entries).RemediationCommand, StringComparison.Ordinal);
    }

    // Given every required server installed and in range, when the gap is analysed,
    // then the outcome is exit 0.
    [Fact]
    public void Given_every_requirement_satisfied_When_analysed_Then_the_outcome_is_success()
    {
        using var repository = new TemporaryRepository();
        var path = ClientConfig(
            repository,
            "client.json",
            """{ "mcpServers": { "docs": { "command": "npx", "version": "1.5.0" } } }""");

        var report = Analyse(repository, Requiring(path));

        Assert.False(report.HasGap);
        Assert.Equal(ExitCode.Success, report.ExitCode);
    }
}
