using Primer.Shared.Hosting;

namespace Primer.Shared.Generation;

/// <summary>
/// Decides what writing each target will do, before anything is written. Every target is
/// generated in full, so a run replaces what it finds; a target whose bytes already match
/// is reported unchanged and left closed, which is what keeps its timestamp still.
/// </summary>
internal sealed class OverwritePolicy(RepositoryLocation location)
{
    private readonly LineEndingPolicy _lineEndings = new(location);

    internal WritePlan Plan(IReadOnlyList<GeneratedFile> generated)
    {
        ArgumentNullException.ThrowIfNull(generated);

        var lineEnding = _lineEndings.Resolve();
        var entries = new List<GeneratedFile>(generated.Count);

        foreach (var file in generated)
        {
            entries.Add(Decide(file, lineEnding));
        }

        return new WritePlan(entries);
    }

    private GeneratedFile Decide(GeneratedFile file, string lineEnding)
    {
        var absolute = Path.Combine(location.Path, file.RelativePath);
        var content = EncodingPolicy.Normalize(file.Content, lineEnding);

        if (!File.Exists(absolute))
        {
            return file with { Content = content };
        }

        var existing = File.ReadAllText(absolute);

        return file with
        {
            Content = content,
            ExistingContent = existing,
            Action = string.Equals(existing, content, StringComparison.Ordinal)
                ? FileAction.Unchanged
                : FileAction.Update,
        };
    }
}
