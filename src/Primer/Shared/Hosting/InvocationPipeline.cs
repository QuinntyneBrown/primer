using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Primer.Shared.Analysis;
using Primer.Shared.Presentation;

namespace Primer.Shared.Hosting;

/// <summary>
/// Runs one command. Resolves the repository, builds the host, resolves the handler from
/// the container, and maps every outcome onto the exit-code contract.
///
/// This is a wrapper each command action routes through rather than middleware: the
/// System.CommandLine middleware pipeline that earlier designs assumed no longer exists in
/// the released API.
/// </summary>
internal static class InvocationPipeline
{
    internal static async Task<int> RunAsync(
        ParseResult parseResult,
        InvocationConfiguration configuration,
        Func<IServiceProvider, GlobalOptionValues, CancellationToken, Task<ExitCode>> handler,
        CancellationToken cancellationToken,
        bool requiresRepository = true)
    {
        ArgumentNullException.ThrowIfNull(parseResult);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(handler);

        var globals = GlobalOptionValues.From(parseResult);

        var capabilities = TerminalCapabilities.Detect(
            name => globals.NoColor && name == "NO_COLOR" ? "1" : Environment.GetEnvironmentVariable(name),
            detectedWidth: null,
            outputRedirected: true,
            consoleSupportsUnicode: true);

        var console = new PrimerConsole(
            configuration.Output, configuration.Error, capabilities, globals.Format, globals.Verbosity);

        var presenter = new ErrorPresenter(console);

        try
        {
            // Generating from a description needs no repository to read, and the moment a
            // project is described is usually before `git init` has been run. Analysis
            // still requires one, because without it there is nothing to analyse.
            var rootPath = requiresRepository
                ? new GitRepositoryLocator().Locate(globals.TargetPath).Path
                : Path.GetFullPath(globals.TargetPath);

            var built = PrimerHostBuilder.Build(new PrimerHostOptions
            {
                RepositoryRoot = rootPath,
                Environment = CapturedEnvironment(),
                Output = configuration.Output,
                Error = configuration.Error,
                Format = globals.Format,
                Verbosity = globals.Verbosity,
                Capabilities = capabilities,
            });

            using (built)
            {
                if (!built.Succeeded)
                {
                    return (int)presenter.Present(built.Error, globals.Verbosity);
                }

                built.Host.Services.GetRequiredService<EffectiveSettingsReporter>().Report();

                return (int)await handler(built.Host.Services, globals, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (PrimerException expected)
        {
            return (int)presenter.Present(expected.Error, globals.Verbosity);
        }
        catch (OperationCanceledException)
        {
            console.WriteErrorLine("Cancelled.");
            return (int)ExitCode.Unexpected;
        }
        catch (Exception unexpected)
        {
            return (int)presenter.PresentUnhandled(unexpected);
        }
    }

    private static Dictionary<string, string?> CapturedEnvironment()
    {
        var captured = new Dictionary<string, string?>(StringComparer.Ordinal);

        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            if (entry.Key is string key && key.StartsWith(ConfigurationSourceOrder.EnvironmentPrefix, StringComparison.Ordinal))
            {
                captured[key] = entry.Value as string;
            }
        }

        return captured;
    }
}
