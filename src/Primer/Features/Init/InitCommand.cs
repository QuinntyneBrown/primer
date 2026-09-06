using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Primer.Shared.Analysis;
using Primer.Shared.Generation;
using Primer.Shared.Generation.Greenfield;
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

    internal static Option<string> Prompt { get; } = new("--prompt")
    {
        Description = "Describe a project that does not exist yet, instead of analysing this one.",
    };

    internal static Option<string> PromptFile { get; } = new("--prompt-file")
    {
        Description = "Read the description from a file instead of the command line.",
    };

    internal static Option<string> Archetype { get; } = new("--archetype")
    {
        Description = $"The solution shape to generate: {string.Join(" or ", ArchetypeNames.Supported)}.",
    };

    public Command Build(InvocationConfiguration configuration)
    {
        var command = new Command("init", "Generate agent instruction files for this repository.")
        {
            Agent,
            Recursive,
            Force,
            Prompt,
            PromptFile,
            Archetype,
        };

        command.SetAction((parseResult, cancellationToken) => InvocationPipeline.RunAsync(
            parseResult,
            configuration,
            (services, globals, token) => Task.FromResult(
                Handle(services, globals, parseResult)),
            cancellationToken,

            // A described project has no repository to find, and describing one usually
            // comes before `git init`.
            requiresRepository: !DescribesAProject(parseResult)));

        return command;
    }

    /// <summary>Whether this invocation supplies a description rather than analysing a repository.</summary>
    private static bool DescribesAProject(ParseResult parseResult) =>
        parseResult.GetValue(Prompt) is not null
        || !string.IsNullOrWhiteSpace(parseResult.GetValue(PromptFile));

    private static ExitCode Handle(IServiceProvider services, GlobalOptionValues globals, ParseResult parseResult)
    {
        var selection = AgentSelection.Parse(parseResult.GetValue(Agent) ?? []);

        var generated = DescribesAProject(parseResult)
            ? Describe(services, parseResult)
            : Analyse(services, parseResult);

        var files = generated
            .Concat(selection.Targets.Select(PointerFileGenerator.Generate).OfType<GeneratedFile>())
            .ToList();

        var plan = services.GetRequiredService<OverwritePolicy>()
            .Plan(files, parseResult.GetValue(Force));

        if (globals.DryRun)
        {
            services.GetRequiredService<IDryRunReporter>().Report(plan);
            return ExitCode.Success;
        }

        services.GetRequiredService<IFileWriter>().Apply(plan);

        services.GetRequiredService<IPrimerConsole>().WriteResult(string.Join(
            '\n',
            plan.Entries.Select(entry =>
                $"{entry.Action.ToString().ToLowerInvariant()}: {entry.RelativePath}")));

        return ExitCode.Success;
    }

    /// <summary>Writes down what the repository is.</summary>
    private static IEnumerable<GeneratedFile> Analyse(IServiceProvider services, ParseResult parseResult)
    {
        var locator = services.GetRequiredService<IRepositoryLocator>();
        var location = services.GetRequiredService<RepositoryLocation>();

        var context = services.GetRequiredService<IRepositoryAnalyzer>()
            .Analyze(locator.Locate(location.Path));

        return services.GetRequiredService<IAgentsFileGenerator>()
            .Generate(context, parseResult.GetValue(Recursive));
    }

    /// <summary>Writes down what the project shall be, from the description supplied.</summary>
    private static IEnumerable<GeneratedFile> Describe(IServiceProvider services, ParseResult parseResult)
    {
        if (parseResult.GetValue(Recursive))
        {
            throw new RecursiveDescriptionException();
        }

        var prompt = services.GetRequiredService<PromptSource>()
            .Resolve(parseResult.GetValue(Prompt), parseResult.GetValue(PromptFile));

        var requested = parseResult.GetValue(Archetype);

        var archetype = requested is not null
            ? ArchetypeNames.Resolve(requested)
            : services.GetRequiredService<IPromptClassifier>().Classify(prompt.Value)
                ?? throw new UndecidableDescriptionException();

        return [services.GetRequiredService<IGreenfieldGenerator>().Generate(prompt, archetype)];
    }
}

/// <summary>Raised when nested guidance is asked for without a repository to derive it from.</summary>
internal sealed class RecursiveDescriptionException()
    : PrimerException(new PrimerError(
        What: "--recursive needs a repository to analyse",
        Subject: "--recursive with a description",
        NextAction: "Drop --recursive. Nested guidance describes projects found on disk, and a described project has none yet.",
        ExitCode: ExitCode.Usage));
