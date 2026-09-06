using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Primer.Shared.Hosting;
using Primer.Shared.Mcp;
using Primer.Shared.Presentation;

namespace Primer.Features.Mcp;

/// <summary>Defines <c>primer mcp</c> and its subcommands.</summary>
internal sealed class McpCommand : ICommandModule
{
    public Command Build(InvocationConfiguration configuration)
    {
        var command = new Command("mcp", "Inspect and provision the required MCP components.")
        {
            BuildList(configuration),
            BuildCheck(configuration),
            BuildInstall(configuration),
        };

        return command;
    }

    private static Command BuildList(InvocationConfiguration configuration)
    {
        var command = new Command("list", "List the MCP servers this repository requires.");

        command.SetAction((parseResult, cancellationToken) => InvocationPipeline.RunAsync(
            parseResult,
            configuration,
            (services, _, _) =>
            {
                var requirements = services.GetRequiredService<IMcpRequirementSource>().GetRequirements();

                services.GetRequiredService<IPrimerConsole>().WriteResult(
                    requirements.Select(requirement => new
                    {
                        name = requirement.Name,
                        version = requirement.VersionRange,
                        install = requirement.InstallMethod,
                    }).ToList());

                return Task.FromResult(ExitCode.Success);
            },
            cancellationToken));

        return command;
    }

    private static Command BuildCheck(InvocationConfiguration configuration)
    {
        var command = new Command("check", "Report which required MCP servers are missing or outdated.");

        command.SetAction((parseResult, cancellationToken) => InvocationPipeline.RunAsync(
            parseResult,
            configuration,
            (services, _, _) =>
            {
                var report = services.GetRequiredService<McpGapAnalyzer>().Analyze();
                var console = services.GetRequiredService<IPrimerConsole>();

                console.WriteResult(report.HasGap
                    ? string.Join(
                        '\n',
                        report.Entries
                            .Where(entry => entry.Status is McpServerStatus.Missing or McpServerStatus.Outdated)
                            .Select(entry =>
                                $"{entry.Status.ToString().ToLowerInvariant()}: {entry.Requirement.Name} "
                                + $"-- {entry.RemediationCommand}"))
                    : "Every required MCP server is installed.");

                return Task.FromResult(report.ExitCode);
            },
            cancellationToken));

        return command;
    }

    private static Command BuildInstall(InvocationConfiguration configuration)
    {
        var command = new Command("install", "Install the missing MCP servers, with consent.");

        command.SetAction((parseResult, cancellationToken) => InvocationPipeline.RunAsync(
            parseResult,
            configuration,
            (services, globals, token) => services.GetRequiredService<McpInstaller>()
                .InstallAsync(globals.Yes, globals.DryRun, token),
            cancellationToken));

        return command;
    }
}
