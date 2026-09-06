using Primer.Shared.Hosting;
using Primer.Shared.Presentation;

namespace Primer.Shared.Analysis;

/// <summary>Confines a candidate output path to the repository.</summary>
internal interface ISafePathResolver
{
    string Resolve(string candidate);
}

/// <summary>Raised when a candidate path resolves outside the repository root.</summary>
internal sealed class PathEscapesRepositoryException(string rejectedPath, string root)
    : PrimerException(new PrimerError(
        What: "The path resolves outside the repository",
        Subject: rejectedPath,
        NextAction: $"Choose a path inside '{root}'. Primer never writes outside the repository.",
        ExitCode: ExitCode.Configuration));

/// <summary>
/// Enforces containment in one place rather than at each write site, so a traversal
/// segment, an absolute path, or a symbolic link pointing outside is refused consistently.
/// </summary>
internal sealed class SafePathResolver : ISafePathResolver
{
    private readonly string _root;

    internal SafePathResolver(RepositoryRoot root)
        : this((root ?? throw new ArgumentNullException(nameof(root))).Path)
    {
    }

    internal SafePathResolver(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        _root = ResolveLinks(Path.GetFullPath(rootPath));
    }

    public string Resolve(string candidate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(candidate);

        var combined = Path.IsPathRooted(candidate)
            ? candidate
            : Path.Combine(_root, candidate);

        var resolved = ResolveLinks(Path.GetFullPath(combined));

        return IsContained(resolved)
            ? resolved
            : throw new PathEscapesRepositoryException(candidate, _root);
    }

    private bool IsContained(string resolved)
    {
        var boundary = _root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return resolved.Equals(boundary, StringComparison.OrdinalIgnoreCase)
            || resolved.StartsWith(boundary + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || resolved.StartsWith(boundary + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Follows symbolic links to their final target. Path.GetFullPath normalises separators
    /// and traversal segments but does not follow a link, so a link is how a write would
    /// otherwise escape a boundary that looks intact.
    /// </summary>
    private static string ResolveLinks(string path)
    {
        var directory = new DirectoryInfo(path);

        if (directory.Exists)
        {
            return directory.ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? directory.FullName;
        }

        var file = new FileInfo(path);

        if (file.Exists)
        {
            return file.ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? file.FullName;
        }

        var parent = Path.GetDirectoryName(path);

        return string.IsNullOrEmpty(parent)
            ? path
            : Path.Combine(ResolveLinks(parent), Path.GetFileName(path));
    }
}
