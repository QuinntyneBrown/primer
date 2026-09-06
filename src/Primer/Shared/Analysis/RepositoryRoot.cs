using Primer.Shared.Hosting;
using Primer.Shared.Presentation;

namespace Primer.Shared.Analysis;

/// <summary>The form the .git entry takes.</summary>
internal enum GitEntryKind
{
    /// <summary>An ordinary clone, where .git is a directory.</summary>
    Directory,

    /// <summary>A linked worktree, where .git is a file pointing elsewhere.</summary>
    WorktreeFile,
}

/// <summary>
/// Where the repository begins. Every downstream service takes this as its origin, and
/// nothing is written outside it.
/// </summary>
internal sealed record RepositoryRoot(string Path, GitEntryKind GitEntryKind);

/// <summary>Finds the repository a target path belongs to.</summary>
internal interface IRepositoryLocator
{
    RepositoryRoot Locate(string targetPath);
}

/// <summary>Raised when no ancestor of the target path holds a .git entry.</summary>
internal sealed class RepositoryNotFoundException(string targetPath) : PrimerException(new PrimerError(
    What: "No repository root was found",
    Subject: targetPath,
    NextAction: "Run primer from inside a git repository, or pass --path pointing at one.",
    ExitCode: ExitCode.Configuration));

/// <summary>
/// Walks upward from the target path to the nearest .git entry. A developer invoking the
/// tool from a nested source folder reaches the same root as one standing at the top.
/// </summary>
internal sealed class GitRepositoryLocator : IRepositoryLocator
{
    private const string GitEntryName = ".git";

    public RepositoryRoot Locate(string targetPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);

        var candidate = new DirectoryInfo(System.IO.Path.GetFullPath(targetPath));

        while (candidate is not null)
        {
            var entry = System.IO.Path.Combine(candidate.FullName, GitEntryName);

            if (System.IO.Directory.Exists(entry))
            {
                return new RepositoryRoot(candidate.FullName, GitEntryKind.Directory);
            }

            // A linked worktree records its real git directory in a file rather than a
            // directory. A developer working in a worktree is working in a repository.
            if (File.Exists(entry))
            {
                return new RepositoryRoot(candidate.FullName, GitEntryKind.WorktreeFile);
            }

            candidate = candidate.Parent;
        }

        throw new RepositoryNotFoundException(System.IO.Path.GetFullPath(targetPath));
    }
}
