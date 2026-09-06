// Acceptance Test
// Traces to: L2-063, L2-064, L2-065, L2-066, L2-067, L2-068
// Description: Verify primer init can generate guidance for a project that does not exist
//              yet, from a description rather than from repository content: the input
//              surface, deterministic archetype selection that refuses to guess, the two
//              archetypes' prescribed structure and conventions, and the content rules
//              that keep the result honest and repeatable.

using Primer.IntegrationTests.TestSupport;
using Primer.Shared.Generation;
using Primer.Shared.Hosting;

namespace Primer.IntegrationTests.Generation;

public sealed class GreenfieldGenerationTests
{
    private const string WebDescription =
        "a web app with an Angular front end in the browser and a .NET backend API";

    private const string CliDescription =
        "a command-line tool that runs in the terminal and packs as a dotnet tool";

    // The description Barnabas was actually built from. It names no technology at all,
    // which is exactly why a keyword table cannot classify it.
    private const string UndecidableDescription =
        "a private lending, giving, selling and helping board for a church congregation";

    private static Task<PrimerCliHarness.Result> Run(TemporaryRepository directory, params string[] args) =>
        PrimerCliHarness.RunAsync([.. args, "--path", directory.Path]);

    private static async Task<string> GenerateAsync(TemporaryRepository directory, params string[] args)
    {
        var result = await Run(directory, args);

        Assert.Equal((int)ExitCode.Success, result.ExitCode);
        return directory.Read("AGENTS.md");
    }

    // Given a directory that lies inside no git repository, when init runs with a
    // description, then AGENTS.md is written and the process exits 0.
    [Fact]
    public async Task Given_no_git_repository_When_init_runs_with_a_prompt_Then_it_writes_agents_md()
    {
        using var directory = TemporaryRepository.WithoutGit();

        var result = await Run(directory, "init", "--prompt", CliDescription);

        Assert.Equal((int)ExitCode.Success, result.ExitCode);
        Assert.True(directory.Exists("AGENTS.md"));
    }

    // Given --prompt-file naming a readable file, when init runs,
    // then that file's content is used as the description.
    [Fact]
    public async Task Given_a_prompt_file_When_init_runs_Then_its_content_is_used()
    {
        using var directory = TemporaryRepository.WithoutGit();
        directory.Write("brief.md", WebDescription);

        var content = await GenerateAsync(directory, "init", "--prompt-file", "brief.md");

        Assert.Contains(WebDescription, content, StringComparison.Ordinal);
    }

    // Given both --prompt and --prompt-file, when init runs,
    // then it exits 2 saying they are mutually exclusive and writes nothing.
    [Fact]
    public async Task Given_both_prompt_options_When_init_runs_Then_it_exits_two()
    {
        using var directory = TemporaryRepository.WithoutGit();
        directory.Write("brief.md", WebDescription);

        var result = await Run(directory, "init", "--prompt", WebDescription, "--prompt-file", "brief.md");

        Assert.Equal((int)ExitCode.Usage, result.ExitCode);
        Assert.Contains("mutually exclusive", result.StandardError, StringComparison.OrdinalIgnoreCase);
        Assert.False(directory.Exists("AGENTS.md"));
    }

    // Given --prompt-file naming a path that does not exist, when init runs,
    // then it exits 3 naming the path.
    [Fact]
    public async Task Given_a_missing_prompt_file_When_init_runs_Then_it_exits_three()
    {
        using var directory = TemporaryRepository.WithoutGit();

        var result = await Run(directory, "init", "--prompt-file", "nowhere.md");

        Assert.Equal((int)ExitCode.Configuration, result.ExitCode);
        Assert.Contains("nowhere.md", result.StandardError, StringComparison.Ordinal);
        Assert.False(directory.Exists("AGENTS.md"));
    }

    // Given a description that is only whitespace, when init runs, then it exits 3.
    [Fact]
    public async Task Given_an_empty_prompt_When_init_runs_Then_it_exits_three()
    {
        using var directory = TemporaryRepository.WithoutGit();

        var result = await Run(directory, "init", "--prompt", "   ");

        Assert.Equal((int)ExitCode.Configuration, result.ExitCode);
        Assert.False(directory.Exists("AGENTS.md"));
    }

    // Given --prompt together with --recursive, when init runs, then it exits 2:
    // nested guidance needs a repository to analyse.
    [Fact]
    public async Task Given_recursive_with_a_prompt_When_init_runs_Then_it_exits_two()
    {
        using var directory = TemporaryRepository.WithoutGit();

        var result = await Run(directory, "init", "--prompt", CliDescription, "--recursive");

        Assert.Equal((int)ExitCode.Usage, result.ExitCode);
        Assert.Contains("recursive", result.StandardError, StringComparison.OrdinalIgnoreCase);
        Assert.False(directory.Exists("AGENTS.md"));
    }

    // Given a description that determines no archetype and no override, when init runs,
    // then it exits 2 naming both archetypes and the option, and writes nothing.
    [Fact]
    public async Task Given_an_undecidable_description_When_init_runs_Then_it_exits_two_naming_both_archetypes()
    {
        using var directory = TemporaryRepository.WithoutGit();

        var result = await Run(directory, "init", "--prompt", UndecidableDescription);

        Assert.Equal((int)ExitCode.Usage, result.ExitCode);
        Assert.Contains("web", result.StandardError, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("cli", result.StandardError, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--archetype", result.StandardError, StringComparison.Ordinal);
        Assert.False(directory.Exists("AGENTS.md"));
    }

    // Given --archetype naming no supported archetype, when init runs,
    // then it exits 2 listing the supported values.
    [Fact]
    public async Task Given_an_unknown_archetype_value_When_init_runs_Then_it_exits_two()
    {
        using var directory = TemporaryRepository.WithoutGit();

        var result = await Run(directory, "init", "--prompt", CliDescription, "--archetype", "mainframe");

        Assert.Equal((int)ExitCode.Usage, result.ExitCode);
        Assert.Contains("mainframe", result.StandardError, StringComparison.Ordinal);
        Assert.False(directory.Exists("AGENTS.md"));
    }

    // Given --archetype web on a description a table cannot classify, when init runs,
    // then the override decides and the web guidance is produced.
    [Fact]
    public async Task Given_the_archetype_override_When_generated_Then_it_decides_what_the_description_cannot()
    {
        using var directory = TemporaryRepository.WithoutGit();

        var content = await GenerateAsync(
            directory, "init", "--prompt", UndecidableDescription, "--archetype", "web");

        Assert.Contains("backend/", content, StringComparison.Ordinal);
        Assert.Contains("frontend/", content, StringComparison.Ordinal);
    }

    // Given a description naming a browser front end and a served back end, when the file
    // is generated, then the web outline and its conventions are present.
    [Fact]
    public async Task Given_a_web_description_When_generated_Then_the_outline_and_conventions_are_present()
    {
        using var directory = TemporaryRepository.WithoutGit();

        var content = await GenerateAsync(directory, "init", "--prompt", WebDescription);

        foreach (var expected in (string[])["backend/", "frontend/", "design-system/", "e2e/"])
        {
            Assert.Contains(expected, content, StringComparison.Ordinal);
        }

        Assert.Contains("Clean Architecture", content, StringComparison.Ordinal);
        Assert.Contains("MediatR", content, StringComparison.Ordinal);
        Assert.Contains("12.5.0", content, StringComparison.Ordinal);
        Assert.Contains("Apache-2.0", content, StringComparison.Ordinal);
        Assert.Contains(".Api.Controllers", content, StringComparison.Ordinal);
        Assert.Contains("one file per type", content, StringComparison.OrdinalIgnoreCase);
    }

    // Given a directory name that is not a valid C# identifier, when the web guidance is
    // generated, then the namespace example it gives is one that would actually compile.
    // Guidance an agent cannot act on is worse than none, because it cannot tell.
    [Fact]
    public async Task Given_a_hyphenated_directory_When_generated_Then_the_namespace_example_compiles()
    {
        // The temporary directory is named "primer-test-<guid>", so this is the ordinary
        // case rather than a contrived one.
        using var directory = TemporaryRepository.WithoutGit();

        var content = await GenerateAsync(directory, "init", "--prompt", WebDescription);

        var match = System.Text.RegularExpressions.Regex.Match(content, @"`(?<ns>[^`]+)\.Api\.Controllers`");

        Assert.True(match.Success, "The guidance gives no namespace example.");
        Assert.Matches("^[A-Za-z_][A-Za-z0-9_]*$", match.Groups["ns"].Value);
    }

    // Given a web description, when the file is generated, then the folder outline is
    // an Angular multi-project workspace: every project sits under
    // frontend/projects/, and the application project is named for the repository.
    [Fact]
    public async Task Given_a_web_description_When_generated_Then_the_frontend_is_a_project_workspace()
    {
        using var directory = TemporaryRepository.WithoutGit();

        var content = await GenerateAsync(directory, "init", "--prompt", WebDescription);

        Assert.Contains("frontend/projects/", content, StringComparison.Ordinal);

        var outline = content[content.IndexOf("## Folder Structure", StringComparison.Ordinal)..];

        // The application project carries the repository's own name, beside the three
        // libraries rather than above them.
        Assert.Contains($"|-- {new DirectoryInfo(directory.Path).Name}/", outline, StringComparison.Ordinal);

        foreach (var project in (string[])["|-- api/", "|-- components/", "`-- domain/"])
        {
            Assert.Contains(project, outline, StringComparison.Ordinal);
        }

        // The single-project layout the outline used to show.
        Assert.DoesNotContain("src/app", content, StringComparison.Ordinal);
    }

    // Given a web description, when the file is generated, then the guidance requires
    // every consumed service to be reached through an interface and an InjectionToken.
    [Fact]
    public async Task Given_a_web_description_When_generated_Then_services_are_consumed_through_a_token()
    {
        using var directory = TemporaryRepository.WithoutGit();

        var content = await GenerateAsync(directory, "init", "--prompt", WebDescription);

        Assert.Contains("InjectionToken", content, StringComparison.Ordinal);
        Assert.Contains("I<Entity>Service", content, StringComparison.Ordinal);
        Assert.Contains("inject(", content, StringComparison.Ordinal);

        // The rule is worthless without the two halves that make it enforceable: a
        // consumer never naming a concrete class, and HTTP confined to the api layer.
        Assert.Contains("concrete implementation", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("HTTP", content, StringComparison.Ordinal);
    }

    // Given a web description, when the file is generated, then the guidance says which
    // project a component belongs in, by what the component knows.
    [Fact]
    public async Task Given_a_web_description_When_generated_Then_component_placement_is_stated()
    {
        using var directory = TemporaryRepository.WithoutGit();

        var content = await GenerateAsync(directory, "init", "--prompt", WebDescription);

        // The presentational tier, and what disqualifies a component from it.
        Assert.Contains("presentational", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no application service", content, StringComparison.OrdinalIgnoreCase);

        // The page tier. Naming the project matters more than naming the components:
        // an agent has to know where to put the file.
        Assert.Contains("page component", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("application project", content, StringComparison.OrdinalIgnoreCase);
    }

    // Given a web description, when the file is generated, then the design system owns
    // the tokens and a hard-coded value in a component stylesheet is a defect.
    [Fact]
    public async Task Given_a_web_description_When_generated_Then_design_tokens_are_required()
    {
        using var directory = TemporaryRepository.WithoutGit();

        var content = await GenerateAsync(directory, "init", "--prompt", WebDescription);

        Assert.Contains("design token", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("custom propert", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("var(--", content, StringComparison.Ordinal);
        Assert.Contains("hard-code", content, StringComparison.OrdinalIgnoreCase);
    }

    // Given the web archetype, when the file is generated, then the Angular workspace and
    // the design system are described as the deliverables they are.
    [Fact]
    public async Task Given_a_web_description_When_generated_Then_the_workspace_and_design_system_are_described()
    {
        using var directory = TemporaryRepository.WithoutGit();

        var content = await GenerateAsync(directory, "init", "--prompt", WebDescription);

        foreach (var library in (string[])["`api`", "`components`", "`domain`"])
        {
            Assert.Contains(library, content, StringComparison.Ordinal);
        }

        Assert.Contains("design system", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("package.json", content, StringComparison.Ordinal);
    }

    // Given a web description that also names a command-line tool, when the file is
    // generated, then the web outline holds and the tool is placed under backend/src.
    [Fact]
    public async Task Given_a_web_description_naming_a_cli_When_generated_Then_the_web_outline_holds()
    {
        using var directory = TemporaryRepository.WithoutGit();

        var content = await GenerateAsync(
            directory, "init", "--prompt", WebDescription + ", plus a command-line tool for imports");

        Assert.Contains("frontend/", content, StringComparison.Ordinal);
        Assert.Contains("backend/src", content, StringComparison.Ordinal);
    }

    // Given a description naming a command-line tool and no browser front end, when the
    // file is generated, then src and tests sit at the root and the stack is stated.
    [Fact]
    public async Task Given_a_cli_description_When_generated_Then_the_outline_and_stack_are_present()
    {
        using var directory = TemporaryRepository.WithoutGit();

        var content = await GenerateAsync(directory, "init", "--prompt", CliDescription);

        Assert.Contains("System.CommandLine", content, StringComparison.Ordinal);
        Assert.Contains("Microsoft.Extensions", content, StringComparison.Ordinal);
        Assert.Contains("SOLID", content, StringComparison.Ordinal);
        Assert.DoesNotContain("backend/", content, StringComparison.Ordinal);
        Assert.DoesNotContain("frontend/", content, StringComparison.Ordinal);
    }

    // Given any archetype, when the file is generated, then the always-applied conventions
    // are present, including the standing prohibition on architecture tests.
    [Theory]
    [InlineData(WebDescription)]
    [InlineData(CliDescription)]
    public async Task Given_any_archetype_When_generated_Then_the_standing_conventions_are_present(string description)
    {
        using var directory = TemporaryRepository.WithoutGit();

        var content = await GenerateAsync(directory, "init", "--prompt", description);

        Assert.Contains("Speed", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("radically simply", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("acceptance test", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("architecture test", content, StringComparison.OrdinalIgnoreCase);
    }

    // Given the web archetype, when the file is generated, then Playwright and the Page
    // Object Model are required and a selector is kept out of a test.
    [Fact]
    public async Task Given_a_web_description_When_generated_Then_playwright_page_objects_are_required()
    {
        using var directory = TemporaryRepository.WithoutGit();

        var content = await GenerateAsync(directory, "init", "--prompt", WebDescription);

        Assert.Contains("Playwright", content, StringComparison.Ordinal);
        Assert.Contains("Page Object Model", content, StringComparison.Ordinal);
        Assert.Contains("selector", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("integration test", content, StringComparison.OrdinalIgnoreCase);
    }

    // Given a description, when the file is generated, then it appears inside a fenced
    // block labelled as supplied content rather than as an instruction to the agent.
    [Fact]
    public async Task Given_a_description_When_generated_Then_it_is_fenced_as_supplied_content()
    {
        using var directory = TemporaryRepository.WithoutGit();

        var content = await GenerateAsync(directory, "init", "--prompt", CliDescription);

        Assert.Contains(CliDescription, content, StringComparison.Ordinal);
        Assert.Contains("```text", content, StringComparison.Ordinal);

        var quoted = content[..content.IndexOf(CliDescription, StringComparison.Ordinal)];
        Assert.Contains("```text", quoted[quoted.LastIndexOf("```text", StringComparison.Ordinal)..], StringComparison.Ordinal);
    }

    // Given any greenfield file, when it is inspected, then it carries no Domain Language
    // section: a glossary cannot be derived without reading the description for meaning.
    [Theory]
    [InlineData(WebDescription)]
    [InlineData(CliDescription)]
    public async Task Given_any_archetype_When_generated_Then_there_is_no_domain_language_section(string description)
    {
        using var directory = TemporaryRepository.WithoutGit();

        var content = await GenerateAsync(directory, "init", "--prompt", description);

        Assert.DoesNotContain("Domain Language", content, StringComparison.OrdinalIgnoreCase);
    }

    // Given any greenfield file, when its lines are counted, then it is within the ceiling
    // and was not truncated to get there.
    [Theory]
    [InlineData(WebDescription)]
    [InlineData(CliDescription)]
    public async Task Given_any_archetype_When_generated_Then_it_fits_the_budget_without_truncation(string description)
    {
        using var directory = TemporaryRepository.WithoutGit();

        var content = await GenerateAsync(directory, "init", "--prompt", description);

        Assert.True(
            content.Split('\n').Length <= 150,
            $"The generated file is {content.Split('\n').Length} lines, over the 150-line ceiling.");

        Assert.DoesNotContain("was truncated", content, StringComparison.OrdinalIgnoreCase);
    }

    // Given the same description twice, when the file is generated on each run,
    // then the two are byte-identical and the second run reports the target unchanged.
    [Fact]
    public async Task Given_the_same_description_twice_When_generated_Then_the_result_is_identical()
    {
        using var directory = TemporaryRepository.WithoutGit();

        var first = await GenerateAsync(directory, "init", "--prompt", WebDescription);
        var result = await Run(directory, "init", "--prompt", WebDescription);

        Assert.Equal((int)ExitCode.Success, result.ExitCode);
        Assert.Equal(first, directory.Read("AGENTS.md"));
        Assert.Contains("unchanged: AGENTS.md", result.StandardOutput, StringComparison.Ordinal);
    }

    // Given a greenfield run, when it completes, then the pointer files are written
    // alongside AGENTS.md, exactly as in analysis mode.
    [Fact]
    public async Task Given_a_prompt_When_init_runs_Then_the_pointer_files_are_written()
    {
        using var directory = TemporaryRepository.WithoutGit();

        await GenerateAsync(directory, "init", "--prompt", CliDescription);

        Assert.True(directory.Exists("CLAUDE.md"));
        Assert.True(directory.Exists("GEMINI.md"));
        Assert.True(directory.Exists(".github/copilot-instructions.md"));
    }

    // Given --dry-run with a description, when init runs,
    // then the intended content is reported and nothing is written.
    [Fact]
    public async Task Given_dry_run_When_init_runs_with_a_prompt_Then_nothing_is_written()
    {
        using var directory = TemporaryRepository.WithoutGit();

        var result = await Run(directory, "init", "--prompt", WebDescription, "--dry-run");

        Assert.Equal((int)ExitCode.Success, result.ExitCode);
        Assert.Contains("AGENTS.md", result.StandardOutput, StringComparison.Ordinal);
        Assert.False(directory.Exists("AGENTS.md"));
    }
}
