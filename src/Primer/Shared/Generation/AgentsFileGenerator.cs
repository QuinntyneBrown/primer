using System.Text;
using System.Text.RegularExpressions;
using Primer.Shared.Analysis;
using Primer.Shared.Hosting;

namespace Primer.Shared.Generation;

/// <summary>Builds the repository guidance document.</summary>
internal interface IAgentsFileGenerator
{
    IReadOnlyList<GeneratedFile> Generate(RepositoryContext context, bool recursive);
}

/// <summary>
/// Fills the template from the repository context, drops what cannot be verified, holds the
/// result inside the line ceiling, and wraps it in a managed region recording what produced
/// it.
/// </summary>
internal sealed partial class AgentsFileGenerator(
    ITemplateLocator templates,
    SectionComposer composer,
    GroundingValidator grounding,
    NestedProjectPlanner nested) : IAgentsFileGenerator
{
    /// <summary>The generated document's name, at the root and in every nested directory.</summary>
    internal const string FileName = "AGENTS.md";

    [GeneratedRegex(@"\{\{(?<token>\w+)\}\}")]
    private static partial Regex TokenPattern { get; }

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex BlankRuns { get; }

    public IReadOnlyList<GeneratedFile> Generate(RepositoryContext context, bool recursive)
    {
        ArgumentNullException.ThrowIfNull(context);

        var files = new List<GeneratedFile> { Compose(context, FileName) };

        if (recursive)
        {
            foreach (var directory in nested.Plan())
            {
                files.Add(Compose(context, $"{directory}/{FileName}", directory));
            }
        }

        return files;
    }

    private GeneratedFile Compose(RepositoryContext context, string relativePath, string? scope = null)
    {
        var template = templates.Locate(TemplateNames.AgentsFile).Content;
        var hash = ContentHash.Compute(context);

        var filled = TokenPattern.Replace(template, match => Substitute(match, context, hash));
        var body = BlankRuns.Replace(filled, "\n\n").Trim('\n');

        if (scope is not null)
        {
            body = $"# {scope}\n\nThis guidance is scoped to `{scope}/`. "
                + $"The repository-wide guidance is in the root `{FileName}`.\n\n" + body;
        }

        body = LineBudget.Apply(grounding.Validate(body));

        return new GeneratedFile(
            relativePath,
            ManagedRegion.Wrap(body, BuildMetadata.InformationalVersion, hash),
            FileAction.Create);
    }

    private string Substitute(Match match, RepositoryContext context, string hash)
    {
        var token = match.Groups["token"].Value;

        if (Enum.TryParse<TemplateTokenName>(token, ignoreCase: false, out var sectionToken))
        {
            return composer.Render(sectionToken, context);
        }

        return token switch
        {
            nameof(TemplateToken.ToolVersion) => BuildMetadata.InformationalVersion,
            nameof(TemplateToken.ContentHash) => hash,

            // A token the tool cannot supply is dropped rather than left visible; the
            // template locator has already rejected any token it does not recognise.
            _ => string.Empty,
        };
    }
}
