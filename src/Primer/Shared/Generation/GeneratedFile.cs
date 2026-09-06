namespace Primer.Shared.Generation;

/// <summary>What writing a target will do to it.</summary>
internal enum FileAction
{
    /// <summary>The target does not exist and will be created.</summary>
    Create,

    /// <summary>The target exists and its content differs.</summary>
    Update,

    /// <summary>The target already holds exactly this content.</summary>
    Unchanged,
}

/// <summary>
/// One file the tool intends to write, and what writing it will do. The prior content
/// travels with the entry so a preview needs no file access of its own.
/// </summary>
internal sealed record GeneratedFile(
    string RelativePath,
    string Content,
    FileAction Action,
    string? ExistingContent = null);

/// <summary>
/// Every intended change, decided before the first byte is written. A dry run and a real
/// run differ only in whether the writer is then invoked.
/// </summary>
internal sealed record WritePlan(IReadOnlyList<GeneratedFile> Entries)
{
    /// <summary>Whether any target would actually change.</summary>
    public bool HasChanges => Entries.Any(entry => entry.Action is not FileAction.Unchanged);
}
