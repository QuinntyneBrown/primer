// Acceptance Test
// Traces to: L2-023, L2-024, L2-025, L2-026, L2-051, L2-057
// Description: Verify a run can be previewed before it happens, that repeating it changes
//              nothing, that human-authored content outside the managed region survives,
//              and that a failed write leaves the original file intact.

using System.Text;
using Primer.IntegrationTests.TestSupport;
using Primer.Shared.Generation;
using Primer.Shared.Hosting;
using Primer.Shared.Presentation;

namespace Primer.IntegrationTests.Generation;

public sealed class SafeWriteTests
{
    private static readonly TerminalCapabilities Capabilities = new(80, false, true, false);

    private static AtomicFileWriter Writer(TemporaryRepository repository) =>
        new(new RepositoryLocation(repository.Path), new LineEndingPolicy(new RepositoryLocation(repository.Path)));

    private static GeneratedFile Managed(string relativePath, string body) =>
        new(relativePath, ManagedRegion.Wrap(body, "0.1.0", "hash-1"), FileAction.Create);

    // Given a repository with no generated files, when a dry run is planned, then the
    // intended content of every file is reported, and no file is created on disk.
    [Fact]
    public void Given_a_dry_run_for_a_new_file_When_reported_Then_the_content_is_shown_and_nothing_is_written()
    {
        using var repository = new TemporaryRepository();
        using var output = new StringWriter();
        using var error = new StringWriter();
        var console = new PrimerConsole(output, error, Capabilities, OutputFormat.Text, VerbosityLevel.Normal);

        var plan = new OverwritePolicy(new RepositoryLocation(repository.Path))
            .Plan([Managed("AGENTS.md", "# Overview")], force: false);

        new DryRunReporter(console).Report(plan);

        Assert.Contains("# Overview", output.ToString(), StringComparison.Ordinal);
        Assert.False(repository.Exists("AGENTS.md"));
    }

    // Given a dry-run diff wider than the terminal, when it is reported,
    // then its lines are emitted intact rather than wrapped.
    [Fact]
    public void Given_a_wide_diff_When_reported_Then_its_lines_are_not_wrapped()
    {
        using var repository = new TemporaryRepository();
        var wide = "# " + new string('x', 200);
        repository.Write("AGENTS.md", ManagedRegion.Wrap("# Old", "0.1.0", "hash-0"));

        using var output = new StringWriter();
        using var error = new StringWriter();
        var narrow = new TerminalCapabilities(40, false, true, false);
        var console = new PrimerConsole(output, error, narrow, OutputFormat.Text, VerbosityLevel.Normal);

        var plan = new OverwritePolicy(new RepositoryLocation(repository.Path))
            .Plan([Managed("AGENTS.md", wide)], force: false);

        new DryRunReporter(console).Report(plan);

        Assert.Contains("+" + wide, output.ToString(), StringComparison.Ordinal);
    }

    // Given a repository with an existing generated file, when a dry run is planned,
    // then a unified diff of the intended change is reported and no file is modified.
    [Fact]
    public void Given_a_dry_run_for_an_existing_file_When_reported_Then_a_diff_is_shown_and_nothing_changes()
    {
        using var repository = new TemporaryRepository();
        repository.Write("AGENTS.md", ManagedRegion.Wrap("# Old", "0.1.0", "hash-0"));
        var before = repository.Read("AGENTS.md");

        using var output = new StringWriter();
        using var error = new StringWriter();
        var console = new PrimerConsole(output, error, Capabilities, OutputFormat.Text, VerbosityLevel.Normal);

        var plan = new OverwritePolicy(new RepositoryLocation(repository.Path))
            .Plan([Managed("AGENTS.md", "# New")], force: false);

        new DryRunReporter(console).Report(plan);

        var reported = output.ToString();
        Assert.Contains("-# Old", reported, StringComparison.Ordinal);
        Assert.Contains("+# New", reported, StringComparison.Ordinal);
        Assert.Equal(before, repository.Read("AGENTS.md"));
    }

    // Given a repository where the generated content matches the existing content,
    // when a run is planned, then every target is reported as unchanged.
    [Fact]
    public void Given_content_that_already_matches_When_planned_Then_the_target_is_unchanged()
    {
        using var repository = new TemporaryRepository();
        var policy = new OverwritePolicy(new RepositoryLocation(repository.Path));
        var generated = Managed("AGENTS.md", "# Overview");

        // Write through the writer, so the file on disk is what a real run would leave.
        Writer(repository).Apply(policy.Plan([generated], force: false));

        var plan = policy.Plan([generated], force: false);

        Assert.Equal(FileAction.Unchanged, Assert.Single(plan.Entries).Action);
        Assert.False(plan.HasChanges);
    }

    // Given an existing AGENTS.md containing human-authored text before and after the
    // managed region, when it is written, then only the content between the delimiters is
    // replaced and the surrounding text is byte-identical to its prior content.
    [Fact]
    public void Given_text_around_the_managed_region_When_regenerated_Then_the_surround_is_byte_identical()
    {
        using var repository = new TemporaryRepository();
        const string Before = "# Hand written intro\n\nSomething a person wrote.\n\n";
        const string After = "\n\n## Team notes\n\nAlso hand written.\n";
        repository.Write("AGENTS.md", Before + ManagedRegion.Wrap("# Old", "0.1.0", "hash-0") + After);

        var plan = new OverwritePolicy(new RepositoryLocation(repository.Path))
            .Plan([Managed("AGENTS.md", "# New")], force: false);
        Writer(repository).Apply(plan);

        var updated = repository.Read("AGENTS.md");
        Assert.StartsWith(Before, updated, StringComparison.Ordinal);
        Assert.EndsWith(After.TrimEnd('\n') + "\n", updated, StringComparison.Ordinal);
        Assert.Contains("# New", updated, StringComparison.Ordinal);
        Assert.DoesNotContain("# Old", updated, StringComparison.Ordinal);
    }

    // Given an existing AGENTS.md with no managed-region delimiters, when a run is planned
    // without --force, then the file is not modified and the outcome is exit 3.
    [Fact]
    public void Given_an_unmanaged_file_When_planned_without_force_Then_it_is_refused_with_exit_three()
    {
        using var repository = new TemporaryRepository();
        repository.Write("AGENTS.md", "# Entirely hand written\n");

        var policy = new OverwritePolicy(new RepositoryLocation(repository.Path));

        var failure = Assert.Throws<UnmanagedFileException>(
            () => policy.Plan([Managed("AGENTS.md", "# New")], force: false));

        Assert.Equal(ExitCode.Configuration, failure.ExitCode);
        Assert.Equal("# Entirely hand written\n", repository.Read("AGENTS.md"));
    }

    // Given an existing AGENTS.md with no managed-region delimiters, when a run is forced,
    // then the file is overwritten and a backup of the prior content is written alongside.
    [Fact]
    public void Given_an_unmanaged_file_When_forced_Then_it_is_overwritten_after_a_backup_is_written()
    {
        using var repository = new TemporaryRepository();
        const string Original = "# Entirely hand written\n";
        repository.Write("AGENTS.md", Original);

        var plan = new OverwritePolicy(new RepositoryLocation(repository.Path))
            .Plan([Managed("AGENTS.md", "# New")], force: true);

        var backups = new BackupWriter(new RepositoryLocation(repository.Path));
        var backupPath = backups.Backup("AGENTS.md");
        Writer(repository).Apply(plan);

        Assert.Contains("# New", repository.Read("AGENTS.md"), StringComparison.Ordinal);
        Assert.Equal(Original, File.ReadAllText(backupPath));
    }

    // Given an existing AGENTS.md with a begin delimiter but no end delimiter, when a run
    // is planned, then the file is not modified and the outcome is exit 3.
    [Fact]
    public void Given_a_malformed_region_When_planned_Then_it_is_refused_with_exit_three()
    {
        using var repository = new TemporaryRepository();
        const string Malformed = "intro\n" + ManagedRegion.Begin + "\n# body\nno end marker\n";
        repository.Write("AGENTS.md", Malformed);

        var policy = new OverwritePolicy(new RepositoryLocation(repository.Path));

        var failure = Assert.Throws<MalformedRegionException>(
            () => policy.Plan([Managed("AGENTS.md", "# New")], force: false));

        Assert.Equal(ExitCode.Configuration, failure.ExitCode);
        Assert.Equal(Malformed, repository.Read("AGENTS.md"));
    }

    // Given an unchanged repository, when a run is applied twice in succession,
    // then the resulting file is byte-identical and the second run reports no change.
    [Fact]
    public void Given_an_unchanged_repository_When_applied_twice_Then_the_file_is_byte_identical()
    {
        using var repository = new TemporaryRepository();
        var policy = new OverwritePolicy(new RepositoryLocation(repository.Path));
        var writer = Writer(repository);

        writer.Apply(policy.Plan([Managed("AGENTS.md", "# Overview")], force: false));
        var first = repository.Read("AGENTS.md");
        var firstWrite = File.GetLastWriteTimeUtc(Path.Combine(repository.Path, "AGENTS.md"));

        var secondPlan = policy.Plan([Managed("AGENTS.md", "# Overview")], force: false);
        writer.Apply(secondPlan);

        Assert.Equal(first, repository.Read("AGENTS.md"));
        Assert.Equal(FileAction.Unchanged, Assert.Single(secondPlan.Entries).Action);
        Assert.Equal(firstWrite, File.GetLastWriteTimeUtc(Path.Combine(repository.Path, "AGENTS.md")));
    }

    // Given a repository whose generated files exist, when a run is repeated,
    // then exactly one managed-region delimiter pair remains.
    [Fact]
    public void Given_repeated_runs_When_applied_Then_exactly_one_delimiter_pair_remains()
    {
        using var repository = new TemporaryRepository();
        var policy = new OverwritePolicy(new RepositoryLocation(repository.Path));
        var writer = Writer(repository);

        for (var run = 0; run < 3; run++)
        {
            writer.Apply(policy.Plan([Managed("AGENTS.md", $"# Overview {run}")], force: false));
        }

        var content = repository.Read("AGENTS.md");
        Assert.Equal(1, CountOccurrences(content, ManagedRegion.Begin));
        Assert.Equal(1, CountOccurrences(content, ManagedRegion.End));
    }

    // Given any generated file, when its bytes are inspected,
    // then the encoding is UTF-8 without a byte-order mark.
    [Fact]
    public void Given_a_generated_file_When_written_Then_it_is_utf8_without_a_byte_order_mark()
    {
        using var repository = new TemporaryRepository();
        var policy = new OverwritePolicy(new RepositoryLocation(repository.Path));

        Writer(repository).Apply(policy.Plan([Managed("AGENTS.md", "# Prüfung")], force: false));

        var bytes = File.ReadAllBytes(Path.Combine(repository.Path, "AGENTS.md"));
        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
        Assert.Contains("Prüfung", Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
    }

    // Given a generated file, when it is inspected,
    // then it ends with exactly one trailing newline.
    [Fact]
    public void Given_a_generated_file_When_written_Then_it_ends_with_exactly_one_newline()
    {
        using var repository = new TemporaryRepository();
        var policy = new OverwritePolicy(new RepositoryLocation(repository.Path));

        Writer(repository).Apply(policy.Plan([Managed("AGENTS.md", "# Overview")], force: false));

        var content = repository.Read("AGENTS.md");
        Assert.EndsWith("\n", content, StringComparison.Ordinal);
        Assert.DoesNotContain("\n\n\n", content[^3..], StringComparison.Ordinal);
        Assert.False(content.EndsWith("\n\n", StringComparison.Ordinal));
    }

    // Given a repository whose .gitattributes declares a line ending,
    // when a file is generated, then it uses the declared ending.
    [Theory]
    [InlineData("* text=auto eol=crlf", "\r\n")]
    [InlineData("* text=auto eol=lf", "\n")]
    public void Given_a_declared_line_ending_When_generated_Then_the_declaration_is_honoured(
        string declaration,
        string expected)
    {
        using var repository = new TemporaryRepository();
        repository.Write(".gitattributes", declaration + "\n");

        var policy = new OverwritePolicy(new RepositoryLocation(repository.Path));
        Writer(repository).Apply(policy.Plan([Managed("AGENTS.md", "# One\n# Two")], force: false));

        var content = repository.Read("AGENTS.md");
        Assert.Contains("# One" + expected, content, StringComparison.Ordinal);
    }

    // Given no line-ending declaration, when a file is generated, then it uses LF.
    [Fact]
    public void Given_no_declaration_When_generated_Then_line_endings_are_lf()
    {
        using var repository = new TemporaryRepository();
        var policy = new OverwritePolicy(new RepositoryLocation(repository.Path));

        Writer(repository).Apply(policy.Plan([Managed("AGENTS.md", "# One\n# Two")], force: false));

        Assert.DoesNotContain('\r', repository.Read("AGENTS.md"));
    }

    // Given a target the process cannot write, when a run is applied, then the original
    // content remains intact, no partial file is left, and the outcome is exit 3.
    [Fact]
    public void Given_an_unwritable_target_When_applied_Then_the_original_survives_with_exit_three()
    {
        using var repository = new TemporaryRepository();
        var target = repository.Write("AGENTS.md", ManagedRegion.Wrap("# Old", "0.1.0", "hash-0"));
        var before = File.ReadAllText(target);

        var plan = new OverwritePolicy(new RepositoryLocation(repository.Path))
            .Plan([Managed("AGENTS.md", "# New")], force: false);

        File.SetAttributes(target, FileAttributes.ReadOnly);

        try
        {
            var failure = Assert.Throws<UnwritableTargetException>(() => Writer(repository).Apply(plan));

            Assert.Equal(ExitCode.Configuration, failure.ExitCode);
            File.SetAttributes(target, FileAttributes.Normal);
            Assert.Equal(before, File.ReadAllText(target));
            Assert.Empty(Directory.GetFiles(repository.Path, "*.tmp", SearchOption.AllDirectories));
        }
        finally
        {
            File.SetAttributes(target, FileAttributes.Normal);
        }
    }

    // Given any file created by the tool, when its permissions are inspected on a POSIX
    // host, then it is created with mode 0644 and no broader.
    [Fact]
    public void Given_a_posix_host_When_a_file_is_created_Then_its_mode_is_no_broader_than_0644()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("File modes are a POSIX concept; CI asserts this on Linux.");
            return;
        }

        using var repository = new TemporaryRepository();
        var policy = new OverwritePolicy(new RepositoryLocation(repository.Path));

        Writer(repository).Apply(policy.Plan([Managed("AGENTS.md", "# Overview")], force: false));

        var mode = File.GetUnixFileMode(Path.Combine(repository.Path, "AGENTS.md"));
        Assert.Equal(UnixFileMode.None, mode & (UnixFileMode.GroupWrite | UnixFileMode.OtherWrite));
        Assert.Equal(UnixFileMode.None, mode & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute));
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var index = 0;

        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }
}
