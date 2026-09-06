namespace Primer.Shared.Analysis;

/// <summary>Records the engineering conventions a repository already declares.</summary>
internal interface IConventionDetector
{
    IReadOnlyList<ConventionDescriptor> Detect(RepositoryRoot root);
}

/// <summary>
/// Reuses what the repository already states rather than inventing a convention beside it.
/// </summary>
internal sealed class ConventionDetector : IConventionDetector
{
    private static readonly (string Kind, string RelativePath)[] Known =
    [
        ("formatting", ".editorconfig"),
        ("build", "Directory.Build.props"),
        ("packages", "Directory.Packages.props"),
        ("line-endings", ".gitattributes"),
        ("sdk", "global.json"),
    ];

    public IReadOnlyList<ConventionDescriptor> Detect(RepositoryRoot root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var found = Known
            .Where(entry => File.Exists(Path.Combine(root.Path, entry.RelativePath)))
            .Select(entry => new ConventionDescriptor(entry.Kind, entry.RelativePath))
            .ToList();

        var workflows = Path.Combine(root.Path, ".github", "workflows");

        if (Directory.Exists(workflows))
        {
            foreach (var workflow in Directory
                .EnumerateFiles(workflows, "*.y*ml")
                .OrderBy(path => path, StringComparer.Ordinal))
            {
                found.Add(new ConventionDescriptor(
                    "ci",
                    Path.GetRelativePath(root.Path, workflow).Replace('\\', '/')));
            }
        }

        return found;
    }
}
