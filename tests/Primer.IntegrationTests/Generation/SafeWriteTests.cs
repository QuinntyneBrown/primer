// Acceptance Test
// Traces to: L2-023, L2-024, L2-025, L2-026, L2-051, L2-057
// Description: Verify a run can be previewed before it happens, that repeating it changes
//              nothing, that an existing file is replaced by generated content, and that
//              a failed write leaves the original file intact.

using System.Text;
using Primer.IntegrationTests.TestSupport;
using Primer.Shared.Generation;
using Primer.Shared.Hosting;
using Primer.Shared.Presentation;

namespace Primer.IntegrationTests.Generation;

public sealed class SafeWriteTests
{
    private static readonly TerminalCapabilities Capabilities = new(80, false, true, false);

    private static readonly char[] NewlineCharacters = ['\r', '\n'];

    private static AtomicFileWriter Writer(TemporaryRepository repository) =>
        new(new RepositoryLocation(repository.Path), new LineEndingPolicy(new RepositoryLocation(repository.Path)));

    private static GeneratedFile Generated(string relativePath, string body) =>
        new(relativePath, body, FileAction.Create);

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
            .Plan([Generated("AGENTS.md", "# Overview")]);

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
        repository.Write("AGENTS.md", "# Old\n");

        using var output = new StringWriter();
        using var error = new StringWriter();
        var narrow = new TerminalCapabilities(40, false, true, false);
        var console = new PrimerConsole(output, error, narrow, OutputFormat.Text, VerbosityLevel.Normal);

        var plan = new OverwritePolicy(new RepositoryLocation(repository.Path))
            .Plan([Generated("AGENTS.md", wide)]);

        new DryRunReporter(console).Report(plan);

        Assert.Contains("+" + wide, output.ToString(), StringComparison.Ordinal);
    }

    // Given a repository with an existing generated file, when a dry run is planned,
    // then a unified diff of the intended change is reported and no file is modified.
    [Fact]
    public void Given_a_dry_run_for_an_existing_file_When_reported_Then_a_diff_is_shown_and_nothing_changes()
    {
        using var repository = new TemporaryRepository();
        repository.Write("AGENTS.md", "# Old\n");
        var before = repository.Read("AGENTS.md");

        using var output = new StringWriter();
        using var error = new StringWriter();
        var console = new PrimerConsole(output, error, Capabilities, OutputFormat.Text, VerbosityLevel.Normal);

        var plan = new OverwritePolicy(new RepositoryLocation(repository.Path))
            .Plan([Generated("AGENTS.md", "# New")]);

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
        var generated = Generated("AGENTS.md", "# Overview");

        // Write through the writer, so the file on disk is what a real run would leave.
        Writer(repository).Apply(policy.Plan([generated]));

        var plan = policy.Plan([generated]);

        Assert.Equal(FileAction.Unchanged, Assert.Single(plan.Entries).Action);
        Assert.False(plan.HasChanges);
    }

    // Given an existing AGENTS.md a person has written or edited by hand, when a run is
    // planned and applied, then it is reported as an update and the file is replaced in
    // full by generated content.
    [Fact]
    public void Given_a_hand_edited_file_When_applied_Then_it_is_replaced_by_generated_content()
    {
        using var repository = new TemporaryRepository();
        repository.Write("AGENTS.md", "# Entirely hand written\n");

        var plan = new OverwritePolicy(new RepositoryLocation(repository.Path))
            .Plan([Generated("AGENTS.md", "# New")]);

        Assert.Equal(FileAction.Update, Assert.Single(plan.Entries).Action);

        Writer(repository).Apply(plan);

        var updated = repository.Read("AGENTS.md");
        Assert.Equal("# New\n", updated);
        Assert.DoesNotContain("hand written", updated, StringComparison.Ordinal);
    }

    // Given an unchanged repository, when a run is applied twice in succession,
    // then the resulting file is byte-identical and the second run reports no change.
    [Fact]
    public void Given_an_unchanged_repository_When_applied_twice_Then_the_file_is_byte_identical()
    {
        using var repository = new TemporaryRepository();
        var policy = new OverwritePolicy(new RepositoryLocation(repository.Path));
        var writer = Writer(repository);

        writer.Apply(policy.Plan([Generated("AGENTS.md", "# Overview")]));
        var first = repository.Read("AGENTS.md");
        var firstWrite = File.GetLastWriteTimeUtc(Path.Combine(repository.Path, "AGENTS.md"));

        var secondPlan = policy.Plan([Generated("AGENTS.md", "# Overview")]);
        writer.Apply(secondPlan);

        Assert.Equal(first, repository.Read("AGENTS.md"));
        Assert.Equal(FileAction.Unchanged, Assert.Single(secondPlan.Entries).Action);
        Assert.Equal(firstWrite, File.GetLastWriteTimeUtc(Path.Combine(repository.Path, "AGENTS.md")));
    }

    // Given an unchanged repository, when primer init is run a second time, then every
    // target path is reported unchanged and no file's last-write timestamp moves. Running
    // the real command is what proves it: the first run's own output lands in the tree the
    // second run analyses, so a run that read its own output would differ here.
    [Fact]
    public async Task Given_an_unchanged_repository_When_init_runs_twice_Then_every_target_is_unchanged()
    {
        using var repository = new TemporaryRepository();
        repository.Write("Primer.sln", "Microsoft Visual Studio Solution File");

        await PrimerCliHarness.RunAsync("init", "--path", repository.Path);

        var before = Directory
            .EnumerateFiles(repository.Path, "*", SearchOption.AllDirectories)
            .ToDictionary(path => path, File.GetLastWriteTimeUtc, StringComparer.Ordinal);

        var result = await PrimerCliHarness.RunAsync("init", "--path", repository.Path);

        Assert.Equal((int)ExitCode.Success, result.ExitCode);
        Assert.All(
            result.StandardOutput.Split(NewlineCharacters, StringSplitOptions.RemoveEmptyEntries),
            line => Assert.StartsWith("unchanged:", line.Trim(), StringComparison.Ordinal));
        Assert.Equal(
            before,
            Directory
                .EnumerateFiles(repository.Path, "*", SearchOption.AllDirectories)
                .ToDictionary(path => path, File.GetLastWriteTimeUtc, StringComparer.Ordinal));
    }

    // Given any generated file, when its bytes are inspected,
    // then the encoding is UTF-8 without a byte-order mark.
    [Fact]
    public void Given_a_generated_file_When_written_Then_it_is_utf8_without_a_byte_order_mark()
    {
        using var repository = new TemporaryRepository();
        var policy = new OverwritePolicy(new RepositoryLocation(repository.Path));

        Writer(repository).Apply(policy.Plan([Generated("AGENTS.md", "# Prüfung")]));

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

        Writer(repository).Apply(policy.Plan([Generated("AGENTS.md", "# Overview")]));

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
        Writer(repository).Apply(policy.Plan([Generated("AGENTS.md", "# One\n# Two")]));

        var content = repository.Read("AGENTS.md");
        Assert.Contains("# One" + expected, content, StringComparison.Ordinal);
    }

    // Given no line-ending declaration, when a file is generated, then it uses LF.
    [Fact]
    public void Given_no_declaration_When_generated_Then_line_endings_are_lf()
    {
        using var repository = new TemporaryRepository();
        var policy = new OverwritePolicy(new RepositoryLocation(repository.Path));

        Writer(repository).Apply(policy.Plan([Generated("AGENTS.md", "# One\n# Two")]));

        Assert.DoesNotContain('\r', repository.Read("AGENTS.md"));
    }

    // Given a target the process cannot write, when a run is applied, then the original
    // content remains intact, no partial file is left, and the outcome is exit 3.
    [Fact]
    public void Given_an_unwritable_target_When_applied_Then_the_original_survives_with_exit_three()
    {
        using var repository = new TemporaryRepository();

        // The target sits in its own directory so the lock can be applied there without
        // stopping the fixture from cleaning itself up.
        var target = repository.Write("locked/AGENTS.md", "# Old\n");
        var directory = Path.GetDirectoryName(target)!;
        var before = File.ReadAllText(target);

        var plan = new OverwritePolicy(new RepositoryLocation(repository.Path))
            .Plan([Generated("locked/AGENTS.md", "# New")]);

        // Making a target unwritable is platform-specific. On Windows a read-only file
        // cannot be replaced. On Unix, replacing a file is a rename, which needs write
        // permission on the containing directory rather than on the file itself, so a
        // read-only file there is still perfectly replaceable.
        Lock(target, directory);

        try
        {
            var failure = Assert.Throws<UnwritableTargetException>(() => Writer(repository).Apply(plan));

            Assert.Equal(ExitCode.Configuration, failure.ExitCode);
        }
        finally
        {
            Unlock(target, directory);
        }

        Assert.Equal(before, File.ReadAllText(target));
        Assert.Empty(Directory.GetFiles(repository.Path, "*.tmp", SearchOption.AllDirectories));
    }

    private static void Lock(string target, string directory)
    {
        if (OperatingSystem.IsWindows())
        {
            File.SetAttributes(target, FileAttributes.ReadOnly);
        }
        else
        {
            File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        }
    }

    private static void Unlock(string target, string directory)
    {
        if (OperatingSystem.IsWindows())
        {
            File.SetAttributes(target, FileAttributes.Normal);
        }
        else
        {
            File.SetUnixFileMode(
                directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
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

        Writer(repository).Apply(policy.Plan([Generated("AGENTS.md", "# Overview")]));

        var mode = File.GetUnixFileMode(Path.Combine(repository.Path, "AGENTS.md"));
        Assert.Equal(UnixFileMode.None, mode & (UnixFileMode.GroupWrite | UnixFileMode.OtherWrite));
        Assert.Equal(UnixFileMode.None, mode & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute));
    }
}
