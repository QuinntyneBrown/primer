using System.CommandLine;
using Primer.Shared.Hosting;

namespace Primer.IntegrationTests;

/// <summary>
/// Runs the CLI in-process against captured streams. Keeps the acceptance suite
/// hermetic and parallel-safe: no process is spawned and no ambient stream is touched.
/// </summary>
internal static class PrimerCliHarness
{
    internal readonly record struct Result(int ExitCode, string StandardOutput, string StandardError);

    internal static async Task<Result> RunAsync(params string[] args)
    {
        await using var output = new StringWriter();
        await using var error = new StringWriter();

        var configuration = new InvocationConfiguration
        {
            Output = output,
            Error = error,
            EnableDefaultExceptionHandler = false,
        };

        var exitCode = await PrimerCli.RunAsync(args, configuration, TestContext.Current.CancellationToken);

        return new Result(exitCode, output.ToString(), error.ToString());
    }
}
