// Acceptance Test
// Traces to: L2-034, L2-035
// Description: Verify settings resolve in the order defaults, primer.json, PRIMER_
//              environment variables, command-line arguments, and that options are
//              validated before any file is read or written.

using Microsoft.Extensions.DependencyInjection;
using Primer.IntegrationTests.TestSupport;
using Primer.Shared.Hosting;

namespace Primer.IntegrationTests.ResolveConfiguration;

public sealed class ConfigurationPrecedenceTests
{
    private static PrimerHostOptions Options(
        TemporaryRepository repository,
        IReadOnlyDictionary<string, string?>? environment = null,
        IReadOnlyDictionary<string, string?>? commandLine = null) =>
        new()
        {
            RepositoryRoot = repository.Path,
            Environment = environment ?? new Dictionary<string, string?>(StringComparer.Ordinal),
            CommandLineOverrides = commandLine ?? new Dictionary<string, string?>(StringComparer.Ordinal),
            Output = TextWriter.Null,
            Error = TextWriter.Null,
        };

    private const string DepthSevenFile = """{ "Primer": { "Budget": { "MaxDepth": 7 } } }""";

    // Given a setting defined only by built-in defaults, when a command is run,
    // then the default value is used.
    [Fact]
    public void Given_only_defaults_When_options_are_resolved_Then_the_default_value_is_used()
    {
        using var repository = new TemporaryRepository();
        using var result = PrimerHostBuilder.Build(Options(repository));

        Assert.True(result.Succeeded);
        Assert.Equal(AnalysisBudget.DefaultMaxDepth, result.Host.Options().Budget.MaxDepth);
    }

    // Given the same setting defined in a configuration file and by a built-in default,
    // when a command is run, then the configuration file value is used.
    [Fact]
    public void Given_a_configuration_file_When_options_are_resolved_Then_the_file_overrides_the_default()
    {
        using var repository = new TemporaryRepository();
        repository.Write("primer.json", DepthSevenFile);

        using var result = PrimerHostBuilder.Build(Options(repository));

        Assert.True(result.Succeeded);
        Assert.Equal(7, result.Host.Options().Budget.MaxDepth);
    }

    // Given the same setting defined in a configuration file and in a PRIMER_ prefixed
    // environment variable, when a command is run, then the environment value is used.
    [Fact]
    public void Given_an_environment_variable_When_options_are_resolved_Then_it_overrides_the_file()
    {
        using var repository = new TemporaryRepository();
        repository.Write("primer.json", DepthSevenFile);

        var environment = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["PRIMER_Budget__MaxDepth"] = "9",
        };

        using var result = PrimerHostBuilder.Build(Options(repository, environment));

        Assert.True(result.Succeeded);
        Assert.Equal(9, result.Host.Options().Budget.MaxDepth);
    }

    // Given the same setting defined in an environment variable and as a command-line
    // argument, when a command is run, then the command-line value is used.
    [Fact]
    public void Given_a_command_line_override_When_options_are_resolved_Then_it_wins_over_everything()
    {
        using var repository = new TemporaryRepository();
        repository.Write("primer.json", DepthSevenFile);

        var environment = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["PRIMER_Budget__MaxDepth"] = "9",
        };
        var commandLine = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Primer:Budget:MaxDepth"] = "11",
        };

        using var result = PrimerHostBuilder.Build(Options(repository, environment, commandLine));

        Assert.True(result.Succeeded);
        Assert.Equal(11, result.Host.Options().Budget.MaxDepth);
    }

    // Given --verbosity diagnostic, when a command is run, then the effective value and
    // originating source of each setting is written to stderr.
    [Fact]
    public void Given_diagnostic_verbosity_When_settings_are_reported_Then_each_names_its_source()
    {
        using var repository = new TemporaryRepository();
        repository.Write("primer.json", DepthSevenFile);

        using var error = new StringWriter();
        var options = Options(repository) with { Error = error, Verbosity = VerbosityLevel.Diagnostic };

        using (var result = PrimerHostBuilder.Build(options))
        {
            Assert.True(result.Succeeded);
            result.Host.Services.GetRequiredService<EffectiveSettingsReporter>().Report();
        }

        var written = error.ToString();
        Assert.Contains("MaxDepth", written, StringComparison.Ordinal);
        Assert.Contains("7", written, StringComparison.Ordinal);
        Assert.Contains("primer.json", written, StringComparison.OrdinalIgnoreCase);
    }

    // Given a configuration value that violates its declared constraint, when any command
    // is run, then validation fails before any file is read or written, the failure names
    // the offending key and constraint, and the outcome is exit 3.
    [Fact]
    public void Given_an_invalid_value_When_options_are_validated_Then_it_fails_naming_the_key_with_exit_three()
    {
        using var repository = new TemporaryRepository();
        repository.Write("primer.json", """{ "Primer": { "Budget": { "MaxDepth": 0 } } }""");

        using var result = PrimerHostBuilder.Build(Options(repository));

        Assert.False(result.Succeeded);
        Assert.Equal(ExitCode.Configuration, result.Error.ExitCode);
        Assert.Contains("MaxDepth", result.Error.Subject, StringComparison.Ordinal);
    }

    // Given a configuration file that is not valid JSON, when any command is run,
    // then the failure names the file and the outcome is exit 3.
    [Fact]
    public void Given_malformed_json_When_options_are_resolved_Then_it_fails_naming_the_file_with_exit_three()
    {
        using var repository = new TemporaryRepository();
        repository.Write("primer.json", "{ this is not json");

        using var result = PrimerHostBuilder.Build(Options(repository));

        Assert.False(result.Succeeded);
        Assert.Equal(ExitCode.Configuration, result.Error.ExitCode);
        Assert.Contains("primer.json", result.Error.Subject, StringComparison.OrdinalIgnoreCase);
    }

    // Given a configuration file containing an unrecognised key, when any command is run,
    // then a warning naming the key is written to stderr and the process continues.
    [Fact]
    public void Given_an_unrecognised_key_When_options_are_resolved_Then_it_warns_and_continues()
    {
        using var repository = new TemporaryRepository();
        repository.Write("primer.json", """{ "Primer": { "Budgey": { "MaxDepth": 4 } } }""");

        using var error = new StringWriter();
        using var result = PrimerHostBuilder.Build(Options(repository) with { Error = error });

        Assert.True(result.Succeeded);
        Assert.Contains("Budgey", error.ToString(), StringComparison.Ordinal);
    }

    // Given a valid configuration, when any command is run, then options are bound to
    // strongly-typed classes registered through the options pattern.
    [Fact]
    public void Given_a_valid_configuration_When_resolved_Then_options_bind_through_the_options_pattern()
    {
        using var repository = new TemporaryRepository();
        repository.Write(
            "primer.json",
            """{ "Primer": { "TemplatePath": "templates", "Budget": { "MaxFileCount": 1234 } } }""");

        using var result = PrimerHostBuilder.Build(Options(repository));

        Assert.True(result.Succeeded);

        var options = result.Host.Options();
        Assert.Equal("templates", options.TemplatePath);
        Assert.Equal(1234, options.Budget.MaxFileCount);
        Assert.Equal(AnalysisBudget.DefaultMaxDepth, options.Budget.MaxDepth);
    }
}
