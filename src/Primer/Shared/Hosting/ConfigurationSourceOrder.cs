using Microsoft.Extensions.Configuration;

namespace Primer.Shared.Hosting;

/// <summary>
/// Registers configuration sources in precedence order. Later sources override earlier
/// ones, so a developer overriding a setting on the command line always wins, and a
/// repository can fix a setting for everyone without changing anyone's environment.
/// </summary>
internal static class ConfigurationSourceOrder
{
    /// <summary>Prefix marking an environment variable as Primer configuration.</summary>
    internal const string EnvironmentPrefix = "PRIMER_";

    /// <summary>The repository-root configuration file.</summary>
    internal const string FileName = "primer.json";

    internal static void Apply(IConfigurationBuilder builder, PrimerHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);

        builder.AddInMemoryCollection(Defaults());

        builder.AddJsonFile(
            Path.Combine(options.RepositoryRoot, FileName),
            optional: true,
            reloadOnChange: false);

        builder.AddInMemoryCollection(FromEnvironment(options.Environment));
        builder.AddInMemoryCollection(options.CommandLineOverrides);
    }

    /// <summary>
    /// The built-in defaults, stated explicitly so that reporting can name their source
    /// rather than showing a value that appears to come from nowhere.
    /// </summary>
    private static IEnumerable<KeyValuePair<string, string?>> Defaults() =>
    [
        new($"{PrimerOptions.SectionName}:Budget:MaxDepth", AnalysisBudget.DefaultMaxDepth.ToString()),
        new($"{PrimerOptions.SectionName}:Budget:MaxFileCount", AnalysisBudget.DefaultMaxFileCount.ToString()),
        new($"{PrimerOptions.SectionName}:Budget:MaxFileBytes", AnalysisBudget.DefaultMaxFileBytes.ToString()),
        new(
            $"{PrimerOptions.SectionName}:Budget:NetworkTimeout",
            TimeSpan.FromSeconds(AnalysisBudget.DefaultNetworkTimeoutSeconds).ToString()),
        new($"{PrimerOptions.SectionName}:TemplatePath", "templates"),
    ];

    /// <summary>
    /// Translates PRIMER_ prefixed variables into configuration keys. A double underscore
    /// separates levels, matching the convention the configuration system already uses.
    /// </summary>
    private static IEnumerable<KeyValuePair<string, string?>> FromEnvironment(
        IReadOnlyDictionary<string, string?> environment)
    {
        foreach (var (name, value) in environment)
        {
            if (!name.StartsWith(EnvironmentPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            var path = name[EnvironmentPrefix.Length..].Replace("__", ":", StringComparison.Ordinal);
            yield return new KeyValuePair<string, string?>($"{PrimerOptions.SectionName}:{path}", value);
        }
    }
}
