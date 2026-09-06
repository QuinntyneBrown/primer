namespace Primer.IntegrationTests.TestSupport;

/// <summary>
/// Locates the Primer repository from the test binary's location, so tests do not
/// depend on the working directory the runner happens to choose.
/// </summary>
internal static class RepositoryPaths
{
    private const string SolutionFileName = "Primer.slnx";

    internal static string Root { get; } = Locate();

    internal static string ToolProject => Path.Combine(Root, "src", "Primer", "Primer.csproj");

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

        throw new InvalidOperationException(
            $"Could not locate {SolutionFileName} above {AppContext.BaseDirectory}.");
    }
}
