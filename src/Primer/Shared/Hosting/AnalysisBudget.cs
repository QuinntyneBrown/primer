namespace Primer.Shared.Hosting;

/// <summary>
/// Caps that bound the cost of analysing a repository. A repository may hold a hundred
/// thousand files or a two-gigabyte artefact, and neither shall exhaust the run.
/// </summary>
internal sealed class AnalysisBudget
{
    internal const int DefaultMaxDepth = 4;
    internal const int DefaultMaxFileCount = 50_000;
    internal const long DefaultMaxFileBytes = 1024 * 1024;
    internal const int DefaultNetworkTimeoutSeconds = 10;

    /// <summary>How far below the repository root traversal descends.</summary>
    public int MaxDepth { get; set; } = DefaultMaxDepth;

    /// <summary>How many files traversal visits before it halts and warns.</summary>
    public int MaxFileCount { get; set; } = DefaultMaxFileCount;

    /// <summary>The largest file whose content is read rather than recorded by name.</summary>
    public long MaxFileBytes { get; set; } = DefaultMaxFileBytes;

    /// <summary>How long a network request may take before it is abandoned.</summary>
    public TimeSpan NetworkTimeout { get; set; } = TimeSpan.FromSeconds(DefaultNetworkTimeoutSeconds);
}
