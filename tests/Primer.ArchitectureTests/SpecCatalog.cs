using System.Text.RegularExpressions;

namespace Primer.ArchitectureTests;

/// <summary>One requirement, as the specification states it.</summary>
internal sealed record RequirementRecord(string Id, string? ParentId, string Title);

/// <summary>
/// Reads the requirements from docs/specs. Identifiers are preserved exactly as written, so
/// every identifier in a test resolves back to a real line in the specification.
/// </summary>
internal static partial class SpecCatalog
{
    [GeneratedRegex(@"^##\s+(?<id>L1-\d{3}):\s*(?<title>.+)$", RegexOptions.Multiline)]
    private static partial Regex Level1Heading { get; }

    [GeneratedRegex(
        @"^##\s+(?<id>L2-\d{3}):\s*(?<title>.+?)$\s*^\*\*Traces to:\*\*\s*(?<parent>L1-\d{3})",
        RegexOptions.Multiline)]
    private static partial Regex Level2Heading { get; }

    internal static string SpecsDirectory => Path.Combine(SourceTree.Root, "docs", "specs");

    internal static IReadOnlyList<RequirementRecord> Level1() =>
    [
        .. Level1Heading
            .Matches(File.ReadAllText(Path.Combine(SpecsDirectory, "L1.md")))
            .Select(match => new RequirementRecord(
                match.Groups["id"].Value, null, match.Groups["title"].Value.Trim())),
    ];

    internal static IReadOnlyList<RequirementRecord> Level2() =>
    [
        .. Level2Heading
            .Matches(File.ReadAllText(Path.Combine(SpecsDirectory, "L2.md")))
            .Select(match => new RequirementRecord(
                match.Groups["id"].Value,
                match.Groups["parent"].Value,
                match.Groups["title"].Value.Trim())),
    ];
}

/// <summary>
/// Reads the requirement identifiers each test file declares it covers. The header comment
/// is the only link between a test and the criterion it exists to prove, so it is parsed
/// rather than trusted.
/// </summary>
internal static partial class TraceCommentScanner
{
    [GeneratedRegex(@"//\s*Traces to:\s*(?<ids>[^\r\n]+)")]
    private static partial Regex TraceHeader { get; }

    [GeneratedRegex(@"L[12]-\d{3}")]
    private static partial Regex Identifier { get; }

    /// <summary>Every test file that declares a trace, with the identifiers it names.</summary>
    internal static IReadOnlyDictionary<string, IReadOnlyList<string>> Scan()
    {
        var traced = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

        foreach (var file in SourceTree.CSharpFiles(SourceTree.Tests))
        {
            var match = TraceHeader.Match(File.ReadAllText(file));

            if (!match.Success)
            {
                continue;
            }

            traced[Path.GetFileName(file)] =
            [
                .. Identifier.Matches(match.Groups["ids"].Value).Select(identifier => identifier.Value).Distinct(),
            ];
        }

        return traced;
    }
}
