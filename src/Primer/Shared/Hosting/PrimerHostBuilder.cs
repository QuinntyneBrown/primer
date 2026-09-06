using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Primer.Shared.Analysis;
using Primer.Shared.Presentation;

namespace Primer.Shared.Hosting;

/// <summary>
/// The composition root. Registers configuration sources in precedence order, binds and
/// validates options, and builds a container that is validated before anything runs.
/// </summary>
internal static class PrimerHostBuilder
{
    /// <summary>
    /// Builds the host, or reports why it could not be built. Validation completes before
    /// the caller has read or written a single repository file.
    /// </summary>
    internal static HostBuildResult Build(PrimerHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var capabilities = options.Capabilities ?? TerminalCapabilities.FromCurrentProcess();
        var console = new PrimerConsole(
            options.Output, options.Error, capabilities, options.Format, options.Verbosity);

        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings
        {
            ContentRootPath = options.RepositoryRoot,
        });

        try
        {
            ConfigurationSourceOrder.Apply(builder.Configuration, options);

            // Touching a value forces the JSON provider to parse, so a malformed file
            // fails here rather than at the first setting a command happens to read.
            _ = builder.Configuration[$"{PrimerOptions.SectionName}:TemplatePath"];
        }
        catch (InvalidDataException parseFailure)
        {
            return HostBuildResult.Failed(new PrimerError(
                What: "The configuration file could not be parsed",
                Subject: Path.Combine(options.RepositoryRoot, ConfigurationSourceOrder.FileName),
                NextAction: "Correct the JSON syntax, or remove the file to use the defaults.",
                ExitCode: ExitCode.Configuration,
                Cause: parseFailure));
        }

        WarnAboutUnknownKeys(builder.Configuration, console);

        // The empty host builder registers IConfiguration but not IConfigurationRoot, and
        // reporting a setting's source needs the root's provider view.
        builder.Services.AddSingleton<IConfigurationRoot>(builder.Configuration);

        Register(builder.Services, options, capabilities, console);
        options.ConfigureServices?.Invoke(builder.Services);

        builder.ConfigureContainer(new DefaultServiceProviderFactory(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        }));

        var built = builder.Build();

        try
        {
            // Binding and validation both complete before any command does work.
            _ = built.Services.GetRequiredService<IOptions<PrimerOptions>>().Value;
        }
        catch (OptionsValidationException validation)
        {
            built.Dispose();

            return HostBuildResult.Failed(new PrimerError(
                What: "The configuration is not valid",
                Subject: string.Join(' ', validation.Failures),
                NextAction: "Correct the offending setting in primer.json, the environment, or the command line.",
                ExitCode: ExitCode.Configuration,
                Cause: validation));
        }

        return HostBuildResult.Built(built);
    }

    private static void Register(
        IServiceCollection services,
        PrimerHostOptions options,
        ITerminalCapabilities capabilities,
        IPrimerConsole console)
    {
        services.AddSingleton(new RepositoryLocation(options.RepositoryRoot));
        services.AddSingleton(capabilities);
        services.AddSingleton(console);
        services.AddSingleton<ErrorPresenter>();
        services.AddSingleton<ITemplateLocator, TemplateLocator>();
        services.AddSingleton<EffectiveSettingsReporter>();

        services.AddSingleton<IRepositoryLocator, GitRepositoryLocator>();
        services.AddSingleton<IIgnoreMatcher, GitIgnoreMatcher>();
        services.AddSingleton<ISecretRedactor, PatternSecretRedactor>();
        services.AddSingleton<FileProbe>();
        services.AddSingleton<IStructureScanner, BoundedStructureScanner>();
        services.AddSingleton<IConventionDetector, ConventionDetector>();
        services.AddSingleton<IRepositoryAnalyzer, RepositoryAnalyzer>();

        services.AddSingleton<IStackDetector, DotnetStackDetector>();
        services.AddSingleton<IStackDetector, NodeStackDetector>();
        services.AddSingleton<IStackDetector, PythonStackDetector>();
        services.AddSingleton<IStackDetector, GoStackDetector>();

        services.AddSingleton<ICommandInferrer, WorkflowCommandInferrer>();
        services.AddSingleton<ICommandInferrer, SolutionCommandInferrer>();
        services.AddSingleton<ICommandInferrer, PackageScriptInferrer>();

        services.AddSingleton<IValidateOptions<PrimerOptions>, PrimerOptionsValidator>();
        services.AddOptions<PrimerOptions>()
            .BindConfiguration(PrimerOptions.SectionName)
            .ValidateOnStart();
    }

    /// <summary>
    /// A key nobody recognises is almost always a typo. It is reported rather than ignored,
    /// but it does not stop the run, because the rest of the configuration is still usable.
    /// </summary>
    private static void WarnAboutUnknownKeys(ConfigurationManager configuration, PrimerConsole console)
    {
        var known = typeof(PrimerOptions)
            .GetProperties()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var child in configuration.GetSection(PrimerOptions.SectionName).GetChildren())
        {
            if (!known.Contains(child.Key))
            {
                console.WriteWarning(
                    $"Unrecognised configuration key '{PrimerOptions.SectionName}:{child.Key}'.");
            }
        }
    }
}
