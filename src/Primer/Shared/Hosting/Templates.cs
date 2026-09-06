using System.Text.RegularExpressions;

namespace Primer.Shared.Hosting;

/// <summary>The templates the tool renders from.</summary>
internal static class TemplateNames
{
    /// <summary>The repository guidance document.</summary>
    internal const string AgentsFile = "agents.md";
}

/// <summary>
/// The placeholders a template may reference. A template naming anything else is a
/// configuration error rather than a silently empty section.
/// </summary>
internal enum TemplateToken
{
    ProjectOverview,
    Commands,
    ProjectStructure,
    Testing,
    CodeStyle,
    GitWorkflow,
    Boundaries,
    ToolVersion,
    ContentHash,
}

/// <summary>A located template and where it came from.</summary>
internal sealed record TemplateSource(string Name, string Content, bool IsOverride);

/// <summary>Raised when a template names a placeholder the tool cannot supply.</summary>
internal sealed class TemplateTokenException : Exception
{
    internal TemplateTokenException()
        : this("A template referenced an unknown token.")
    {
    }

    internal TemplateTokenException(string message)
        : base(message)
    {
    }

    internal TemplateTokenException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    internal TemplateTokenException(string message, ExitCode exitCode)
        : base(message)
    {
        ExitCode = exitCode;
    }

    /// <summary>The outcome this failure maps to.</summary>
    internal ExitCode ExitCode { get; } = ExitCode.Configuration;
}

/// <summary>Resolves a template by name.</summary>
internal interface ITemplateLocator
{
    TemplateSource Locate(string name);
}

/// <summary>
/// Prefers a repository override over the built-in template, so a team can shape generated
/// output without forking the tool. Every token an override names is checked before it is
/// returned.
/// </summary>
internal sealed partial class TemplateLocator(
    RepositoryLocation location,
    Microsoft.Extensions.Options.IOptions<PrimerOptions> options) : ITemplateLocator
{
    private static readonly Dictionary<string, string> BuiltIn = new(StringComparer.OrdinalIgnoreCase)
    {
        [TemplateNames.AgentsFile] = string.Join(
            '\n',
            "{{ProjectOverview}}",
            "",
            "{{Commands}}",
            "",
            "{{ProjectStructure}}",
            "",
            "{{Testing}}",
            "",
            "{{CodeStyle}}",
            "",
            "{{GitWorkflow}}",
            "",
            "{{Boundaries}}",
            ""),
    };

    [GeneratedRegex(@"\{\{(?<token>\w+)\}\}")]
    private static partial Regex TokenPattern { get; }

    public TemplateSource Locate(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var overridePath = Path.Combine(location.Path, options.Value.TemplatePath, name);

        if (File.Exists(overridePath))
        {
            var content = File.ReadAllText(overridePath);
            EnsureTokensAreKnown(content, overridePath);
            return new TemplateSource(name, content, IsOverride: true);
        }

        if (!BuiltIn.TryGetValue(name, out var builtIn))
        {
            throw new TemplateTokenException(
                $"No template named '{name}' is built in, and no override was found at '{overridePath}'.",
                ExitCode.Configuration);
        }

        return new TemplateSource(name, builtIn, IsOverride: false);
    }

    private static void EnsureTokensAreKnown(string content, string path)
    {
        foreach (Match match in TokenPattern.Matches(content))
        {
            var token = match.Groups["token"].Value;

            if (!Enum.TryParse<TemplateToken>(token, ignoreCase: false, out _))
            {
                throw new TemplateTokenException(
                    $"The template '{path}' references an unknown token '{token}'.",
                    ExitCode.Configuration);
            }
        }
    }
}
