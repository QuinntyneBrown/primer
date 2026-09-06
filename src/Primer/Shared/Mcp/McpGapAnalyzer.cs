using Primer.Shared.Hosting;

namespace Primer.Shared.Mcp;

/// <summary>How a required server stands on this machine.</summary>
internal enum McpServerStatus
{
    /// <summary>Registered at a version inside the constraint.</summary>
    Installed,

    /// <summary>Registered at a version outside the constraint.</summary>
    Outdated,

    /// <summary>Not registered by any configured client.</summary>
    Missing,

    /// <summary>Registered, but the version could not be determined locally.</summary>
    Unknown,
}

/// <summary>One requirement and how it stands.</summary>
internal sealed record McpGapEntry(
    McpRequirement Requirement,
    McpServerStatus Status,
    string? FoundIn,
    string? FoundVersion,
    string RemediationCommand);

/// <summary>The gap between what a repository requires and what the machine has.</summary>
internal sealed record McpGapReport(
    IReadOnlyList<McpGapEntry> Entries,
    IReadOnlyList<ClientProbeResult> Clients)
{
    /// <summary>Whether any requirement is missing or outdated.</summary>
    public bool HasGap => Entries.Any(entry => entry.Status is McpServerStatus.Missing or McpServerStatus.Outdated);

    /// <summary>The outcome the check reports.</summary>
    public ExitCode ExitCode => HasGap ? ExitCode.Verification : ExitCode.Success;
}

/// <summary>Renders the exact command that resolves a requirement.</summary>
internal static class RemediationCommandBuilder
{
    internal static string Build(McpRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(requirement);

        return $"primer mcp install --yes  # installs {requirement.Name} {requirement.VersionRange} "
            + $"via {requirement.InstallMethod}";
    }
}

/// <summary>
/// Compares what a repository requires against what the configured clients register.
/// Everything determinable from local files is determined; a fact that would need a network
/// is reported as unknown rather than assumed, so the check is useful offline.
/// </summary>
internal sealed class McpGapAnalyzer(IMcpRequirementSource requirements, IMcpClientProbe probe)
{
    internal McpGapReport Analyze()
    {
        var clients = probe.ProbeAll();
        var entries = new List<McpGapEntry>();

        foreach (var requirement in requirements.GetRequirements())
        {
            entries.Add(Classify(requirement, clients));
        }

        return new McpGapReport(entries, clients);
    }

    private static McpGapEntry Classify(McpRequirement requirement, IReadOnlyList<ClientProbeResult> clients)
    {
        var remediation = RemediationCommandBuilder.Build(requirement);

        foreach (var client in clients.Where(candidate => candidate.IsConfigured))
        {
            var registered = client.RegisteredServers.FirstOrDefault(
                server => string.Equals(server.Name, requirement.Name, StringComparison.OrdinalIgnoreCase));

            if (registered is null)
            {
                continue;
            }

            if (registered.Version is null)
            {
                return new McpGapEntry(
                    requirement, McpServerStatus.Unknown, client.ConfigurationPath, null, remediation);
            }

            var status = VersionRange.IsSatisfied(registered.Version, requirement.VersionRange)
                ? McpServerStatus.Installed
                : McpServerStatus.Outdated;

            return new McpGapEntry(
                requirement, status, client.ConfigurationPath, registered.Version, remediation);
        }

        return new McpGapEntry(requirement, McpServerStatus.Missing, null, null, remediation);
    }
}

/// <summary>
/// Evaluates the constraint shapes a requirement may declare. Only shapes the tool can
/// evaluate with certainty are accepted; anything else leaves the fact undetermined.
/// </summary>
internal static class VersionRange
{
    internal static bool IsSatisfied(string version, string range)
    {
        if (string.IsNullOrWhiteSpace(range) || range is "*")
        {
            return true;
        }

        if (!Version.TryParse(Trim(version), out var actual))
        {
            return false;
        }

        if (range.StartsWith(">=", StringComparison.Ordinal))
        {
            return Version.TryParse(Trim(range[2..]), out var minimum) && actual >= minimum;
        }

        return Version.TryParse(Trim(range), out var exact) && actual == exact;
    }

    /// <summary>Drops any prerelease or build suffix so the numeric core can be compared.</summary>
    private static string Trim(string value)
    {
        var core = value.Trim().TrimStart('v', '=', ' ');
        var cut = core.IndexOfAny(['-', '+']);
        return cut < 0 ? core : core[..cut];
    }
}
