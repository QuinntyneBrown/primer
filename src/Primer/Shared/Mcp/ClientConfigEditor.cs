using System.Text.Json;
using System.Text.Json.Nodes;

namespace Primer.Shared.Mcp;

/// <summary>
/// Registers a server in a client configuration without disturbing anything else in it.
/// The file belongs to the developer, not to Primer: entries Primer did not add keep their
/// values, and the result is valid JSON the client still loads.
/// </summary>
internal static class ClientConfigEditor
{
    private const string ServersProperty = "mcpServers";

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    /// <summary>
    /// Adds or updates the requirement's entry. Returns false when the configuration
    /// already satisfies it, so an already-satisfied entry is left exactly as it was.
    /// </summary>
    internal static bool Register(string configurationPath, McpRequirement requirement)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configurationPath);
        ArgumentNullException.ThrowIfNull(requirement);

        JsonNode root;

        try
        {
            root = JsonNode.Parse(File.ReadAllText(configurationPath)) ?? new JsonObject();
        }
        catch (JsonException malformed)
        {
            throw new MalformedClientConfigException(configurationPath, malformed);
        }

        if (root is not JsonObject document)
        {
            throw new MalformedClientConfigException(
                configurationPath, new JsonException("The root of the configuration is not an object."));
        }

        if (document[ServersProperty] is not JsonObject servers)
        {
            servers = [];
            document[ServersProperty] = servers;
        }

        if (servers[requirement.Name] is JsonObject existing
            && existing["version"]?.GetValue<string>() is { } version
            && VersionRange.IsSatisfied(version, requirement.VersionRange))
        {
            return false;
        }

        servers[requirement.Name] = new JsonObject
        {
            ["command"] = requirement.InstallMethod,
            ["version"] = requirement.VersionRange,
        };

        File.WriteAllText(configurationPath, document.ToJsonString(WriteOptions));
        return true;
    }
}
