using System.CommandLine;
using Primer.Shared.Presentation;

namespace Primer.Shared.Hosting;

/// <summary>
/// Composes the command tree and runs one invocation. Command definition is free of the
/// service container: the host is built only once a command action needs it, so requests
/// answered here return without constructing any repository-facing service.
/// </summary>
internal static class PrimerCli
{
    private static readonly JsonOutputFormatter JsonFormatter = new();

    /// <summary>
    /// Discovers every command in the assembly. Registration is by discovery rather than a
    /// table, so adding a command means adding one file and editing nothing shared.
    /// </summary>
    internal static IReadOnlyList<ICommandModule> DiscoverModules() =>
    [
        .. typeof(PrimerCli).Assembly
            .GetTypes()
            .Where(type => !type.IsAbstract && !type.IsInterface && typeof(ICommandModule).IsAssignableFrom(type))
            .OrderBy(type => type.Name, StringComparer.Ordinal)
            .Select(type => (ICommandModule)Activator.CreateInstance(type)!),
    ];

    internal static RootCommand CreateRootCommand(InvocationConfiguration configuration) =>
        RootCommandFactory.Create(DiscoverModules(), configuration);

    internal static async Task<int> RunAsync(
        string[] args,
        InvocationConfiguration configuration,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var root = CreateRootCommand(configuration);
        var parseResult = root.Parse(args);

        if (parseResult.Errors.Count > 0 || parseResult.UnmatchedTokens.Count > 0)
        {
            // Rendered here rather than by Invoke, which prints its own message and picks
            // its own non-zero code, neither of which satisfies the exit-code contract.
            return (int)ParseErrorHandler.Render(parseResult, configuration);
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
        // Version reporting answers an option rather than a command, so it is not wrapped
        // in the result envelope: L2-002 asks for version and commit at the root.
        var text = format is OutputFormat.Json
            ? JsonFormatter.Render(
                new VersionPayload(BuildMetadata.InformationalVersion, BuildMetadata.CommitSha))
            : BuildMetadata.InformationalVersion;

        configuration.Output.WriteLine(text);
    }

    private sealed record VersionPayload(string Version, string Commit);
}
