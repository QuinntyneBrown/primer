using System.CommandLine;
using Primer.Shared.Hosting;

namespace Primer;

/// <summary>
/// Process entry point. Dispatches the parsed command line and returns the resulting
/// exit code to the operating system.
/// </summary>
internal static class Program
{
    internal static async Task<int> Main(string[] args)
    {
        var configuration = new InvocationConfiguration
        {
            Output = Console.Out,
            Error = Console.Error,
        };

        return await PrimerCli.RunAsync(args, configuration, CancellationToken.None);
    }
}
