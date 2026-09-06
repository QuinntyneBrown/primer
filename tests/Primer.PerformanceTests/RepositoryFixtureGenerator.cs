namespace Primer.PerformanceTests;

/// <summary>
/// Synthesises a repository of a given size. The budgets in the specification are stated
/// against file counts, so the fixture is built to a count rather than to a shape.
/// </summary>
internal sealed class RepositoryFixtureGenerator : IDisposable
{
    private const int FilesPerDirectory = 500;

    internal RepositoryFixtureGenerator(int fileCount)
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "primer-perf-" + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(Path);
        Directory.CreateDirectory(System.IO.Path.Combine(Path, ".git"));

        File.WriteAllText(System.IO.Path.Combine(Path, "Primer.sln"), "Microsoft Visual Studio Solution File");
        File.WriteAllText(System.IO.Path.Combine(Path, ".editorconfig"), "root = true\n");

        Populate(fileCount);
    }

    internal string Path { get; }

    private void Populate(int fileCount)
    {
        var written = 0;
        var directoryIndex = 0;

        while (written < fileCount)
        {
            var directory = System.IO.Path.Combine(Path, "src", $"Area{directoryIndex:D4}");
            Directory.CreateDirectory(directory);

            var batch = Math.Min(FilesPerDirectory, fileCount - written);

            for (var index = 0; index < batch; index++)
            {
                File.WriteAllText(
                    System.IO.Path.Combine(directory, $"File{index:D4}.cs"),
                    "// synthesised fixture content\n");
            }

            written += batch;
            directoryIndex++;
        }
    }

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
    }
}
