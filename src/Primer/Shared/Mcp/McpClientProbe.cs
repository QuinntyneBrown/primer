using System.Text.Json;
using Microsoft.Extensions.Options;
using Primer.Shared.Hosting;
using Primer.Shared.Presentation;

namespace Primer.Shared.Mcp;

/// <summary>A server a client already launches.</summary>
internal sealed record RegisteredServer(string Name, string? Version);

/// <summary>What one client's configuration reported.</summary>
internal sealed record ClientProbeResult(
    string ClientName,
    string ConfigurationPath,
    bool IsConfigured,
    IReadOnlyList<RegisteredServer> RegisteredServers);

/// <summary>Raised when a client configuration exists but cannot be parsed.</summary>
internal sealed class MalformedClientConfigException(string configurationPath, Exception cause)
    : PrimerException(
        new PrimerError(
            What: "A client configuration could not be parsed",
            Subject: configurationPath,
            NextAction: "Repair the file, or move it aside. Primer will not guess at its contents.",
            ExitCode: ExitCode.Configuration,
            Cause: cause),
        cause);

/// <summary>The clients probed when a repository declares none of its own.</summary>
internal static class McpClientDefaults
{
    internal static IReadOnlyList<McpClientOptions> BuiltIn { get; } =
    [
        new() { Name = "claude-code", ConfigPath = "~/.claude.json", Format = ClientConfigFormat.Json },
        new() { Name = "codex", ConfigPath = "~/.codex/config.toml", Format = ClientConfigFormat.Toml },
    ];
}

/// <summary>Reads the registries of the configured agent clients.</summary>
internal interface IMcpClientProbe
{
    IReadOnlyList<ClientProbeResult> ProbeAll();
}

/// <summary>
/// Probes each configured client. Locations come from configuration rather than being
/// compiled in, so a client relocating its file is a settings change rather than a release.
/// A client that is not installed is reported as such and the remaining clients are still
/// read; a file that exists but cannot be parsed is refused rather than guessed at.
/// </summary>
internal sealed class ConfiguredMcpClientProbe(
    IOptions<PrimerOptions> options,
    RepositoryLocation location) : IMcpClientProbe
{
    public IReadOnlyList<ClientProbeResult> ProbeAll()
    {
        var results = new List<ClientProbeResult>();

        IReadOnlyList<McpClientOptions> clients = options.Value.McpClients.Count > 0
            ? [.. options.Value.McpClients]
            : McpClientDefaults.BuiltIn;

        foreach (var client in clients)
        {
            var path = Resolve(client.ConfigPath);

            if (!File.Exists(path))
            {
                results.Add(new ClientProbeResult(client.Name, path, IsConfigured: false, []));
                continue;
            }

            results.Add(new ClientProbeResult(
                client.Name,
                path,
                IsConfigured: true,
                client.Format is ClientConfigFormat.Toml ? ReadToml(path) : ReadJson(path)));
        }

        return results;
    }

    /// <summary>
    /// A configured path may be absolute, may start at the user's home directory, or may be
    /// relative to the repository under analysis.
    /// </summary>
    private string Resolve(string configuredPath)
    {
        if (configuredPath.StartsWith('~'))
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                configuredPath[1..].TrimStart('/', '\\'));
        }

        return Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(location.Path, configuredPath);
    }

    private static List<RegisteredServer> ReadJson(string path)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));

            if (!document.RootElement.TryGetProperty("mcpServers", out var servers)
                || servers.ValueKind is not JsonValueKind.Object)
            {
                return [];
            }

            return
            [
                .. servers.EnumerateObject().Select(entry => new RegisteredServer(
                    entry.Name,
                    entry.Value.TryGetProperty("version", out var version) ? version.GetString() : null)),
            ];
        }
        catch (JsonException malformed)
        {
            throw new MalformedClientConfigException(path, malformed);
        }
    }

    /// <summary>
    /// Reads the section headers a TOML client uses to register servers. Only the header
    /// shape is parsed, which is what the probe needs and all it can claim to understand.
    /// </summary>
    private static List<RegisteredServer> ReadToml(string path)
    {
        var servers = new List<RegisteredServer>();
        string? current = null;

        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();

            if (line.StartsWith("[mcp_servers.", StringComparison.OrdinalIgnoreCase) && line.EndsWith(']'))
            {
                current = line["[mcp_servers.".Length..^1].Trim('"');
                servers.Add(new RegisteredServer(current, null));
            }
            else if (current is not null && line.StartsWith("version", StringComparison.OrdinalIgnoreCase))
            {
                var value = line.Split('=', 2).ElementAtOrDefault(1)?.Trim().Trim('"');
                servers[^1] = servers[^1] with { Version = value };
            }
        }

        return servers;
    }
}
