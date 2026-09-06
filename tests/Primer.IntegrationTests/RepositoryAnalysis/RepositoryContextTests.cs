// Acceptance Test
// Traces to: L2-008, L2-009, L2-010, L2-011, L2-012, L2-047, L2-049
// Description: Verify repository facts are derived by reading files only: stacks detected
//              from markers, commands inferred exactly, structure bounded and ignore-aware,
//              declared conventions reused, secrets excluded, and no process ever started.

using Microsoft.Extensions.DependencyInjection;
using Primer.IntegrationTests.TestSupport;
using Primer.Shared.Analysis;
using Primer.Shared.Hosting;

namespace Primer.IntegrationTests.RepositoryAnalysis;

public sealed class RepositoryContextTests
{
    private static RepositoryContext Analyse(TemporaryRepository repository, TextWriter? error = null)
    {
        using var result = PrimerHostBuilder.Build(new PrimerHostOptions
        {
            RepositoryRoot = repository.Path,
            Output = TextWriter.Null,
            Error = error ?? TextWriter.Null,
        });

        Assert.True(result.Succeeded);

        var analyzer = result.Host.Services.GetRequiredService<IRepositoryAnalyzer>();
        var root = new GitRepositoryLocator().Locate(repository.Path);
        return analyzer.Analyze(root);
    }

    private const string CsprojWithXunit = """
        <Project Sdk="Microsoft.NET.Sdk">
          <ItemGroup>
            <PackageReference Include="xunit.v3" Version="4.0.0" />
          </ItemGroup>
        </Project>
        """;

    // Given a repository containing a .sln and one or more .csproj files, when detection
    // runs, then the detected stack includes dotnet and lists the discovered project files.
    [Fact]
    public void Given_a_dotnet_repository_When_detected_Then_the_stack_lists_the_project_files()
    {
        using var repository = new TemporaryRepository();
        repository.Write("Primer.sln", "Microsoft Visual Studio Solution File");
        repository.Write("src/Primer/Primer.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />");

        var stacks = Analyse(repository).Stacks;

        var dotnet = Assert.Single(stacks, stack => stack.Name == "dotnet");
        Assert.Contains(dotnet.MarkerFiles, marker => marker.EndsWith("Primer.csproj", StringComparison.Ordinal));
    }

    // Given a repository containing a .csproj that references the xunit package,
    // when detection runs, then the detected test framework is reported as xunit.
    [Fact]
    public void Given_a_project_referencing_xunit_When_detected_Then_the_test_framework_is_xunit()
    {
        using var repository = new TemporaryRepository();
        repository.Write("Primer.sln", "Microsoft Visual Studio Solution File");
        repository.Write("tests/Primer.Tests/Primer.Tests.csproj", CsprojWithXunit);

        var dotnet = Assert.Single(Analyse(repository).Stacks, stack => stack.Name == "dotnet");

        Assert.Equal("xunit", dotnet.TestFramework);
    }

    // Given a repository containing package.json, when detection runs, then the detected
    // stack includes node and names the package manager implied by the lock file.
    [Theory]
    [InlineData("package-lock.json", "npm")]
    [InlineData("pnpm-lock.yaml", "pnpm")]
    [InlineData("yarn.lock", "yarn")]
    public void Given_a_node_repository_When_detected_Then_the_lock_file_names_the_package_manager(
        string lockFile,
        string expectedManager)
    {
        using var repository = new TemporaryRepository();
        repository.Write("package.json", """{ "name": "app" }""");
        repository.Write(lockFile, "");

        var node = Assert.Single(Analyse(repository).Stacks, stack => stack.Name == "node");

        Assert.Equal(expectedManager, node.PackageManager);
    }

    // Given a repository containing no recognised marker file, when detection runs,
    // then the detected stack is reported as empty.
    [Fact]
    public void Given_no_markers_When_detected_Then_no_stack_is_reported()
    {
        using var repository = new TemporaryRepository();
        repository.Write("notes.txt", "nothing to detect");

        Assert.Empty(Analyse(repository).Stacks);
    }

    // Given a repository containing markers for more than one stack, when detection runs,
    // then every detected stack is reported and none is silently discarded.
    [Fact]
    public void Given_markers_for_two_stacks_When_detected_Then_both_are_reported()
    {
        using var repository = new TemporaryRepository();
        repository.Write("Primer.sln", "Microsoft Visual Studio Solution File");
        repository.Write("package.json", """{ "name": "app" }""");
        repository.Write("package-lock.json", "");

        var names = Analyse(repository).Stacks.Select(stack => stack.Name).ToList();

        Assert.Contains("dotnet", names);
        Assert.Contains("node", names);
    }

    // Given a repository whose root contains a solution file, when command inference runs,
    // then the build and test commands name the actual solution and carry their flags.
    [Fact]
    public void Given_a_solution_When_commands_are_inferred_Then_they_are_exact_and_carry_flags()
    {
        using var repository = new TemporaryRepository();
        repository.Write("Primer.sln", "Microsoft Visual Studio Solution File");

        var commands = Analyse(repository).Commands;

        var build = Assert.Single(commands, command => command.Role == CommandRole.Build);
        var test = Assert.Single(commands, command => command.Role == CommandRole.Test);
        Assert.Equal("dotnet build Primer.sln -c Release", build.Invocation);
        Assert.Equal("dotnet test Primer.sln -c Release", test.Invocation);
    }

    // Given a repository with a package.json whose scripts define test and lint, when
    // inference runs, then the commands invoke those scripts through the package manager.
    [Fact]
    public void Given_package_scripts_When_commands_are_inferred_Then_they_use_the_package_manager()
    {
        using var repository = new TemporaryRepository();
        repository.Write(
            "package.json",
            """{ "name": "app", "scripts": { "test": "vitest", "lint": "eslint ." } }""");
        repository.Write("pnpm-lock.yaml", "");

        var commands = Analyse(repository).Commands;

        Assert.Equal("pnpm run test", Assert.Single(commands, c => c.Role == CommandRole.Test).Invocation);
        Assert.Equal("pnpm run lint", Assert.Single(commands, c => c.Role == CommandRole.Lint).Invocation);
    }

    // Given a repository where no test command can be inferred, when analysis runs, then
    // no test command is reported rather than a placeholder standing in for one.
    [Fact]
    public void Given_nothing_to_infer_a_test_command_from_When_analysed_Then_none_is_reported()
    {
        using var repository = new TemporaryRepository();
        repository.Write("notes.txt", "nothing to infer");

        var commands = Analyse(repository).Commands;

        Assert.DoesNotContain(commands, command => command.Role == CommandRole.Test);
        Assert.DoesNotContain(commands, command => command.Invocation.Contains('<', StringComparison.Ordinal));
    }

    // Given any inferred command, when it is reported,
    // then every file path it references exists in the repository.
    [Fact]
    public void Given_an_inferred_command_When_reported_Then_every_path_it_names_exists()
    {
        using var repository = new TemporaryRepository();
        repository.Write("Primer.sln", "Microsoft Visual Studio Solution File");

        var context = Analyse(repository);

        Assert.All(
            context.Commands.SelectMany(command => command.ReferencedPaths),
            relative => Assert.True(
                repository.Exists(relative),
                $"Inferred command referenced a path that does not exist: {relative}"));
    }

    // Given a repository containing workflow files, when inference runs, then the commands
    // those workflows execute are preferred over inferred defaults.
    [Fact]
    public void Given_a_workflow_When_commands_are_inferred_Then_the_workflow_command_is_preferred()
    {
        using var repository = new TemporaryRepository();
        repository.Write("Primer.sln", "Microsoft Visual Studio Solution File");
        repository.Write(
            ".github/workflows/ci.yml",
            """
            jobs:
              build:
                steps:
                  - run: dotnet build Primer.sln -c Release --no-incremental
                  - run: dotnet test Primer.sln -c Release --collect:"XPlat Code Coverage"
            """);

        var build = Assert.Single(Analyse(repository).Commands, c => c.Role == CommandRole.Build);

        Assert.Contains("--no-incremental", build.Invocation, StringComparison.Ordinal);
    }

    // Given a repository containing .editorconfig and Directory.Build.props, when detection
    // runs, then both are recorded among the discovered convention files.
    [Fact]
    public void Given_declared_conventions_When_detected_Then_they_are_recorded_by_relative_path()
    {
        using var repository = new TemporaryRepository();
        repository.Write(".editorconfig", "root = true");
        repository.Write("Directory.Build.props", "<Project />");

        var conventions = Analyse(repository).Conventions;

        Assert.Contains(conventions, c => c.RelativePath == ".editorconfig");
        Assert.Contains(conventions, c => c.RelativePath == "Directory.Build.props");
    }

    // Given a repository containing none of the recognised convention files,
    // when detection runs, then none is reported.
    [Fact]
    public void Given_no_convention_files_When_detected_Then_none_is_reported()
    {
        using var repository = new TemporaryRepository();
        repository.Write("notes.txt", "nothing declared");

        Assert.Empty(Analyse(repository).Conventions);
    }

    // Given a repository with a .gitignore excluding bin/ and obj/, when structure
    // discovery runs, then no path under those directories appears in the structure.
    [Fact]
    public void Given_gitignored_directories_When_scanned_Then_they_are_absent_from_the_structure()
    {
        using var repository = new TemporaryRepository();
        repository.Write(".gitignore", "bin/\nobj/\n");
        repository.Write("src/Primer/Primer.csproj", "<Project />");
        repository.Write("src/Primer/bin/Release/primer.dll", "binary-ish");
        repository.Write("src/Primer/obj/project.assets.json", "{}");

        var structure = Analyse(repository).Structure;

        Assert.DoesNotContain(structure.Directories, path => path.Contains("bin", StringComparison.Ordinal));
        Assert.DoesNotContain(structure.Directories, path => path.Contains("obj", StringComparison.Ordinal));
    }

    // Given a repository containing a directory nested deeper than the configured maximum
    // depth, when structure discovery runs, then traversal stops at the maximum depth.
    [Fact]
    public void Given_a_tree_deeper_than_the_cap_When_scanned_Then_traversal_stops_at_the_cap()
    {
        using var repository = new TemporaryRepository();
        repository.Write("a/b/c/d/e/f/deep.txt", "too deep");

        var structure = Analyse(repository).Structure;

        Assert.All(
            structure.Directories,
            path => Assert.True(
                path.Count(character => character == '/') < AnalysisBudget.DefaultMaxDepth,
                $"Structure included a path below the depth cap: {path}"));
    }

    // Given a repository containing a symbolic link that points to an ancestor directory,
    // when discovery runs, then traversal terminates without infinite recursion.
    [Fact]
    public void Given_a_link_cycle_When_scanned_Then_traversal_terminates()
    {
        using var repository = new TemporaryRepository();
        repository.Write("src/file.txt", "content");

        try
        {
            Directory.CreateSymbolicLink(
                Path.Combine(repository.Path, "src", "loop"),
                Path.Combine(repository.Path, "src"));
        }
        catch (Exception creation) when (creation is IOException or UnauthorizedAccessException)
        {
            Assert.Skip("Symbolic links cannot be created in this environment.");
            return;
        }

        // Completing at all is the assertion: a cycle would not terminate.
        Assert.NotNull(Analyse(repository).Structure);
    }

    // Given a repository containing a file larger than the configured per-file read cap,
    // when analysis runs, then its content is not read and it is reported by name and size.
    [Fact]
    public void Given_a_file_over_the_read_cap_When_analysed_Then_it_is_reported_by_name_and_size()
    {
        using var repository = new TemporaryRepository();
        var oversized = new string('x', (int)AnalysisBudget.DefaultMaxFileBytes + 1024);
        repository.Write("large.txt", oversized);

        var skipped = Analyse(repository).SkippedFiles;

        var record = Assert.Single(skipped, file => file.RelativePath == "large.txt");
        Assert.Equal(SkipReason.TooLarge, record.Reason);
        Assert.True(record.SizeBytes > AnalysisBudget.DefaultMaxFileBytes);
    }

    // Given a repository containing a binary file, when analysis runs,
    // then its content is not parsed as text.
    [Fact]
    public void Given_a_binary_file_When_analysed_Then_it_is_not_parsed_as_text()
    {
        using var repository = new TemporaryRepository();
        File.WriteAllBytes(
            Path.Combine(repository.Path, "image.png"),
            [0x89, 0x50, 0x4E, 0x47, 0x00, 0x01, 0x02, 0x00, 0x03]);

        var skipped = Analyse(repository).SkippedFiles;

        Assert.Contains(skipped, file => file.RelativePath == "image.png" && file.Reason == SkipReason.Binary);
    }

    // Given a repository containing a .env file, when analysis runs, then its contents are
    // not read and no value from it appears anywhere in the derived context.
    [Fact]
    public void Given_a_dot_env_file_When_analysed_Then_its_values_never_reach_the_context()
    {
        using var repository = new TemporaryRepository();
        repository.Write(".env", "API_TOKEN=super-secret-value-1234567890\n");

        var context = Analyse(repository);

        Assert.DoesNotContain("super-secret-value", Rendered(context), StringComparison.Ordinal);
    }

    // Given repository content containing a value matching a known credential pattern,
    // when analysis runs, then the matched value is excluded from what is retained.
    [Fact]
    public void Given_a_credential_shaped_value_When_analysed_Then_it_is_redacted()
    {
        using var repository = new TemporaryRepository();
        repository.Write(
            ".github/workflows/ci.yml",
            """
            jobs:
              build:
                steps:
                  - run: dotnet build Primer.sln -c Release --token ghp_ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789
            """);

        var context = Analyse(repository);

        Assert.DoesNotContain("ghp_ABCDEFGHIJKLMNOPQRSTUVWXYZ", Rendered(context), StringComparison.Ordinal);
    }

    // Given a repository whose package manifest declares lifecycle scripts, when analysis
    // runs, then those scripts are read as data and never executed.
    [Fact]
    public void Given_lifecycle_scripts_When_analysed_Then_they_are_read_as_data_not_executed()
    {
        using var repository = new TemporaryRepository();
        var canary = Path.Combine(Path.GetTempPath(), "primer-canary-" + Guid.NewGuid().ToString("N"));
        repository.Write(
            "package.json",
            $$"""
            { "name": "app", "scripts": { "postinstall": "node -e \"require('fs').writeFileSync('{{canary.Replace("\\", "/")}}','ran')\"" } }
            """);
        repository.Write("package-lock.json", "");

        Analyse(repository);

        Assert.False(File.Exists(canary), "A repository lifecycle script was executed during analysis.");
    }

    // Given the analysis source, when it is inspected, then it starts no child process.
    [Fact]
    public void Given_the_analysis_source_When_inspected_Then_it_references_no_process_type()
    {
        var analysisDirectory = Path.Combine(RepositoryPaths.Root, "src", "Primer", "Shared", "Analysis");

        var offenders = Directory
            .EnumerateFiles(analysisDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(file => File.ReadAllText(file).Contains("System.Diagnostics.Process", StringComparison.Ordinal)
                || File.ReadAllText(file).Contains("Process.Start", StringComparison.Ordinal))
            .ToList();

        Assert.Empty(offenders);
    }

    /// <summary>Everything the derived context would expose, flattened for scanning.</summary>
    private static string Rendered(RepositoryContext context) =>
        string.Join(
            '\n',
            context.Stacks.SelectMany(stack => stack.MarkerFiles)
                .Concat(context.Commands.Select(command => command.Invocation))
                .Concat(context.Commands.SelectMany(command => command.ReferencedPaths))
                .Concat(context.Structure.Directories)
                .Concat(context.Conventions.Select(convention => convention.RelativePath))
                .Concat(context.SkippedFiles.Select(file => file.RelativePath)));
}
