using System.Text.RegularExpressions;
using Primer.Shared.Hosting;

namespace Primer.Shared.Generation.Greenfield;

/// <summary>Builds guidance for a project that does not exist yet.</summary>
internal interface IGreenfieldGenerator
{
    GeneratedFile Generate(PromptText prompt, SolutionArchetype archetype);
}

/// <summary>
/// Renders the archetype's template, exactly as analysis mode renders its own. What it
/// does not do is ground the result against the repository: the folder
/// outline names directories the project is about to create, so checking that they exist
/// would delete the very thing the reader needs.
/// </summary>
internal sealed partial class GreenfieldGenerator(
    ITemplateLocator templates,
    RepositoryLocation location) : IGreenfieldGenerator
{
    /// <summary>How the quoted description is labelled, so it reads as data.</summary>
    internal const string PurposeLabel =
        "The following description was supplied when this file was generated, quoted for reference:";

    [GeneratedRegex(@"\{\{(?<token>\w+)\}\}")]
    private static partial Regex TokenPattern { get; }

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex BlankRuns { get; }

    public GeneratedFile Generate(PromptText prompt, SolutionArchetype archetype)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        var template = templates.Locate(ArchetypeTemplates.NameOf(archetype)).Content;
        var projectName = ProjectName();

        var filled = TokenPattern.Replace(
            template, match => Substitute(match, prompt, projectName, NamespacePrefix(projectName)));
        var body = LineBudget.Apply(BlankRuns.Replace(filled, "\n\n").Trim('\n'));

        return new GeneratedFile(AgentsFileGenerator.FileName, body, FileAction.Create);
    }

    /// <summary>
    /// The directory being initialised names the project, as it does in analysis mode. It
    /// is the one fact available before any code exists.
    /// </summary>
    private string ProjectName() =>
        new DirectoryInfo(location.Path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)).Name;

    /// <summary>
    /// The project name as a namespace can spell it. A directory is free to be called
    /// "my-app"; a namespace is not, and an example that will not compile is worse than no
    /// example, because the agent following it cannot tell.
    /// </summary>
    private static string NamespacePrefix(string projectName)
    {
        var identifier = new System.Text.StringBuilder(projectName.Length);
        var startOfWord = true;

        foreach (var character in projectName)
        {
            if (!char.IsLetterOrDigit(character))
            {
                startOfWord = true;
                continue;
            }

            identifier.Append(startOfWord ? char.ToUpperInvariant(character) : character);
            startOfWord = false;
        }

        // A namespace cannot open with a digit, and an empty one helps nobody.
        return identifier.Length == 0 || char.IsDigit(identifier[0])
            ? "App" + identifier
            : identifier.ToString();
    }

    private static string Substitute(
        Match match, PromptText prompt, string projectName, string namespacePrefix) =>
        match.Groups["token"].Value switch
        {
            nameof(TemplateToken.ProjectName) => UntrustedContentFence.EscapeInline(projectName),

            nameof(TemplateToken.NamespacePrefix) => namespacePrefix,

            // The description is the operator's words, not the tool's. It is fenced and
            // labelled so no sentence in it can be read as a directive the tool issued.
            nameof(TemplateToken.Purpose) => UntrustedContentFence.Fence(prompt.Value, PurposeLabel),

            // A token the tool cannot supply is dropped rather than left visible; the
            // template locator has already rejected any token it does not recognise.
            _ => string.Empty,
        };
}
