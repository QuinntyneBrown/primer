using System.Text.Json;

namespace Primer.Shared.Analysis;

/// <summary>Produces exact, runnable commands for a repository.</summary>
internal interface ICommandInferrer
{
    /// <summary>Lower runs first; the first strategy to produce a role wins.</summary>
    int Order { get; }

    IEnumerable<InferredCommand> Infer(RepositoryRoot root);
}

/// <summary>
/// Reads the commands continuous integration actually runs. A workflow is evidence rather
/// than inference, so it is preferred over anything derived from marker files.
/// </summary>
internal sealed class WorkflowCommandInferrer : ICommandInferrer
{
    public int Order => 0;

    public IEnumerable<InferredCommand> Infer(RepositoryRoot root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var workflows = Path.Combine(root.Path, ".github", "workflows");

        if (!Directory.Exists(workflows))
        {
            yield break;
        }

        var seen = new HashSet<CommandRole>();

        foreach (var file in Directory.EnumerateFiles(workflows, "*.y*ml").OrderBy(path => path, StringComparer.Ordinal))
        {
            foreach (var line in ReadLines(file))
            {
                var trimmed = line.Trim();
                var marker = trimmed.IndexOf("run:", StringComparison.Ordinal);

                if (marker < 0)
                {
                    continue;
                }

                var invocation = trimmed[(marker + "run:".Length)..].Trim();

                if (invocation.Length == 0 || !CommandRoleReader.TryClassify(invocation, out var role))
                {
                    continue;
                }

                if (seen.Add(role))
                {
                    yield return new InferredCommand(role, invocation, PathFinder.Existing(root, invocation));
                }
            }
        }
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

/// <summary>Derives build and test commands from the solution file at the root.</summary>
internal sealed class SolutionCommandInferrer : ICommandInferrer
{
    public int Order => 1;

    public IEnumerable<InferredCommand> Infer(RepositoryRoot root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var solution = Directory
            .EnumerateFiles(root.Path, "*.sln")
            .Concat(Directory.EnumerateFiles(root.Path, "*.slnx"))
            .OrderBy(path => path, StringComparer.Ordinal)
            .FirstOrDefault();

        if (solution is null)
        {
            yield break;
        }

        var name = Path.GetFileName(solution);

        yield return new InferredCommand(
            CommandRole.Build, $"dotnet build {name} -c Release", [name]);

        yield return new InferredCommand(
            CommandRole.Test, $"dotnet test {name} -c Release", [name]);
    }
}

/// <summary>Derives commands from the scripts a package manifest declares.</summary>
internal sealed class PackageScriptInferrer : ICommandInferrer
{
    public int Order => 2;

    public IEnumerable<InferredCommand> Infer(RepositoryRoot root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var manifest = Path.Combine(root.Path, "package.json");

        if (!File.Exists(manifest))
        {
            yield break;
        }

        var manager = NodeStackDetector.NodeLockFiles
            .FirstOrDefault(entry => File.Exists(Path.Combine(root.Path, entry.LockFile)))
            .Manager ?? "npm";

        JsonElement scripts;

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(manifest));

            if (!document.RootElement.TryGetProperty("scripts", out var element))
            {
                yield break;
            }

            scripts = element.Clone();
        }
        catch (Exception failure) when (failure is IOException or JsonException)
        {
            yield break;
        }

        foreach (var (script, role) in ScriptRoles)
        {
            // The script is read as data. Its body is never executed by the tool.
            if (scripts.TryGetProperty(script, out _))
            {
                yield return new InferredCommand(role, $"{manager} run {script}", ["package.json"]);
            }
        }
    }

    private static IReadOnlyList<(string Script, CommandRole Role)> ScriptRoles { get; } =
    [
        ("build", CommandRole.Build),
        ("test", CommandRole.Test),
        ("lint", CommandRole.Lint),
    ];
}

/// <summary>Classifies an invocation by what it does.</summary>
internal static class CommandRoleReader
{
    internal static bool TryClassify(string invocation, out CommandRole role)
    {
        if (Contains(invocation, "test"))
        {
            role = CommandRole.Test;
            return true;
        }

        if (Contains(invocation, "lint") || Contains(invocation, "format"))
        {
            role = CommandRole.Lint;
            return true;
        }

        if (Contains(invocation, "build"))
        {
            role = CommandRole.Build;
            return true;
        }

        role = default;
        return false;
    }

    private static bool Contains(string invocation, string word) =>
        invocation.Contains(word, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Finds the repository paths an invocation names.</summary>
internal static class PathFinder
{
    internal static IReadOnlyList<string> Existing(RepositoryRoot root, string invocation)
    {
        var found = new List<string>();

        foreach (var token in invocation.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = token.Trim('"', '\'');

            if (candidate.StartsWith('-') || candidate.Length == 0)
            {
                continue;
            }

            if (File.Exists(Path.Combine(root.Path, candidate)))
            {
                found.Add(candidate.Replace('\\', '/'));
            }
        }

        return found;
    }
}
