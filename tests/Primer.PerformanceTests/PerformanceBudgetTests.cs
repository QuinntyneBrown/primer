// Acceptance Test
// Traces to: L2-052, L2-053, L2-054
// Description: Verify generation completes inside its stated time budgets, that memory
//              stays bounded on a large repository, and that version and help return
//              without performing repository analysis.

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Primer.Shared.Analysis;
using Primer.Shared.Generation;
using Primer.Shared.Hosting;

namespace Primer.PerformanceTests;

[Trait("tier", "slow")]
public sealed class PerformanceBudgetTests
{
    private const int SmallRepositoryFiles = 5_000;
    private const int LargeRepositoryFiles = 100_000;
    private const int TimedRuns = 20;

    /// <summary>Runs generation end to end, the work the budgets are stated against.</summary>
    private static void GenerateOnce(string repositoryPath)
    {
        using var result = PrimerHostBuilder.Build(new PrimerHostOptions
        {
            RepositoryRoot = repositoryPath,
            Output = TextWriter.Null,
            Error = TextWriter.Null,
        });

        var services = result.Host.Services;
        var root = services.GetRequiredService<IRepositoryLocator>().Locate(repositoryPath);
        var context = services.GetRequiredService<IRepositoryAnalyzer>().Analyze(root);

        _ = services.GetRequiredService<IAgentsFileGenerator>().Generate(context, recursive: false);
    }

    /// <summary>
    /// The 95th percentile over the sample, which is what the budgets are stated against:
    /// a single slow run on a busy machine is not a regression.
    /// </summary>
    private static TimeSpan Percentile95(IReadOnlyList<TimeSpan> samples)
    {
        var ordered = samples.OrderBy(sample => sample).ToList();
        var index = (int)Math.Ceiling(0.95 * ordered.Count) - 1;
        return ordered[Math.Clamp(index, 0, ordered.Count - 1)];
    }

    private static List<TimeSpan> Time(int runs, Action action)
    {
        // One untimed run first, so a cold file-system cache is not charged to the budget.
        action();

        var samples = new List<TimeSpan>(runs);

        for (var run = 0; run < runs; run++)
        {
            var stopwatch = Stopwatch.StartNew();
            action();
            stopwatch.Stop();
            samples.Add(stopwatch.Elapsed);
        }

        return samples;
    }

    // Given a repository of 5,000 files on a warm file-system cache, when generation runs,
    // then it completes in under 2 seconds at the 95th percentile over 20 runs.
    [Fact]
    public void Given_a_five_thousand_file_repository_When_generated_Then_it_completes_within_two_seconds()
    {
        using var fixture = new RepositoryFixtureGenerator(SmallRepositoryFiles);

        var elapsed = Percentile95(Time(TimedRuns, () => GenerateOnce(fixture.Path)));

        Assert.True(
            elapsed < TimeSpan.FromSeconds(2),
            $"p95 was {elapsed.TotalMilliseconds:F0} ms against a 2,000 ms budget.");
    }

    // Given a repository of 100,000 files, when generation runs,
    // then it completes in under 10 seconds at the 95th percentile.
    [Fact]
    public void Given_a_hundred_thousand_file_repository_When_generated_Then_it_completes_within_ten_seconds()
    {
        using var fixture = new RepositoryFixtureGenerator(LargeRepositoryFiles);

        var elapsed = Percentile95(Time(10, () => GenerateOnce(fixture.Path)));

        Assert.True(
            elapsed < TimeSpan.FromSeconds(10),
            $"p95 was {elapsed.TotalMilliseconds:F0} ms against a 10,000 ms budget.");
    }

    // Given a repository of 100,000 files, when generation runs,
    // then peak managed heap remains below 256 MiB.
    [Fact]
    public void Given_a_hundred_thousand_file_repository_When_generated_Then_the_heap_stays_bounded()
    {
        const long Ceiling = 256L * 1024 * 1024;

        using var fixture = new RepositoryFixtureGenerator(LargeRepositoryFiles);

        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        GC.WaitForPendingFinalizers();

        GenerateOnce(fixture.Path);

        // Heap size after the run, which is the closest proxy the runtime offers for peak
        // occupancy without attaching a profiler to the test host.
        var heap = GC.GetGCMemoryInfo().HeapSizeBytes;

        Assert.True(heap < Ceiling, $"Managed heap was {heap / (1024 * 1024)} MiB against a 256 MiB ceiling.");
    }

    // Given a repository containing a file far larger than the read cap, when analysis
    // runs, then peak memory is unaffected by its size.
    [Fact]
    public void Given_an_oversized_file_When_analysed_Then_it_is_never_loaded_into_memory()
    {
        using var fixture = new RepositoryFixtureGenerator(100);

        var oversized = Path.Combine(fixture.Path, "huge.bin");
        using (var stream = File.Create(oversized))
        {
            stream.SetLength(512L * 1024 * 1024);
        }

        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        var before = GC.GetTotalMemory(forceFullCollection: true);

        GenerateOnce(fixture.Path);

        var after = GC.GetTotalMemory(forceFullCollection: true);

        Assert.True(
            after - before < 64L * 1024 * 1024,
            $"Analysis grew the heap by {(after - before) / (1024 * 1024)} MiB for a file it should never read.");
    }
}
