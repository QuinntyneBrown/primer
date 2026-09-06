using System.Text;
using Primer.Shared.Analysis;

namespace Primer.Shared.Generation;

/// <summary>
/// One section of the generated guidance. A section reports whether it has anything to say
/// before it says it, so a section with nothing behind it is dropped rather than emitted
/// with a placeholder an agent cannot distinguish from an instruction.
/// </summary>
internal interface ISection
{
    /// <summary>The template token this section fills.</summary>
    TemplateTokenName Token { get; }

    /// <summary>The heading the section renders under.</summary>
    string Heading { get; }

    bool HasContent(RepositoryContext context);

    string RenderBody(RepositoryContext context);
}

/// <summary>The tokens sections fill, named to match the template placeholders.</summary>
internal enum TemplateTokenName
{
    ProjectOverview,
    Commands,
    ProjectStructure,
    Testing,
    CodeStyle,
    GitWorkflow,
    Boundaries,
}

/// <summary>States what the repository is and what it is built with.</summary>
internal sealed class OverviewSection : ISection
{
    public TemplateTokenName Token => TemplateTokenName.ProjectOverview;

    public string Heading => "Project Overview";

    public bool HasContent(RepositoryContext context) => true;

    public string RenderBody(RepositoryContext context)
    {
        var name = new DirectoryInfo(context.Root.Path).Name;
        var body = new StringBuilder();

        body.Append("This repository is `").Append(name).AppendLine("`.");

        if (context.Stacks.Count > 0)
        {
            var stacks = string.Join(", ", context.Stacks.Select(stack => stack.Name));
            body.Append("It is built with ").Append(stacks).AppendLine(".");
        }

        return body.ToString();
    }
}

/// <summary>States the exact commands that build, test, and lint the repository.</summary>
internal sealed class CommandsSection : ISection
{
    public TemplateTokenName Token => TemplateTokenName.Commands;

    public string Heading => "Commands";

    public bool HasContent(RepositoryContext context) => context.Commands.Count > 0;

    public string RenderBody(RepositoryContext context)
    {
        var body = new StringBuilder();

        foreach (var command in context.Commands)
        {
            body.Append("- ").Append(command.Role).Append(": `")
                .Append(UntrustedContentFence.EscapeInline(command.Invocation)).AppendLine("`");
        }

        return body.ToString();
    }
}

/// <summary>Names the directories the repository is organised into.</summary>
internal sealed class StructureSection : ISection
{
    public TemplateTokenName Token => TemplateTokenName.ProjectStructure;

    public string Heading => "Project Structure";

    public bool HasContent(RepositoryContext context) => context.Structure.Directories.Count > 0;

    public string RenderBody(RepositoryContext context)
    {
        var body = new StringBuilder();

        // Only the top two levels carry orientation; deeper paths are noise in a file an
        // agent reads in full. The overall ceiling is LineBudget's to enforce, so this
        // section does not silently drop content that would otherwise earn a note.
        var listed = context.Structure.Directories
            .Where(path => path.Count(character => character == '/') < 2)
            .ToList();

        foreach (var directory in listed)
        {
            body.Append("- `").Append(directory).AppendLine("/`");
        }

        return body.ToString();
    }
}

/// <summary>States how the repository is tested.</summary>
internal sealed class TestingSection : ISection
{
    public TemplateTokenName Token => TemplateTokenName.Testing;

    public string Heading => "Testing";

    public bool HasContent(RepositoryContext context) =>
        context.Commands.Any(command => command.Role is CommandRole.Test)
        || context.Stacks.Any(stack => stack.TestFramework is not null);

    public string RenderBody(RepositoryContext context)
    {
        var body = new StringBuilder();

        foreach (var framework in context.Stacks.Select(stack => stack.TestFramework).OfType<string>().Distinct())
        {
            body.Append("- Tests use ").Append(framework).AppendLine(".");
        }

        foreach (var command in context.Commands.Where(command => command.Role is CommandRole.Test))
        {
            body.Append("- Run them with `")
                .Append(UntrustedContentFence.EscapeInline(command.Invocation)).AppendLine("`.");
        }

        return body.ToString();
    }
}

/// <summary>Points at the formatting rules the repository already declares.</summary>
internal sealed class CodeStyleSection : ISection
{
    public TemplateTokenName Token => TemplateTokenName.CodeStyle;

    public string Heading => "Code Style";

    public bool HasContent(RepositoryContext context) =>
        context.Conventions.Any(convention => convention.Kind is "formatting" or "build" or "packages");

    public string RenderBody(RepositoryContext context)
    {
        var body = new StringBuilder();

        foreach (var convention in context.Conventions.Where(c => c.Kind is "formatting" or "build" or "packages"))
        {
            body.Append("- `").Append(convention.RelativePath).Append("` is authoritative for ")
                .Append(convention.Kind).AppendLine(".");
        }

        return body.ToString();
    }
}

/// <summary>States what continuous integration runs on every change.</summary>
internal sealed class GitWorkflowSection : ISection
{
    public TemplateTokenName Token => TemplateTokenName.GitWorkflow;

    public string Heading => "Git Workflow";

    public bool HasContent(RepositoryContext context) =>
        context.Conventions.Any(convention => convention.Kind is "ci");

    public string RenderBody(RepositoryContext context)
    {
        var body = new StringBuilder();
        body.AppendLine("- Every change is checked by continuous integration:");

        foreach (var workflow in context.Conventions.Where(convention => convention.Kind is "ci"))
        {
            body.Append("  - `").Append(workflow.RelativePath).AppendLine("`");
        }

        return body.ToString();
    }
}

/// <summary>Declares what an agent shall not modify.</summary>
internal sealed class BoundariesSection : ISection
{
    private static readonly string[] GeneratedDirectories = ["bin", "obj", "node_modules", "dist", "target"];

    private static readonly string[] LockFiles =
        ["package-lock.json", "pnpm-lock.yaml", "yarn.lock", "packages.lock.json", "Cargo.lock", "poetry.lock"];

    public TemplateTokenName Token => TemplateTokenName.Boundaries;

    public string Heading => "Boundaries";

    public bool HasContent(RepositoryContext context) => true;

    public string RenderBody(RepositoryContext context)
    {
        var body = new StringBuilder();

        var generated = GeneratedDirectories
            .Where(name => Directory.Exists(Path.Combine(context.Root.Path, name))
                || context.Structure.Directories.Any(path => path.EndsWith(name, StringComparison.Ordinal)))
            .ToList();

        if (generated.Count > 0)
        {
            body.Append("- Never modify generated output: ")
                .Append(string.Join(", ", generated.Select(name => $"`{name}/`")))
                .AppendLine(".");
        }

        if (LockFiles.Any(name => File.Exists(Path.Combine(context.Root.Path, name))))
        {
            body.AppendLine("- A lock file changes only through its package manager, never by hand.");
        }

        body.AppendLine(
            "- `AGENTS.md` is generated. Regenerate it with `primer init`; edits inside the managed region are replaced.");

        return body.ToString();
    }
}
