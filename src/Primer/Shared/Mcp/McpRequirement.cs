using Microsoft.Extensions.Options;
using Primer.Shared.Hosting;

namespace Primer.Shared.Mcp;

/// <summary>
/// One MCP server a repository depends on. Requirements are data rather than compiled-in
/// constants, so the tool serves repositories whose dependencies its authors never saw.
/// </summary>
internal sealed record McpRequirement(
    string Name,
    string VersionRange,
    string InstallMethod,
    Uri? SourceUrl,
    string? ExpectedChecksum);

/// <summary>The requirements a run works against.</summary>
internal interface IMcpRequirementSource
{
    IReadOnlyList<McpRequirement> GetRequirements();
}

/// <summary>What a repository declaring nothing gets.</summary>
internal static class McpRequirementDefaults
{
    internal static IReadOnlyList<McpRequirement> BuiltIn { get; } =
    [
        new("filesystem", ">=0.1.0", "npx", null, null),
    ];
}

/// <summary>Reads the manifest a repository declares, falling back to the built-in set.</summary>
internal sealed class ConfigurationMcpRequirementSource(IOptions<PrimerOptions> options) : IMcpRequirementSource
{
    public IReadOnlyList<McpRequirement> GetRequirements()
    {
        var declared = options.Value.McpRequirements;

        if (declared.Count == 0)
        {
            return McpRequirementDefaults.BuiltIn;
        }

        return
        [
            .. declared.Select(entry => new McpRequirement(
                entry.Name,
                entry.VersionRange,
                entry.InstallMethod,
                string.IsNullOrWhiteSpace(entry.SourceUrl) ? null : new Uri(entry.SourceUrl),
                entry.ExpectedChecksum)),
        ];
    }
}
