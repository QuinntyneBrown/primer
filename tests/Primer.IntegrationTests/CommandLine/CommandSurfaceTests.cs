// Acceptance Test
// Traces to: L2-003, L2-004, L2-005, L2-006, L2-007, L2-036, L2-057
// Description: Verify help lists every command and option, that the exit-code contract
//              holds, that global options are accepted by every command, and that mistyped
//              input fails fast with a suggestion and changes nothing.

using Primer.IntegrationTests.TestSupport;
using Primer.Shared.Hosting;

namespace Primer.IntegrationTests.CommandLine;

public sealed class CommandSurfaceTests
{
    private static Task<PrimerCliHarness.Result> Run(TemporaryRepository repository, params string[] args) =>
        PrimerCliHarness.RunAsync([.. args, "--path", repository.Path]);

    private static Dictionary<string, DateTime> Snapshot(TemporaryRepository repository) =>
        Directory
            .EnumerateFiles(repository.Path, "*", SearchOption.AllDirectories)
            .ToDictionary(path => path, File.GetLastWriteTimeUtc, StringComparer.Ordinal);

    // Given an installed tool, when it is run with no arguments, then help listing the
    // commands init, check, and mcp is written to stdout and the process exits 0.
    [Fact]
    public async Task Given_no_arguments_When_run_Then_help_lists_every_command_with_success()
    {
        var result = await PrimerCliHarness.RunAsync();

        Assert.Equal((int)ExitCode.Success, result.ExitCode);
        Assert.Contains("init", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("check", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("mcp", result.StandardOutput, StringComparison.Ordinal);
    }

    // Given --help, when it is run, then every global option appears with a description.
    [Fact]
    public async Task Given_help_When_run_Then_every_global_option_is_described()
    {
        var result = await PrimerCliHarness.RunAsync("--help");

        Assert.Equal((int)ExitCode.Success, result.ExitCode);

        foreach (var option in (string[])["--path", "--verbosity", "--format", "--no-color", "--dry-run", "--yes"])
        {
            Assert.Contains(option, result.StandardOutput, StringComparison.Ordinal);
        }
    }

    // Given a subcommand's help, when it is run, then that command's own options appear.
    [Fact]
    public async Task Given_init_help_When_run_Then_the_init_options_are_described()
    {
        var result = await PrimerCliHarness.RunAsync("init", "--help");

        Assert.Equal((int)ExitCode.Success, result.ExitCode);
        Assert.Contains("--agent", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("--recursive", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("--prompt", result.StandardOutput, StringComparison.Ordinal);
    }

    // Given the mcp command's help, when it is run, then its subcommands are listed.
    [Fact]
    public async Task Given_mcp_help_When_run_Then_the_subcommands_are_listed()
    {
        var result = await PrimerCliHarness.RunAsync("mcp", "--help");

        Assert.Equal((int)ExitCode.Success, result.ExitCode);
        Assert.Contains("list", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("check", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("install", result.StandardOutput, StringComparison.Ordinal);
    }

    // Given a valid repository, when init completes successfully, then the process exits 0.
    [Fact]
    public async Task Given_a_valid_repository_When_init_runs_Then_it_exits_zero()
    {
        using var repository = new TemporaryRepository();
        repository.Write("Primer.sln", "Microsoft Visual Studio Solution File");

        var result = await Run(repository, "init");

        Assert.Equal((int)ExitCode.Success, result.ExitCode);
        Assert.True(repository.Exists("AGENTS.md"));
        Assert.Equal("@AGENTS.md\n", repository.Read("CLAUDE.md"));
    }

    // Given an unrecognised option, when the tool is run, then a parse error is written to
    // stderr and the process exits 2.
    [Fact]
    public async Task Given_an_unrecognised_option_When_run_Then_it_exits_two()
    {
        using var repository = new TemporaryRepository();

        var result = await Run(repository, "init", "--agnet", "claude");

        Assert.Equal((int)ExitCode.Usage, result.ExitCode);
        Assert.Contains("--agnet", result.StandardError, StringComparison.Ordinal);
    }

    // Given a mistyped command, when the tool is run, then stderr names it as unrecognised
    // and suggests the nearest match, and the process exits 2.
    [Fact]
    public async Task Given_a_mistyped_command_When_run_Then_the_nearest_match_is_suggested()
    {
        using var repository = new TemporaryRepository();

        var result = await Run(repository, "inti");

        Assert.Equal((int)ExitCode.Usage, result.ExitCode);
        Assert.Contains("inti", result.StandardError, StringComparison.Ordinal);
        Assert.Contains("init", result.StandardError, StringComparison.Ordinal);
    }

    // Given any parse failure, when the tool is run,
    // then no file in the target repository is created, modified, or deleted.
    [Fact]
    public async Task Given_a_parse_failure_When_run_Then_the_repository_is_untouched()
    {
        using var repository = new TemporaryRepository();
        repository.Write("Primer.sln", "Microsoft Visual Studio Solution File");
        var before = Snapshot(repository);

        await Run(repository, "inti");
        await Run(repository, "init", "--agnet", "claude");

        Assert.Equal(before, Snapshot(repository));
    }

    // Given a configuration file with an invalid value, when any command is run,
    // then a validation error is written to stderr and the process exits 3.
    [Fact]
    public async Task Given_an_invalid_configuration_When_run_Then_it_exits_three()
    {
        using var repository = new TemporaryRepository();
        repository.Write("primer.json", """{ "Primer": { "Budget": { "MaxDepth": 0 } } }""");

        var result = await Run(repository, "init");

        Assert.Equal((int)ExitCode.Configuration, result.ExitCode);
        Assert.Contains("MaxDepth", result.StandardError, StringComparison.Ordinal);
    }

    // Given a repository whose generated files are out of date, when check is run,
    // then the process exits 4.
    [Fact]
    public async Task Given_drifted_files_When_check_runs_Then_it_exits_four()
    {
        using var repository = new TemporaryRepository();
        repository.Write("Primer.sln", "Microsoft Visual Studio Solution File");

        var result = await Run(repository, "check");

        Assert.Equal((int)ExitCode.Verification, result.ExitCode);
        Assert.Contains("AGENTS.md", result.StandardOutput, StringComparison.Ordinal);
    }

    // Given a repository whose generated files are current, when check is run,
    // then the process exits 0 and nothing is modified.
    [Fact]
    public async Task Given_current_files_When_check_runs_Then_it_exits_zero_and_changes_nothing()
    {
        using var repository = new TemporaryRepository();
        repository.Write("Primer.sln", "Microsoft Visual Studio Solution File");
        await Run(repository, "init");
        var before = Snapshot(repository);

        var result = await Run(repository, "check");

        Assert.Equal((int)ExitCode.Success, result.ExitCode);
        Assert.Equal(before, Snapshot(repository));
    }

    // Given --path pointing at a directory that is not in a repository,
    // when a command is run, then it exits 3 naming the path.
    [Fact]
    public async Task Given_a_path_outside_any_repository_When_run_Then_it_exits_three()
    {
        var outside = Path.Combine(Path.GetTempPath(), "primer-none-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outside);

        try
        {
            var result = await PrimerCliHarness.RunAsync("init", "--path", outside);

            Assert.Equal((int)ExitCode.Configuration, result.ExitCode);
            Assert.Contains("repository", result.StandardError, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(outside, recursive: true);
        }
    }

    // Given --dry-run, when init runs, then the intended content is reported and no file is
    // written. Every command accepts the global options with identical semantics.
    [Fact]
    public async Task Given_dry_run_When_init_runs_Then_nothing_is_written()
    {
        using var repository = new TemporaryRepository();
        repository.Write("Primer.sln", "Microsoft Visual Studio Solution File");

        var result = await Run(repository, "init", "--dry-run");

        Assert.Equal((int)ExitCode.Success, result.ExitCode);
        Assert.Contains("AGENTS.md", result.StandardOutput, StringComparison.Ordinal);
        Assert.False(repository.Exists("AGENTS.md"));
    }

    // Given --format json on a command, when it completes,
    // then stdout is a single JSON document.
    [Fact]
    public async Task Given_json_format_When_a_command_runs_Then_stdout_is_one_json_document()
    {
        using var repository = new TemporaryRepository();
        repository.Write("Primer.sln", "Microsoft Visual Studio Solution File");

        var result = await Run(repository, "mcp", "list", "--format", "json");

        Assert.Equal((int)ExitCode.Success, result.ExitCode);
        using var document = System.Text.Json.JsonDocument.Parse(result.StandardOutput);
        Assert.True(document.RootElement.TryGetProperty("schemaVersion", out _));
    }

    // Given --verbosity quiet on a successful run, then both streams are empty.
    [Fact]
    public async Task Given_quiet_verbosity_When_a_command_succeeds_Then_both_streams_are_empty()
    {
        using var repository = new TemporaryRepository();
        repository.Write("Primer.sln", "Microsoft Visual Studio Solution File");

        var result = await Run(repository, "init", "--verbosity", "quiet");

        Assert.Equal((int)ExitCode.Success, result.ExitCode);
        Assert.Empty(result.StandardOutput);
        Assert.Empty(result.StandardError);
    }

    // Given an unsupported --agent value, when init runs,
    // then it exits 2 and no file is written.
    [Fact]
    public async Task Given_an_unsupported_agent_When_init_runs_Then_it_exits_two_and_writes_nothing()
    {
        using var repository = new TemporaryRepository();
        repository.Write("Primer.sln", "Microsoft Visual Studio Solution File");

        var result = await Run(repository, "init", "--agent", "notarealagent");

        Assert.Equal((int)ExitCode.Usage, result.ExitCode);
        Assert.False(repository.Exists("AGENTS.md"));
    }

    // Given `primer init` run twice, when the second run completes,
    // then every path is reported unchanged.
    [Fact]
    public async Task Given_a_repeated_run_When_init_runs_again_Then_it_reports_unchanged()
    {
        using var repository = new TemporaryRepository();
        repository.Write("Primer.sln", "Microsoft Visual Studio Solution File");

        await Run(repository, "init");
        var result = await Run(repository, "init");

        Assert.Equal((int)ExitCode.Success, result.ExitCode);
        Assert.Contains("unchanged", result.StandardOutput, StringComparison.OrdinalIgnoreCase);
    }
}
