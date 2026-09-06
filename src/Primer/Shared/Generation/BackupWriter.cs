using System.Globalization;
using Primer.Shared.Hosting;

namespace Primer.Shared.Generation;

/// <summary>
/// Copies a file's prior content before it is overwritten. A forced overwrite is still
/// recoverable, and the operator is told where the copy went.
/// </summary>
internal sealed class BackupWriter(RepositoryLocation location)
{
    internal string Backup(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        // The same writer backs up repository files and the client configurations the MCP
        // installer edits, which live outside the repository entirely.
        var source = Path.IsPathRooted(path)
            ? path
            : Path.Combine(location.Path, path);
        var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssfff", CultureInfo.InvariantCulture);
        var destination = $"{source}.{stamp}.bak";

        File.Copy(source, destination, overwrite: false);

        return destination;
    }
}
