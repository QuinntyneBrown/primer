namespace Primer.Shared.Hosting;

/// <summary>
/// How much the tool says while it works. Each level includes everything the levels
/// below it emit.
/// </summary>
internal enum VerbosityLevel
{
    /// <summary>Nothing is written; the exit code alone conveys the outcome.</summary>
    Quiet = 0,

    /// <summary>Results only.</summary>
    Minimal = 1,

    /// <summary>Results and warnings.</summary>
    Normal = 2,

    /// <summary>Adds progress and diagnostic notes.</summary>
    Detailed = 3,

    /// <summary>Adds timestamped log records and exception detail.</summary>
    Diagnostic = 4,
}
