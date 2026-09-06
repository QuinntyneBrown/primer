// Acceptance Test
// Traces to: L2-019, L2-020, L2-021, L2-022, L2-024
// Description: Verify each supported agent's file is written to that tool's conventional
//              path, that CLAUDE.md is a bare import and nothing else, that --agent
//              selection behaves, and that an unsupported value changes nothing.

using Primer.IntegrationTests.TestSupport;
using Primer.Shared.Generation;
using Primer.Shared.Hosting;

namespace Primer.IntegrationTests.Generation;

public sealed class PointerFileTests
{
    private static List<GeneratedFile> Generate(params string[] agents)
    {
        var selection = agents.Length == 0
            ? AgentSelection.Default()
            : AgentSelection.Parse(agents);

        return selection.Targets.Select(PointerFileGenerator.Generate).OfType<GeneratedFile>().ToList();
    }

    // Given a repository with no CLAUDE.md, when generation runs, then CLAUDE.md exists and
    // its only non-empty line is the import directive.
    [Fact]
    public void Given_the_claude_target_When_generated_Then_its_only_non_empty_line_is_the_import()
    {
        var claude = Assert.Single(Generate("claude"), file => file.RelativePath == "CLAUDE.md");

        var lines = claude.Content
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToList();

        Assert.Equal("@AGENTS.md", Assert.Single(lines));
    }

    // Given a generated CLAUDE.md, when its bytes are inspected,
    // then the file ends with exactly one trailing newline once written.
    [Fact]
    public void Given_the_claude_target_When_written_Then_it_ends_with_exactly_one_newline()
    {
        using var repository = new TemporaryRepository();
        var location = new RepositoryLocation(repository.Path);
        var plan = new OverwritePolicy(location).Plan(Generate("claude"), force: false);

        new AtomicFileWriter(location, new LineEndingPolicy(location)).Apply(plan);

        var content = repository.Read("CLAUDE.md");
        Assert.Equal("@AGENTS.md\n", content);
    }

    // Given a generated CLAUDE.md and an AGENTS.md, when the two are compared,
    // then no heading present in AGENTS.md appears in CLAUDE.md.
    [Fact]
    public void Given_the_claude_target_When_compared_with_agents_Then_it_restates_no_heading()
    {
        var claude = Assert.Single(Generate("claude"), file => file.RelativePath == "CLAUDE.md");

        Assert.DoesNotContain("##", claude.Content, StringComparison.Ordinal);
    }

    // Given no --agent option, when generation runs, then AGENTS.md and CLAUDE.md alone are
    // written and no other tool-specific file is created.
    [Fact]
    public void Given_no_agent_option_When_generated_Then_only_the_claude_pointer_is_produced()
    {
        var files = Generate();

        Assert.Equal("CLAUDE.md", Assert.Single(files).RelativePath);
    }

    // Given --agent gemini --agent copilot, when generation runs, then the Gemini and
    // Copilot files are written and CLAUDE.md is not created.
    [Fact]
    public void Given_gemini_and_copilot_When_generated_Then_those_files_are_produced_without_claude()
    {
        var paths = Generate("gemini", "copilot").Select(file => file.RelativePath).ToList();

        Assert.Contains("GEMINI.md", paths);
        Assert.Contains(".github/copilot-instructions.md", paths);
        Assert.DoesNotContain("CLAUDE.md", paths);
    }

    // Given --agent all, when generation runs,
    // then the instruction file for every supported agent is written.
    [Fact]
    public void Given_all_When_generated_Then_every_supported_agent_file_is_produced()
    {
        var paths = Generate("all").Select(file => file.RelativePath).ToList();

        Assert.Contains("CLAUDE.md", paths);
        Assert.Contains("GEMINI.md", paths);
        Assert.Contains(".github/copilot-instructions.md", paths);
    }

    // Given a repeated --agent value, when generation runs, then the file is written once.
    [Fact]
    public void Given_a_repeated_agent_value_When_generated_Then_the_file_is_produced_once()
    {
        Assert.Single(Generate("claude", "claude"), file => file.RelativePath == "CLAUDE.md");
    }

    // Given --agent codex, when generation runs, then AGENTS.md is treated as the native
    // instruction file and no additional Codex-specific file is created.
    [Fact]
    public void Given_codex_When_generated_Then_no_additional_file_is_produced()
    {
        Assert.Empty(Generate("codex"));
    }

    // Given a value in a different case, when it is parsed,
    // then it is matched case-insensitively.
    [Fact]
    public void Given_an_agent_value_in_a_different_case_When_parsed_Then_it_still_resolves()
    {
        Assert.Single(Generate("CLAUDE"), file => file.RelativePath == "CLAUDE.md");
    }

    // Given an unsupported --agent value, when it is parsed, then the failure names the
    // supported values and reports exit 2.
    [Fact]
    public void Given_an_unsupported_agent_value_When_parsed_Then_it_names_the_supported_values()
    {
        var failure = Assert.Throws<UnsupportedAgentException>(() => AgentSelection.Parse(["notarealagent"]));

        Assert.Equal(ExitCode.Usage, failure.ExitCode);
        Assert.Contains("notarealagent", failure.Error.Subject, StringComparison.Ordinal);
        Assert.Contains("claude", failure.Error.NextAction, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("codex", failure.Error.NextAction, StringComparison.OrdinalIgnoreCase);
    }

    // Given a target that supports no import directive, when its file is generated,
    // then it directs the agent to AGENTS.md and restates nothing.
    [Fact]
    public void Given_an_instruction_target_When_generated_Then_it_directs_to_agents_without_restating_it()
    {
        var gemini = Assert.Single(Generate("gemini"), file => file.RelativePath == "GEMINI.md");

        Assert.Contains("AGENTS.md", gemini.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("## Commands", gemini.Content, StringComparison.Ordinal);
        Assert.True(gemini.Content.Split('\n').Length <= 10);
    }

    // Given an existing pointer file whose content differs from the generated pointer, when
    // a run is planned without --force, then the file is not modified and the outcome is 3.
    [Fact]
    public void Given_a_differing_pointer_file_When_planned_without_force_Then_it_is_refused()
    {
        using var repository = new TemporaryRepository();
        const string HandWritten = "# My own CLAUDE.md\n";
        repository.Write("CLAUDE.md", HandWritten);

        var policy = new OverwritePolicy(new RepositoryLocation(repository.Path));

        var failure = Assert.Throws<UnmanagedFileException>(() => policy.Plan(Generate("claude"), force: false));

        Assert.Equal(ExitCode.Configuration, failure.ExitCode);
        Assert.Equal(HandWritten, repository.Read("CLAUDE.md"));
    }

    // Given an existing pointer file whose content already equals the generated pointer,
    // when a run is planned, then it is reported as unchanged.
    [Fact]
    public void Given_a_matching_pointer_file_When_planned_Then_it_is_reported_unchanged()
    {
        using var repository = new TemporaryRepository();
        var location = new RepositoryLocation(repository.Path);
        var policy = new OverwritePolicy(location);

        new AtomicFileWriter(location, new LineEndingPolicy(location))
            .Apply(policy.Plan(Generate("claude"), force: false));

        var plan = policy.Plan(Generate("claude"), force: false);

        Assert.Equal(FileAction.Unchanged, Assert.Single(plan.Entries).Action);
    }
}
