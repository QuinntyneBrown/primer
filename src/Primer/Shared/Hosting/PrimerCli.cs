using System.CommandLine;
using System.Text.Json;

namespace Primer.Shared.Hosting;

/// <summary>
/// Composes the command tree and runs one invocation. Command definition is free of the
/// service container: the host is built only once a command action needs it, so requests
/// answered here return without constructing any repository-facing service.
/// </summary>
internal static class PrimerCli
{
    private const string RootDescription =
        "Creates contextual agent instruction files for a repository and verifies that the "
        + "required Model Context Protocol components are installed.";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    internal static RootCommand CreateRootCommand()
    {
        var root = new RootCommand(RootDescription);

        // The built-in version option carries a validator that rejects it alongside any
        // other option, which would make `--version --format json` a parse failure.
        foreach (var builtIn in root.Options.OfType<VersionOption>().ToList())
        {
            root.Options.Remove(builtIn);
        }

        root.Options.Add(GlobalOptions.Version);

        foreach (var option in GlobalOptions.All)
        {
            root.Options.Add(option);
        }

        return root;
    }

    internal static async Task<int> RunAsync(
        string[] args,
        InvocationConfiguration configuration,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var root = CreateRootCommand();
        var parseResult = root.Parse(args);

        if (parseResult.Errors.Count > 0)
        {
            foreach (var error in parseResult.Errors)
            {
                configuration.Error.WriteLine(error.Message);
            }

            return (int)ExitCode.Usage;
        }

        // Answered before any host is built, so the version path constructs no
        // repository-facing service and stays well inside its startup budget.
        if (parseResult.GetValue(GlobalOptions.Version))
        {
            WriteVersion(configuration, parseResult.GetValue(GlobalOptions.Format));
            return (int)ExitCode.Success;
        }

        return await parseResult.InvokeAsync(configuration, cancellationToken);
    }

    private static void WriteVersion(InvocationConfiguration configuration, OutputFormat format)
    {
        var text = format is OutputFormat.Json
            ? JsonSerializer.Serialize(
                new VersionPayload(BuildMetadata.InformationalVersion, BuildMetadata.CommitSha),
                JsonOptions)
            : BuildMetadata.InformationalVersion;

        configuration.Output.WriteLine(text);
    }

    private sealed record VersionPayload(string Version, string Commit);
}
