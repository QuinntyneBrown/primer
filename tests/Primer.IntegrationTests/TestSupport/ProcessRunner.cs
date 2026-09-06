using System.Diagnostics;
using System.Text;

namespace Primer.IntegrationTests.TestSupport;

/// <summary>
/// Runs an external process and captures its streams. Used only by the out-of-process
/// tier of the suite, where a requirement can be proven no other way.
/// </summary>
internal static class ProcessRunner
{
    internal readonly record struct Result(int ExitCode, string StandardOutput, string StandardError)
    {
        internal string Combined => StandardOutput + StandardError;
    }

    internal static async Task<Result> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string>? environment,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (environment is not null)
        {
            foreach (var (key, value) in environment)
            {
                startInfo.Environment[key] = value;
            }
        }

        using var process = new Process { StartInfo = startInfo };
        var output = new StringBuilder();
        var error = new StringBuilder();

        process.OutputDataReceived += (_, e) => { if (e.Data is not null) { output.AppendLine(e.Data); } };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) { error.AppendLine(e.Data); } };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);

        return new Result(process.ExitCode, output.ToString(), error.ToString());
    }
}
