using System.Globalization;
using Primer.Shared.Hosting;

namespace Primer.Shared.Generation;

/// <summary>
/// Copies a file's prior content before it is overwritten. A forced overwrite is still
/// recoverable, and the operator is told where the copy went.
/// </summary>
internal sealed class BackupWriter(RepositoryLocation location)
{
    internal string Backup(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        var source = Path.Combine(location.Path, relativePath);
        var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssfff", CultureInfo.InvariantCulture);
        var destination = $"{source}.{stamp}.bak";

        File.Copy(source, destination, overwrite: false);

        return destination;
    }
}
