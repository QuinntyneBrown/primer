using Primer.Shared.Hosting;

namespace Primer.Shared.Generation;

/// <summary>
/// Decides what writing each target will do, before anything is written. A file the tool
/// did not generate is never replaced on the tool's own judgement.
/// </summary>
internal sealed class OverwritePolicy(RepositoryLocation location)
{
    private readonly LineEndingPolicy _lineEndings = new(location);

    internal WritePlan Plan(IReadOnlyList<GeneratedFile> generated, bool force)
    {
        ArgumentNullException.ThrowIfNull(generated);

        var lineEnding = _lineEndings.Resolve();
        var entries = new List<GeneratedFile>(generated.Count);

        foreach (var file in generated)
        {
            entries.Add(Decide(file, lineEnding, force));
        }

        return new WritePlan(entries);
    }

    private GeneratedFile Decide(GeneratedFile file, string lineEnding, bool force)
    {
        var absolute = Path.Combine(location.Path, file.RelativePath);

        if (!File.Exists(absolute))
        {
            return file with { Content = EncodingPolicy.Normalize(file.Content, lineEnding) };
        }

        var existing = File.ReadAllText(absolute);
        var merged = Merge(existing, file, force);
        var normalized = EncodingPolicy.Normalize(merged, lineEnding);

        return file with
        {
            Content = normalized,
            ExistingContent = existing,
            Action = string.Equals(existing, normalized, StringComparison.Ordinal)
                ? FileAction.Unchanged
                : FileAction.Update,
        };
    }

    private static string Merge(string existing, GeneratedFile file, bool force)
    {
        // A malformed region is refused here, so the file is never opened for writing.
        if (ManagedRegion.TrySplit(existing, file.RelativePath, out var before, out var after))
        {
            return before + ManagedRegion.ExtractRegion(file.Content) + after;
        }

        return force
            ? file.Content
            : throw new UnmanagedFileException(file.RelativePath);
    }
}
