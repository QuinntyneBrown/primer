using Microsoft.Extensions.Options;
using Primer.Shared.Generation;
using Primer.Shared.Hosting;
using Primer.Shared.Presentation;

namespace Primer.Shared.Mcp;

/// <summary>What installing a requirement will do.</summary>
internal enum InstallOperation
{
    /// <summary>The server is absent and will be registered.</summary>
    Install,

    /// <summary>The server is registered at a version outside the constraint.</summary>
    Upgrade,

    /// <summary>The registry already satisfies the requirement.</summary>
    AlreadySatisfied,
}

/// <summary>One change installation will apply.</summary>
internal sealed record InstallAction(
    McpRequirement Requirement,
    InstallOperation Operation,
    string TargetConfigurationPath);

/// <summary>The exact changes, displayed for consent before any is applied.</summary>
internal sealed record InstallPlan(IReadOnlyList<InstallAction> Actions)
{
    /// <summary>Whether there is anything to do.</summary>
    public bool IsEmpty => Actions.Count == 0;
}

/// <summary>
/// Installs the MCP servers a repository requires. This is the only part of the tool that
/// changes state outside the target repository, and it is built around that fact: nothing
/// is applied without consent, every artefact is verified first, and the prior
/// configuration is copied before it is touched.
/// </summary>
internal sealed class McpInstaller(
    McpGapAnalyzer analyzer,
    ArtifactVerifier verifier,
    ConsentPrompt consent,
    BackupWriter backups,
    IPrimerConsole console)
{
    internal async Task<ExitCode> InstallAsync(
        bool assumeYes,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        var report = analyzer.Analyze();
        var plan = BuildPlan(report);

        if (plan.IsEmpty)
        {
            console.WriteResult("Every required MCP server is already installed.");
            return ExitCode.Success;
        }

        console.WriteResult(Describe(plan));

        if (dryRun)
        {
            // A preview applies nothing, so consent is not sought for a change not made.
            return ExitCode.Success;
        }

        switch (consent.Confirm($"Apply {plan.Actions.Count} change(s) to your agent clients?", assumeYes))
        {
            case ConsentOutcome.Declined:
                console.WriteResult("Declined. Nothing was changed.");
                return ExitCode.Success;

            case ConsentOutcome.RequiresYesFlag:
                return ExitCode.Configuration;

            default:
                break;
        }

        foreach (var action in plan.Actions)
        {
            // Verification precedes the backup, and the backup precedes the edit. No later
            // step runs when an earlier one refuses.
            if (action.Requirement.SourceUrl is not null)
            {
                _ = await verifier.VerifyAsync(action.Requirement, cancellationToken).ConfigureAwait(false);
            }

            var backup = backups.Backup(action.TargetConfigurationPath);
            console.WriteResult($"Backed up {action.TargetConfigurationPath} to {backup}");

            if (ClientConfigEditor.Register(action.TargetConfigurationPath, action.Requirement))
            {
                console.WriteResult($"Registered {action.Requirement.Name} in {action.TargetConfigurationPath}");
            }
        }

        return ExitCode.Success;
    }

    private static InstallPlan BuildPlan(McpGapReport report)
    {
        var target = report.Clients.FirstOrDefault(client => client.IsConfigured);
        var actions = new List<InstallAction>();

        if (target is null)
        {
            return new InstallPlan(actions);
        }

        foreach (var entry in report.Entries)
        {
            var operation = entry.Status switch
            {
                McpServerStatus.Missing => InstallOperation.Install,
                McpServerStatus.Outdated => InstallOperation.Upgrade,
                _ => InstallOperation.AlreadySatisfied,
            };

            if (operation is not InstallOperation.AlreadySatisfied)
            {
                actions.Add(new InstallAction(entry.Requirement, operation, target.ConfigurationPath));
            }
        }

        return new InstallPlan(actions);
    }

    private static string Describe(InstallPlan plan) =>
        string.Join(
            '\n',
            plan.Actions
                .Select(action =>
                    $"{action.Operation.ToString().ToLowerInvariant()}: {action.Requirement.Name} "
                    + $"{action.Requirement.VersionRange} into {action.TargetConfigurationPath}")
                .Prepend("The following changes will be applied:"));
}
