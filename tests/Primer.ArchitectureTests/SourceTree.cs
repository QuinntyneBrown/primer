namespace Primer.ArchitectureTests;

/// <summary>
/// Locates the repository and the source it holds, so the structural rules are asserted
/// against the tree itself rather than against a description of it.
/// </summary>
internal static class SourceTree
{
    private const string SolutionFileName = "Primer.slnx";

    internal static string Root { get; } = Locate();

    internal static string Source => Path.Combine(Root, "src");

    internal static string Tests => Path.Combine(Root, "tests");

    internal static string Features => Path.Combine(Source, "Primer", "Features");

    internal static IReadOnlyList<string> ProjectFiles(string directory) =>
        Directory.Exists(directory)
            ? [.. Directory.EnumerateFiles(directory, "*.csproj", SearchOption.AllDirectories)]
            : [];

    internal static IReadOnlyList<string> CSharpFiles(string directory) =>
        Directory.Exists(directory)
            ? [.. Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))]
            : [];

    private static string Locate()
    {
        var candidate = new DirectoryInfo(AppContext.BaseDirectory);

        while (candidate is not null)
        {
            if (File.Exists(Path.Combine(candidate.FullName, SolutionFileName)))
            {
                return candidate.FullName;
            }

            candidate = candidate.Parent;
        }

        throw new InvalidOperationException($"Could not locate {SolutionFileName}.");
    }
}
