using Primer.Shared.Hosting;
using Primer.Shared.Presentation;

namespace Primer.Shared.Generation;

/// <summary>How an agent's instruction file reaches AGENTS.md.</summary>
internal enum PointerMechanism
{
    /// <summary>The agent reads AGENTS.md itself; no additional file is written.</summary>
    Native,

    /// <summary>The agent resolves an import directive, loading AGENTS.md into context.</summary>
    Import,

    /// <summary>The agent is directed to read AGENTS.md before proceeding.</summary>
    Instruction,
}

/// <summary>One supported coding agent, its conventional file, and how it is pointed.</summary>
internal sealed record AgentTarget(string Name, string RelativePath, PointerMechanism Mechanism);

/// <summary>Raised when an --agent value names no supported tool.</summary>
internal sealed class UnsupportedAgentException(string value, IReadOnlyList<string> supported)
    : PrimerException(new PrimerError(
        What: "That agent is not supported",
        Subject: value,
        NextAction: $"Choose one of: {string.Join(", ", supported)}, or 'all'.",
        ExitCode: ExitCode.Usage));

/// <summary>The fixed set of agents the tool knows how to point at AGENTS.md.</summary>
internal static class AgentTargetRegistry
{
    /// <summary>Selects every supported target.</summary>
    internal const string AllValue = "all";

    internal static IReadOnlyList<AgentTarget> Supported { get; } =
    [
        new("codex", "AGENTS.md", PointerMechanism.Native),
        new("claude", "CLAUDE.md", PointerMechanism.Import),
        new("gemini", "GEMINI.md", PointerMechanism.Instruction),
        new("copilot", ".github/copilot-instructions.md", PointerMechanism.Instruction),
    ];

    internal static IReadOnlyList<string> SupportedNames { get; } =
        [.. Supported.Select(target => target.Name)];

    /// <summary>
    /// Every repository-relative path a run can write. Analysis disregards these, because
    /// generation output must never become generation input.
    /// </summary>
    internal static IReadOnlySet<string> GeneratedPaths { get; } = Supported
        .Select(target => target.RelativePath)
        .Append(AgentsFileGenerator.FileName)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    internal static AgentTarget Resolve(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        return Supported.FirstOrDefault(
                target => string.Equals(target.Name, name.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new UnsupportedAgentException(name, SupportedNames);
    }
}

/// <summary>
/// The agents a run writes files for. Values are de-duplicated, so naming one twice writes
/// its file once.
/// </summary>
internal sealed record AgentSelection(IReadOnlyList<AgentTarget> Targets)
{
    /// <summary>
    /// Absent an explicit selection, a run writes a file for every supported agent. The
    /// --agent option narrows that set rather than opting into it: an unwanted pointer costs
    /// one line, while a missing one costs that tool its guidance entirely.
    /// </summary>
    internal static AgentSelection Default() => new(AgentTargetRegistry.Supported);

    internal static AgentSelection Parse(IReadOnlyList<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (values.Count == 0)
        {
            return Default();
        }

        if (values.Any(value => string.Equals(value.Trim(), AgentTargetRegistry.AllValue, StringComparison.OrdinalIgnoreCase)))
        {
            return new AgentSelection(AgentTargetRegistry.Supported);
        }

        var targets = new List<AgentTarget>();

        foreach (var value in values)
        {
            var target = AgentTargetRegistry.Resolve(value);

            if (!targets.Contains(target))
            {
                targets.Add(target);
            }
        }

        return new AgentSelection(targets);
    }
}

/// <summary>
/// Emits an agent's instruction file according to its mechanism. Every file points at
/// AGENTS.md and restates none of it, so three of the four cannot go stale.
/// </summary>
internal static class PointerFileGenerator
{
    /// <summary>The directive Claude Code resolves at load time.</summary>
    internal const string ImportDirective = "@" + AgentsFileGenerator.FileName;

    internal static GeneratedFile? Generate(AgentTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);

        return target.Mechanism switch
        {
            // Codex reads AGENTS.md natively, so selecting it adds no output.
            PointerMechanism.Native => null,

            // An import resolves: an agent reading this file alone obtains the whole of
            // AGENTS.md, with nothing duplicated here to drift out of agreement with it.
            PointerMechanism.Import => new GeneratedFile(
                target.RelativePath, ImportDirective, FileAction.Create),

            _ => new GeneratedFile(
                target.RelativePath,
                $"Read [{AgentsFileGenerator.FileName}](./{AgentsFileGenerator.FileName}) at the repository root "
                + "before working in this repository.\n"
                + "It is the single source of guidance; this file adds nothing of its own.",
                FileAction.Create),
        };
    }
}
