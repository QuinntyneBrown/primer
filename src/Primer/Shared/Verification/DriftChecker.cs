using Microsoft.Extensions.Options;
using Primer.Shared.Analysis;
using Primer.Shared.Generation;
using Primer.Shared.Hosting;
using Primer.Shared.Presentation;

namespace Primer.Shared.Verification;

/// <summary>How a committed generated file stands against current output.</summary>
internal enum DriftKind
{
    /// <summary>The committed file is what generation would produce now.</summary>
    Current,

    /// <summary>The committed file differs from current output.</summary>
    Divergent,

    /// <summary>No committed file exists.</summary>
    Missing,
}

/// <summary>One target and how it stands.</summary>
internal sealed record DriftEntry(string RelativePath, DriftKind Kind);

/// <summary>Every target and whether any has drifted.</summary>
internal sealed record DriftReport(IReadOnlyList<DriftEntry> Entries)
{
    /// <summary>Whether any target is divergent or missing.</summary>
    public bool HasDrift => Entries.Any(entry => entry.Kind is not DriftKind.Current);

    /// <summary>The outcome the check reports.</summary>
    public ExitCode ExitCode => HasDrift ? ExitCode.Verification : ExitCode.Success;
}

/// <summary>
/// Detects divergence by regenerating in memory and comparing against disk. It consults no
/// recorded timestamp and no stored marker, either of which could itself be stale, and it
/// resolves the same generator services a real run uses, so the two cannot disagree about
/// what correct looks like.
/// </summary>
internal sealed class DriftChecker(
    IRepositoryLocator locator,
    IRepositoryAnalyzer analyzer,
    IAgentsFileGenerator generator,
    OverwritePolicy policy,
    RepositoryLocation location,
    IOptions<PrimerOptions> options)
{
    internal DriftReport Compare(bool recursive)
    {
        var root = locator.Locate(location.Path);
        var context = analyzer.Analyze(root);

        var expected = generator.Generate(context, recursive)
            .Concat(Pointers())
            .ToList();

        var entries = new List<DriftEntry>(expected.Count);

        foreach (var file in expected)
        {
            entries.Add(new DriftEntry(file.RelativePath, Classify(file)));
        }

        return new DriftReport(entries);
    }

    private IEnumerable<GeneratedFile> Pointers()
    {
        var configured = options.Value.Agents;

        var selection = configured.Count == 0
            ? AgentSelection.Default()
            : AgentSelection.Parse([.. configured]);

        return selection.Targets.Select(PointerFileGenerator.Generate).OfType<GeneratedFile>();
    }

    private DriftKind Classify(GeneratedFile file)
    {
        if (!File.Exists(Path.Combine(location.Path, file.RelativePath)))
        {
            return DriftKind.Missing;
        }

        var planned = policy.Plan([file]).Entries.Single();

        return planned.Action is FileAction.Unchanged ? DriftKind.Current : DriftKind.Divergent;
    }
}
