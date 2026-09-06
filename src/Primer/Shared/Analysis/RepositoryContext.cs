namespace Primer.Shared.Analysis;

/// <summary>A technology detected from the marker files present in the repository.</summary>
internal sealed record StackDescriptor(
    string Name,
    IReadOnlyList<string> MarkerFiles,
    string? PackageManager,
    string? TestFramework);

/// <summary>What an inferred command is for.</summary>
internal enum CommandRole
{
    Build,
    Test,
    Lint,
}

/// <summary>
/// An exact, runnable invocation. Flags are part of the command: a bare tool name is not
/// reproducible, and guidance an agent cannot run is worse than none.
/// </summary>
internal sealed record InferredCommand(
    CommandRole Role,
    string Invocation,
    IReadOnlyList<string> ReferencedPaths);

/// <summary>The directory layout, within the traversal bounds.</summary>
internal sealed record StructureSummary(IReadOnlyList<string> Directories, bool TruncatedAtCap);

/// <summary>A convention the repository already declares.</summary>
internal sealed record ConventionDescriptor(string Kind, string RelativePath);

/// <summary>Why a file's content was not read.</summary>
internal enum SkipReason
{
    /// <summary>Larger than the configured per-file read cap.</summary>
    TooLarge,

    /// <summary>Its leading bytes indicate it is not text.</summary>
    Binary,

    /// <summary>The process lacks permission to read it.</summary>
    Unreadable,

    /// <summary>Excluded by ignore rules, or never read by policy.</summary>
    Ignored,
}

/// <summary>A file recorded by name and size rather than by content.</summary>
internal sealed record SkippedFile(string RelativePath, long SizeBytes, SkipReason Reason);

/// <summary>
/// Everything analysis learned. This is the single input generation consumes, so a fact
/// absent here is a fact generation cannot state.
/// </summary>
internal sealed record RepositoryContext(
    RepositoryRoot Root,
    IReadOnlyList<StackDescriptor> Stacks,
    IReadOnlyList<InferredCommand> Commands,
    StructureSummary Structure,
    IReadOnlyList<ConventionDescriptor> Conventions,
    IReadOnlyList<SkippedFile> SkippedFiles);
