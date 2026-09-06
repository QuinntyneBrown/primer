using System.Reflection;

namespace Primer.Shared.Hosting;

/// <summary>
/// Identifies the exact build. Values are read once from compiled-in assembly attributes,
/// so version reporting computes nothing and touches no repository.
/// </summary>
internal static class BuildMetadata
{
    private const string UnknownCommit = "unknown";

    static BuildMetadata()
    {
        var assembly = typeof(BuildMetadata).Assembly;

        var informational =
            assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString(fieldCount: 3)
            ?? "0.0.0";

        InformationalVersion = informational;

        // The SDK appends "+<SourceRevisionId>" to the informational version when the
        // revision is known at build time. Anything after the first '+' is that revision.
        var separator = informational.IndexOf('+', StringComparison.Ordinal);
        CommitSha = separator >= 0 && separator < informational.Length - 1
            ? informational[(separator + 1)..]
            : UnknownCommit;
    }

    /// <summary>The Semantic Versioning 2.0.0 informational version of this build.</summary>
    internal static string InformationalVersion { get; }

    /// <summary>The source revision this build was produced from, or "unknown".</summary>
    internal static string CommitSha { get; }
}
