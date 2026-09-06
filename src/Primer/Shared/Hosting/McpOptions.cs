namespace Primer.Shared.Hosting;

/// <summary>How a client stores its server registry.</summary>
internal enum ClientConfigFormat
{
    /// <summary>A JSON document with an mcpServers object.</summary>
    Json,

    /// <summary>A TOML document with [mcp_servers.name] sections.</summary>
    Toml,
}

/// <summary>A required MCP server, as declared in configuration.</summary>
internal sealed class McpRequirementOptions
{
    public string Name { get; set; } = string.Empty;

    public string VersionRange { get; set; } = string.Empty;

    public string InstallMethod { get; set; } = string.Empty;

    public string? SourceUrl { get; set; }

    public string? ExpectedChecksum { get; set; }
}

/// <summary>
/// Where a client keeps its registry. Declared rather than compiled in, so a client moving
/// its configuration is a settings change instead of a release.
/// </summary>
internal sealed class McpClientOptions
{
    public string Name { get; set; } = string.Empty;

    public string ConfigPath { get; set; } = string.Empty;

    public ClientConfigFormat Format { get; set; } = ClientConfigFormat.Json;
}
