// Acceptance Test
// Traces to: L2-054
// Description: Verify version and help return inside their startup budgets, measured on a
//              real process, and that neither performs repository analysis.

using System.Diagnostics;

namespace Primer.PerformanceTests;

[Trait("tier", "slow")]
public sealed class StartupBudgetTests
{
    private const int TimedRuns = 20;

    /// <summary>
    /// The installed executable. Startup cost includes process creation and host
    /// initialisation, neither of which an in-process call would measure.
    /// </summary>
    private static string? ToolPath()
    {
        var candidate = new DirectoryInfo(AppContext.BaseDirectory);

        while (candidate is not null)
        {
            if (File.Exists(Path.Combine(candidate.FullName, "Primer.slnx")))
            {
                var executable = Path.Combine(
                    candidate.FullName,
                    "src", "Primer", "bin", "Release", "net10.0",
                    OperatingSystem.IsWindows() ? "primer.exe" : "primer");

                return File.Exists(executable) ? executable : null;
            }

            candidate = candidate.Parent;
        }

        return null;
    }

    private static (TimeSpan Percentile95, string Output) Measure(string tool, params string[] args)
    {
        var samples = new List<TimeSpan>(TimedRuns);
        var output = string.Empty;

        // One untimed run, so JIT and file-system caching are not charged to the budget.
        for (var run = -1; run < TimedRuns; run++)
        {
            var startInfo = new ProcessStartInfo(tool)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = Path.GetTempPath(),
            };

            foreach (var argument in args)
            {
                startInfo.ArgumentList.Add(argument);
            }

            var stopwatch = Stopwatch.StartNew();
            using var process = Process.Start(startInfo)!;
            output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            stopwatch.Stop();

            if (run >= 0)
            {
                samples.Add(stopwatch.Elapsed);
            }
        }

        var ordered = samples.OrderBy(sample => sample).ToList();
        var index = Math.Clamp((int)Math.Ceiling(0.95 * ordered.Count) - 1, 0, ordered.Count - 1);

        return (ordered[index], output);
    }

    // Given an installed tool on a warm cache, when primer --version is run,
    // then it completes in under 200 milliseconds at the 95th percentile over 20 runs.
    [Fact]
    public void Given_the_installed_tool_When_version_is_requested_Then_it_returns_within_two_hundred_milliseconds()
    {
        var tool = ToolPath();

        if (tool is null)
        {
            Assert.Skip("The Release build of the tool was not found; build it before measuring startup.");
            return;
        }

        var (elapsed, output) = Measure(tool, "--version");

        Assert.False(string.IsNullOrWhiteSpace(output));
        Assert.True(
            elapsed < TimeSpan.FromMilliseconds(200),
            $"p95 was {elapsed.TotalMilliseconds:F0} ms against a 200 ms budget.");
    }

    // Given an installed tool, when primer --help is run, then it completes in under 300
    // milliseconds at the 95th percentile and performs no repository analysis.
    [Fact]
    public void Given_the_installed_tool_When_help_is_requested_Then_it_returns_within_three_hundred_milliseconds()
    {
        var tool = ToolPath();

        if (tool is null)
        {
            Assert.Skip("The Release build of the tool was not found; build it before measuring startup.");
            return;
        }

        var (elapsed, output) = Measure(tool, "--help");

        Assert.Contains("init", output, StringComparison.Ordinal);
        Assert.True(
            elapsed < TimeSpan.FromMilliseconds(300),
            $"p95 was {elapsed.TotalMilliseconds:F0} ms against a 300 ms budget.");
    }

    // Given an invocation that fails to parse, when it is run,
    // then it exits without performing repository analysis.
    [Fact]
    public void Given_an_unparseable_invocation_When_run_Then_it_returns_without_analysing_anything()
    {
        var tool = ToolPath();

        if (tool is null)
        {
            Assert.Skip("The Release build of the tool was not found; build it before measuring startup.");
            return;
        }

        var (elapsed, _) = Measure(tool, "--not-an-option");

        Assert.True(
            elapsed < TimeSpan.FromMilliseconds(300),
            $"p95 was {elapsed.TotalMilliseconds:F0} ms; a parse failure should not analyse a repository.");
    }
}
