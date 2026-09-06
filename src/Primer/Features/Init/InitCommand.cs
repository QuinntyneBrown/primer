using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Primer.Shared.Analysis;
using Primer.Shared.Generation;
using Primer.Shared.Hosting;
using Primer.Shared.Presentation;

namespace Primer.Features.Init;

/// <summary>Defines <c>primer init</c>.</summary>
internal sealed class InitCommand : ICommandModule
{
    internal static Option<string[]> Agent { get; } = new("--agent")
    {
        Description = "An agent to write an instruction file for. Repeatable, or 'all'.",
        AllowMultipleArgumentsPerToken = false,
    };

    internal static Option<bool> Recursive { get; } = new("--recursive")
    {
        Description = "Also write nested guidance for each independent project.",
    };

    internal static Option<bool> Force { get; } = new("--force")
    {
        Description = "Overwrite a file primer did not generate, after backing it up.",
    };

    public Command Build(InvocationConfiguration configuration)
    {
        var command = new Command("init", "Generate agent instruction files for this repository.")
        {
            Agent,
            Recursive,
            Force,
        };

        command.SetAction((parseResult, cancellationToken) => InvocationPipeline.RunAsync(
            parseResult,
            configuration,
            (services, globals, token) => Task.FromResult(
                Handle(services, globals, parseResult)),
            cancellationToken));

        return command;
    }

    private static ExitCode Handle(IServiceProvider services, GlobalOptionValues globals, ParseResult parseResult)
    {
        var console = services.GetRequiredService<IPrimerConsole>();
        var locator = services.GetRequiredService<IRepositoryLocator>();
        var location = services.GetRequiredService<RepositoryLocation>();

        var context = services.GetRequiredService<IRepositoryAnalyzer>()
            .Analyze(locator.Locate(location.Path));

        var selection = AgentSelection.Parse(parseResult.GetValue(Agent) ?? []);

        var generated = services.GetRequiredService<IAgentsFileGenerator>()
            .Generate(context, parseResult.GetValue(Recursive))
            .Concat(selection.Targets.Select(PointerFileGenerator.Generate).OfType<GeneratedFile>())
            .ToList();

        var plan = services.GetRequiredService<OverwritePolicy>()
            .Plan(generated, parseResult.GetValue(Force));

        if (globals.DryRun)
        {
            services.GetRequiredService<IDryRunReporter>().Report(plan);
            return ExitCode.Success;
        }

        services.GetRequiredService<IFileWriter>().Apply(plan);

        console.WriteResult(string.Join(
            '\n',
            plan.Entries.Select(entry =>
                $"{entry.Action.ToString().ToLowerInvariant()}: {entry.RelativePath}")));

        return ExitCode.Success;
    }
}
