namespace Primer.IntegrationTests.TestSupport;

/// <summary>
/// A git repository in a temporary directory, seeded with whatever files a test needs and
/// deleted on disposal. Every test that touches a repository gets its own, so the suite
/// runs in parallel and never reads or writes the developer's own working tree.
/// </summary>
internal sealed class TemporaryRepository : IDisposable
{
    internal TemporaryRepository()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "primer-test-" + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(Path);

        // A .git directory is what marks the root; the tests never invoke git itself.
        Directory.CreateDirectory(System.IO.Path.Combine(Path, ".git"));
    }

    /// <summary>Absolute path of the repository root.</summary>
    internal string Path { get; }

    /// <summary>Writes a file, creating any directories its path requires.</summary>
    internal string Write(string relativePath, string content)
    {
        var absolute = System.IO.Path.Combine(Path, relativePath);
        var directory = System.IO.Path.GetDirectoryName(absolute);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(absolute, content);
        return absolute;
    }

    /// <summary>Reads a file previously written into the repository.</summary>
    internal string Read(string relativePath) =>
        File.ReadAllText(System.IO.Path.Combine(Path, relativePath));

    /// <summary>Whether a path exists inside the repository.</summary>
    internal bool Exists(string relativePath) =>
        File.Exists(System.IO.Path.Combine(Path, relativePath));

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
        catch (IOException)
        {
            // A locked file in a temporary directory is not a test failure.
        }
        catch (UnauthorizedAccessException)
        {
            // Neither is a permission quirk on a directory the OS will reclaim.
        }
    }
}
