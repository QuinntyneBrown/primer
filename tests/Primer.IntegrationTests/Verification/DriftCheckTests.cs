// Acceptance Test
// Traces to: L2-027
// Description: Verify drift is detected by regenerating and comparing, that missing and
//              divergent files are named, and that verification writes nothing under any
//              outcome.

using Microsoft.Extensions.DependencyInjection;
using Primer.IntegrationTests.TestSupport;
using Primer.Shared.Analysis;
using Primer.Shared.Generation;
using Primer.Shared.Hosting;
using Primer.Shared.Verification;

namespace Primer.IntegrationTests.Verification;

public sealed class DriftCheckTests
{
    private static TemporaryRepository DotnetRepository()
    {
        var repository = new TemporaryRepository();
        repository.Write("Primer.sln", "Microsoft Visual Studio Solution File");
        repository.Write(".editorconfig", "root = true\n");
        return repository;
    }

    /// <summary>Generates and writes the files a real run would leave behind.</summary>
    private static void Materialise(TemporaryRepository repository)
    {
        using var result = Host(repository);
        var services = result.Host.Services;

        var context = services.GetRequiredService<IRepositoryAnalyzer>()
            .Analyze(new GitRepositoryLocator().Locate(repository.Path));

        var generated = services.GetRequiredService<IAgentsFileGenerator>().Generate(context, recursive: false)
            .Concat(AgentSelection.Default().Targets.Select(PointerFileGenerator.Generate).OfType<GeneratedFile>())
            .ToList();

        var plan = services.GetRequiredService<OverwritePolicy>().Plan(generated, force: false);
        services.GetRequiredService<IFileWriter>().Apply(plan);
    }

    private static DriftReport Check(TemporaryRepository repository)
    {
        using var result = Host(repository);
        return result.Host.Services.GetRequiredService<DriftChecker>().Compare(recursive: false);
    }

    private static HostBuildResult Host(TemporaryRepository repository)
    {
        var result = PrimerHostBuilder.Build(new PrimerHostOptions
        {
            RepositoryRoot = repository.Path,
            Output = TextWriter.Null,
            Error = TextWriter.Null,
        });

        Assert.True(result.Succeeded);
        return result;
    }

    private static Dictionary<string, DateTime> Snapshot(TemporaryRepository repository) =>
        Directory
            .EnumerateFiles(repository.Path, "*", SearchOption.AllDirectories)
            .ToDictionary(path => path, File.GetLastWriteTimeUtc, StringComparer.Ordinal);

    // Given a repository whose generated files match what generation would currently
    // produce, when the check runs, then it reports no drift and modifies nothing.
    [Fact]
    public void Given_current_files_When_checked_Then_no_drift_is_reported()
    {
        using var repository = DotnetRepository();
        Materialise(repository);
        var before = Snapshot(repository);

        var report = Check(repository);

        Assert.False(report.HasDrift);
        Assert.Equal(ExitCode.Success, report.ExitCode);
        Assert.All(report.Entries, entry => Assert.Equal(DriftKind.Current, entry.Kind));
        Assert.Equal(before, Snapshot(repository));
    }

    // Given a repository whose AGENTS.md no longer matches current generation output,
    // when the check runs, then the differing path is reported and the outcome is exit 4.
    [Fact]
    public void Given_a_divergent_file_When_checked_Then_it_is_named_with_exit_four()
    {
        using var repository = DotnetRepository();
        Materialise(repository);
        repository.Write("AGENTS.md", ManagedRegion.Wrap("# Stale", "0.0.1", "old-hash") + "\n");

        var report = Check(repository);

        Assert.True(report.HasDrift);
        Assert.Equal(ExitCode.Verification, report.ExitCode);
        Assert.Contains(report.Entries, entry => entry.RelativePath == "AGENTS.md" && entry.Kind == DriftKind.Divergent);
    }

    // Given a repository with no generated files at all, when the check runs,
    // then the missing paths are reported and the outcome is exit 4.
    [Fact]
    public void Given_missing_files_When_checked_Then_they_are_named_with_exit_four()
    {
        using var repository = DotnetRepository();

        var report = Check(repository);

        Assert.True(report.HasDrift);
        Assert.Equal(ExitCode.Verification, report.ExitCode);
        Assert.Contains(report.Entries, entry => entry.RelativePath == "AGENTS.md" && entry.Kind == DriftKind.Missing);
    }

    // Given any invocation of the check, when it completes,
    // then no file in the repository has been created, modified, or deleted.
    [Fact]
    public void Given_any_outcome_When_checked_Then_nothing_in_the_repository_changes()
    {
        using var repository = DotnetRepository();
        Materialise(repository);
        repository.Write("AGENTS.md", ManagedRegion.Wrap("# Stale", "0.0.1", "old-hash") + "\n");
        var before = Snapshot(repository);

        Check(repository);

        Assert.Equal(before, Snapshot(repository));
    }

    // Given a repository whose AGENTS.md carries no managed region, when the check runs,
    // then it is reported as divergent rather than failing the run.
    [Fact]
    public void Given_an_unmanaged_file_When_checked_Then_it_is_reported_divergent()
    {
        using var repository = DotnetRepository();
        repository.Write("AGENTS.md", "# Entirely hand written\n");

        var report = Check(repository);

        Assert.Contains(report.Entries, entry => entry.RelativePath == "AGENTS.md" && entry.Kind == DriftKind.Divergent);
    }
}
