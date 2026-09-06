using System.Text.Json;

namespace Primer.Shared.Analysis;

/// <summary>Reports the technology a repository uses, from the markers present.</summary>
internal interface IStackDetector
{
    StackDescriptor? Detect(RepositoryRoot root);
}

/// <summary>Detects a .NET repository from its solution and project files.</summary>
internal sealed class DotnetStackDetector : IStackDetector
{
    public StackDescriptor? Detect(RepositoryRoot root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var markers = MarkerFinder.Find(root.Path, ["*.sln", "*.slnx", "*.csproj", "*.fsproj"]);

        if (markers.Count == 0)
        {
            return null;
        }

        return new StackDescriptor("dotnet", markers, PackageManager: "nuget", TestFramework: TestFramework(root, markers));
    }

    private static string? TestFramework(RepositoryRoot root, IReadOnlyList<string> markers)
    {
        foreach (var marker in markers.Where(m => m.EndsWith("proj", StringComparison.OrdinalIgnoreCase)))
        {
            string content;

            try
            {
                content = File.ReadAllText(Path.Combine(root.Path, marker));
            }
            catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            if (content.Contains("xunit", StringComparison.OrdinalIgnoreCase))
            {
                return "xunit";
            }

            if (content.Contains("NUnit", StringComparison.OrdinalIgnoreCase))
            {
                return "nunit";
            }

            if (content.Contains("MSTest", StringComparison.OrdinalIgnoreCase))
            {
                return "mstest";
            }
        }

        return null;
    }
}

/// <summary>Detects a Node repository, naming the package manager its lock file implies.</summary>
internal sealed class NodeStackDetector : IStackDetector
{
    public StackDescriptor? Detect(RepositoryRoot root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var manifest = Path.Combine(root.Path, "package.json");

        if (!File.Exists(manifest))
        {
            return null;
        }

        var markers = new List<string> { "package.json" };
        string? packageManager = null;

        foreach (var (lockFile, manager) in NodeLockFiles)
        {
            if (File.Exists(Path.Combine(root.Path, lockFile)))
            {
                markers.Add(lockFile);
                packageManager ??= manager;
            }
        }

        return new StackDescriptor("node", markers, packageManager, TestFramework: null);
    }

    /// <summary>The lock file is what actually decides which manager a repository uses.</summary>
    internal static IReadOnlyList<(string LockFile, string Manager)> NodeLockFiles { get; } =
    [
        ("pnpm-lock.yaml", "pnpm"),
        ("yarn.lock", "yarn"),
        ("package-lock.json", "npm"),
    ];
}

/// <summary>Detects a Python repository from its project or requirements files.</summary>
internal sealed class PythonStackDetector : IStackDetector
{
    public StackDescriptor? Detect(RepositoryRoot root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var markers = MarkerFinder.Find(root.Path, ["pyproject.toml", "requirements.txt", "setup.py"]);

        return markers.Count == 0
            ? null
            : new StackDescriptor("python", markers, PackageManager: null, TestFramework: null);
    }
}

/// <summary>Detects a Go repository from its module file.</summary>
internal sealed class GoStackDetector : IStackDetector
{
    public StackDescriptor? Detect(RepositoryRoot root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var markers = MarkerFinder.Find(root.Path, ["go.mod"]);

        return markers.Count == 0
            ? null
            : new StackDescriptor("go", markers, PackageManager: "go modules", TestFramework: null);
    }
}

/// <summary>Finds marker files near the root without walking the whole repository.</summary>
internal static class MarkerFinder
{
    private const int MaxDepth = 3;

    internal static IReadOnlyList<string> Find(string root, IReadOnlyList<string> patterns)
    {
        var found = new List<string>();

        foreach (var pattern in patterns)
        {
            foreach (var path in Enumerate(root, pattern))
            {
                var relative = Path.GetRelativePath(root, path).Replace('\\', '/');

                if (!relative.StartsWith("bin/", StringComparison.OrdinalIgnoreCase)
                    && !relative.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
                    && !relative.Contains("/obj/", StringComparison.OrdinalIgnoreCase))
                {
                    found.Add(relative);
                }
            }
        }

        found.Sort(StringComparer.Ordinal);
        return found;
    }

    private static List<string> Enumerate(string root, string pattern)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            MaxRecursionDepth = MaxDepth,
            IgnoreInaccessible = true,
        };

        try
        {
            return Directory.EnumerateFiles(root, pattern, options).ToList();
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}
