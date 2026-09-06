using System.Text;
using Primer.Shared.Hosting;

namespace Primer.Shared.Generation;

/// <summary>
/// Fixes the bytes a generated file is written with. Encoding is decided here rather than
/// inherited from the ambient environment, so the same repository produces the same file
/// on every machine.
/// </summary>
internal static class EncodingPolicy
{
    /// <summary>UTF-8 without a byte-order mark.</summary>
    internal static UTF8Encoding Encoding { get; } = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// Applies the declared line ending and leaves exactly one trailing newline, so a file
    /// regenerated twice is byte-identical and no editor reports a spurious change.
    /// </summary>
    internal static string Normalize(string content, string lineEnding)
    {
        ArgumentNullException.ThrowIfNull(content);

        var normalized = content.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .TrimEnd('\n');

        return normalized.Replace("\n", lineEnding, StringComparison.Ordinal) + lineEnding;
    }
}

/// <summary>
/// Resolves the line ending a repository declares. What the repository states outranks what
/// the host platform prefers.
/// </summary>
internal sealed class LineEndingPolicy(RepositoryLocation location)
{
    /// <summary>Used when the repository declares nothing.</summary>
    internal const string Default = "\n";

    internal string Resolve()
    {
        var declared = FromGitAttributes() ?? FromEditorConfig();
        return declared ?? Default;
    }

    private string? FromGitAttributes()
    {
        var path = Path.Combine(location.Path, ".gitattributes");

        if (!File.Exists(path))
        {
            return null;
        }

        foreach (var line in ReadLines(path))
        {
            if (line.Contains("eol=crlf", StringComparison.OrdinalIgnoreCase))
            {
                return "\r\n";
            }

            if (line.Contains("eol=lf", StringComparison.OrdinalIgnoreCase))
            {
                return "\n";
            }
        }

        return null;
    }

    private string? FromEditorConfig()
    {
        var path = Path.Combine(location.Path, ".editorconfig");

        if (!File.Exists(path))
        {
            return null;
        }

        foreach (var line in ReadLines(path))
        {
            var trimmed = line.Replace(" ", string.Empty, StringComparison.Ordinal);

            if (trimmed.StartsWith("end_of_line=crlf", StringComparison.OrdinalIgnoreCase))
            {
                return "\r\n";
            }

            if (trimmed.StartsWith("end_of_line=lf", StringComparison.OrdinalIgnoreCase))
            {
                return "\n";
            }
        }

        return null;
    }

    private static string[] ReadLines(string path)
    {
        try
        {
            return File.ReadAllLines(path);
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}

/// <summary>
/// Creates files readable by everyone and writable only by their owner. A generated file
/// carries no secret, but nothing is gained by making it broader than it needs to be.
/// </summary>
internal static class FilePermissions
{
    private const UnixFileMode Mode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead;

    internal static void Apply(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            File.SetUnixFileMode(path, Mode);
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            // A file system that cannot carry a mode is not a reason to fail the run.
        }
    }
}
