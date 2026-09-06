using System.CommandLine;
using System.CommandLine.Help;

namespace Primer.Shared.Hosting;

/// <summary>
/// Assembles the command tree. Global options are attached once to the root as recursive
/// options, which is what makes every command accept them with identical meaning.
/// </summary>
internal static class RootCommandFactory
{
    private const string Description =
        "Creates contextual agent instruction files for a repository and verifies that the "
        + "required Model Context Protocol components are installed.";

    internal static RootCommand Create(
        IReadOnlyList<ICommandModule> modules,
        InvocationConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(modules);
        ArgumentNullException.ThrowIfNull(configuration);

        var root = new RootCommand(Description);

        // The built-in version option refuses to be combined with any other option, which
        // would make `--version --format json` a parse failure.
        foreach (var builtIn in root.Options.OfType<VersionOption>().ToList())
        {
            root.Options.Remove(builtIn);
        }

        root.Options.Add(GlobalOptions.Version);

        foreach (var option in GlobalOptions.All)
        {
            root.Options.Add(option);
        }

        foreach (var module in modules)
        {
            root.Subcommands.Add(module.Build(configuration));
        }

        // A root command with subcommands and no action fails to parse bare input, which
        // would send `primer` with no arguments out through the parse-error path.
        root.SetAction(parseResult =>
        {
            var help = root.Options.OfType<HelpOption>().FirstOrDefault()?.Action;

            if (help is System.CommandLine.Invocation.SynchronousCommandLineAction synchronous)
            {
                synchronous.Invoke(parseResult);
            }

            return (int)ExitCode.Success;
        });

        return root;
    }
}
