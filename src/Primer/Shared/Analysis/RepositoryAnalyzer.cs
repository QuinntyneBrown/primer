using Primer.Shared.Presentation;

namespace Primer.Shared.Analysis;

/// <summary>Derives the facts generation is allowed to state.</summary>
internal interface IRepositoryAnalyzer
{
    RepositoryContext Analyze(RepositoryRoot root);
}

/// <summary>
/// Orchestrates one read-only pass. Every detector that matches contributes, the first
/// inferrer to produce a role wins, and every retained value passes the redactor.
/// </summary>
internal sealed class RepositoryAnalyzer(
    IEnumerable<IStackDetector> detectors,
    IEnumerable<ICommandInferrer> inferrers,
    IStructureScanner scanner,
    IConventionDetector conventions,
    ISecretRedactor redactor,
    IPrimerConsole console) : IRepositoryAnalyzer
{
    public RepositoryContext Analyze(RepositoryRoot root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var stacks = detectors
            .Select(detector => detector.Detect(root))
            .OfType<StackDescriptor>()
            .OrderBy(stack => stack.Name, StringComparer.Ordinal)
            .ToList();

        var commands = InferCommands(root);
        var scan = scanner.Scan(root);

        foreach (var unreadable in scan.Skipped.Where(file => file.Reason == SkipReason.Unreadable))
        {
            console.WriteWarning($"'{unreadable.RelativePath}' could not be read and was skipped.");
        }

        if (scan.Summary.TruncatedAtCap)
        {
            console.WriteWarning("The file-count cap was reached; the structure summary is partial.");
        }

        return new RepositoryContext(
            root,
            stacks,
            commands,
            scan.Summary,
            conventions.Detect(root),
            scan.Skipped);
    }

    private List<InferredCommand> InferCommands(RepositoryRoot root)
    {
        var byRole = new Dictionary<CommandRole, InferredCommand>();

        foreach (var inferrer in inferrers.OrderBy(strategy => strategy.Order))
        {
            foreach (var command in inferrer.Infer(root))
            {
                // The first strategy to produce a role wins, so evidence beats inference.
                if (byRole.ContainsKey(command.Role))
                {
                    continue;
                }

                byRole[command.Role] = command with { Invocation = redactor.Redact(command.Invocation) };
            }
        }

        return [.. byRole.Values.OrderBy(command => command.Role)];
    }
}
