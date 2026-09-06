using Microsoft.Extensions.Options;
using Primer.Shared.Hosting;

namespace Primer.Shared.Analysis;

/// <summary>What a bounded walk of the repository found.</summary>
internal sealed record ScanResult(StructureSummary Summary, IReadOnlyList<SkippedFile> Skipped);

/// <summary>Walks the repository within the analysis budget.</summary>
internal interface IStructureScanner
{
    ScanResult Scan(RepositoryRoot root);
}

/// <summary>
/// Walks the tree within the configured depth and file-count caps, skipping ignored paths
/// and tracking visited real paths so a link cycle terminates rather than recurring.
/// </summary>
internal sealed class BoundedStructureScanner(
    IIgnoreMatcher ignoreMatcher,
    FileProbe probe,
    IOptions<PrimerOptions> options) : IStructureScanner
{
    /// <summary>Paths whose content is never read, whatever the ignore rules say.</summary>
    private static readonly string[] NeverRead = [".env", ".env.local", ".env.production"];

    public ScanResult Scan(RepositoryRoot root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var budget = options.Value.Budget;
        var directories = new List<string>();
        var skipped = new List<SkippedFile>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var fileCount = 0;
        var truncated = false;

        Walk(root.Path, root.Path, depth: 0);

        directories.Sort(StringComparer.Ordinal);
        return new ScanResult(new StructureSummary(directories, truncated), skipped);

        void Walk(string directory, string rootPath, int depth)
        {
            if (truncated || depth >= budget.MaxDepth)
            {
                return;
            }

            // A link that points at an ancestor would otherwise be walked for ever.
            var real = new DirectoryInfo(directory).ResolveLinkTarget(returnFinalTarget: true)?.FullName
                ?? Path.GetFullPath(directory);

            if (!visited.Add(real))
            {
                return;
            }

            foreach (var child in SafeEnumerate(directory, isDirectory: true))
            {
                var relative = Relative(rootPath, child);

                if (relative is ".git" || ignoreMatcher.IsIgnored(relative))
                {
                    continue;
                }

                directories.Add(relative);
                Walk(child, rootPath, depth + 1);
            }

            foreach (var file in SafeEnumerate(directory, isDirectory: false))
            {
                if (fileCount >= budget.MaxFileCount)
                {
                    truncated = true;
                    return;
                }

                fileCount++;

                var relative = Relative(rootPath, file);

                if (ignoreMatcher.IsIgnored(relative))
                {
                    continue;
                }

                if (NeverRead.Contains(Path.GetFileName(file), StringComparer.OrdinalIgnoreCase))
                {
                    skipped.Add(new SkippedFile(relative, new FileInfo(file).Length, SkipReason.Ignored));
                    continue;
                }

                var facts = probe.Probe(file);

                if (!facts.IsReadable)
                {
                    skipped.Add(new SkippedFile(relative, facts.SizeBytes, SkipReason.Unreadable));
                }
                else if (facts.SizeBytes > budget.MaxFileBytes)
                {
                    skipped.Add(new SkippedFile(relative, facts.SizeBytes, SkipReason.TooLarge));
                }
                else if (facts.IsBinary)
                {
                    skipped.Add(new SkippedFile(relative, facts.SizeBytes, SkipReason.Binary));
                }
            }
        }
    }

    private static List<string> SafeEnumerate(string directory, bool isDirectory)
    {
        try
        {
            return isDirectory
                ? Directory.EnumerateDirectories(directory).ToList()
                : Directory.EnumerateFiles(directory).ToList();
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static string Relative(string root, string path) =>
        Path.GetRelativePath(root, path).Replace('\\', '/');
}
