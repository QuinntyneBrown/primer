namespace Primer.Shared.Analysis;

/// <summary>What a candidate file is, established before any content is read.</summary>
internal sealed record FileFacts(long SizeBytes, bool IsBinary, bool IsReadable);

/// <summary>
/// Inspects a file before it is read. Reading is the expensive and risky part, so size and
/// kind are established from the header alone.
/// </summary>
internal sealed class FileProbe
{
    private const int HeaderBytes = 512;

    internal FileFacts Probe(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var info = new FileInfo(path);

        if (!info.Exists)
        {
            return new FileFacts(0, IsBinary: false, IsReadable: false);
        }

        try
        {
            using var stream = info.OpenRead();
            Span<byte> header = stackalloc byte[HeaderBytes];
            var read = stream.Read(header);

            return new FileFacts(info.Length, LooksBinary(header[..read]), IsReadable: true);
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            return new FileFacts(info.Length, IsBinary: false, IsReadable: false);
        }
    }

    /// <summary>
    /// A NUL byte in the header is the signal git itself uses, and it is decisive: no text
    /// encoding the tool reads produces one.
    /// </summary>
    private static bool LooksBinary(ReadOnlySpan<byte> header) => header.Contains((byte)0);
}
