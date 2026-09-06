using Primer.Shared.Analysis;
using Primer.Shared.Hosting;
using Primer.Shared.Presentation;

namespace Primer.Shared.Generation;

/// <summary>Raised when a target cannot be written.</summary>
internal sealed class UnwritableTargetException(string relativePath, Exception cause)
    : PrimerException(
        new PrimerError(
            What: "The target could not be written",
            Subject: relativePath,
            NextAction: "Check the file is not read-only and that the directory is writable.",
            ExitCode: ExitCode.Configuration,
            Cause: cause),
        cause);

/// <summary>Applies a write plan.</summary>
internal interface IFileWriter
{
    IReadOnlyList<FileAction> Apply(WritePlan plan);
}

/// <summary>
/// Writes through a temporary file in the destination directory and then replaces the
/// target, so an interrupted write leaves the previous content intact rather than a
/// half-written file. A target reported unchanged is never opened for writing at all.
/// </summary>
internal sealed class AtomicFileWriter(
    RepositoryLocation location,
    LineEndingPolicy lineEndings) : IFileWriter
{
    public IReadOnlyList<FileAction> Apply(WritePlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var resolver = new SafePathResolver(location.Path);
        var lineEnding = lineEndings.Resolve();
        var applied = new List<FileAction>(plan.Entries.Count);

        foreach (var entry in plan.Entries)
        {
            if (entry.Action is FileAction.Unchanged)
            {
                // Not opening the file is what preserves its timestamp.
                applied.Add(FileAction.Unchanged);
                continue;
            }

            Write(resolver.Resolve(entry.RelativePath), entry, lineEnding);
            applied.Add(entry.Action);
        }

        return applied;
    }

    private static void Write(string absolute, GeneratedFile entry, string lineEnding)
    {
        var directory = Path.GetDirectoryName(absolute)!;
        Directory.CreateDirectory(directory);

        var temporary = Path.Combine(directory, $"{Path.GetFileName(absolute)}.{Guid.NewGuid():N}.tmp");

        try
        {
            File.WriteAllText(
                temporary,
                EncodingPolicy.Normalize(entry.Content, lineEnding),
                EncodingPolicy.Encoding);

            FilePermissions.Apply(temporary);
            File.Move(temporary, absolute, overwrite: true);
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            throw new UnwritableTargetException(entry.RelativePath, failure);
        }
        finally
        {
            // A temporary file left behind would be indistinguishable from a partial write.
            if (File.Exists(temporary))
            {
                TryDelete(temporary);
            }
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            // The run already failed; a stranded temporary file is not worth masking it.
        }
    }
}
