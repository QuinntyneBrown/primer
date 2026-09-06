// Acceptance Test
// Traces to: L2-007, L2-046
// Description: Verify the repository root is found by walking upward to the nearest .git
//              entry in either form, and that every candidate write is confined to it.

using Primer.IntegrationTests.TestSupport;
using Primer.Shared.Analysis;
using Primer.Shared.Hosting;

namespace Primer.IntegrationTests.RepositoryAnalysis;

public sealed class RepositoryRootTests
{
    private static readonly GitRepositoryLocator Locator = new();

    // Given a git repository with a nested directory, when a command is run against that
    // directory, then the resolved repository root is the directory containing .git.
    [Fact]
    public void Given_a_nested_directory_When_the_root_is_located_Then_it_is_the_directory_holding_dot_git()
    {
        using var repository = new TemporaryRepository();
        var nested = Path.Combine(repository.Path, "src", "Primer", "Commands");
        Directory.CreateDirectory(nested);

        var root = Locator.Locate(nested);

        Assert.Equal(
            Path.GetFullPath(repository.Path).TrimEnd(Path.DirectorySeparatorChar),
            root.Path.TrimEnd(Path.DirectorySeparatorChar));
        Assert.Equal(GitEntryKind.Directory, root.GitEntryKind);
    }

    // Given a directory that has no .git entry in itself or any ancestor,
    // when the root is located, then it fails and the outcome is exit 3.
    [Fact]
    public void Given_no_git_entry_anywhere_When_the_root_is_located_Then_it_fails_with_exit_three()
    {
        var outside = Path.Combine(Path.GetTempPath(), "primer-no-repo-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outside);

        try
        {
            var failure = Assert.Throws<RepositoryNotFoundException>(() => Locator.Locate(outside));

            Assert.Equal(ExitCode.Configuration, failure.ExitCode);
            Assert.Contains(outside, failure.Error.Subject, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(outside, recursive: true);
        }
    }

    // Given a git worktree whose .git is a file rather than a directory,
    // when the root is located, then it resolves successfully.
    [Fact]
    public void Given_a_worktree_git_file_When_the_root_is_located_Then_it_resolves()
    {
        var worktree = Path.Combine(Path.GetTempPath(), "primer-worktree-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(worktree);
        File.WriteAllText(Path.Combine(worktree, ".git"), "gitdir: /elsewhere/.git/worktrees/one\n");

        try
        {
            var root = Locator.Locate(worktree);

            Assert.Equal(GitEntryKind.WorktreeFile, root.GitEntryKind);
            Assert.Equal(
                Path.GetFullPath(worktree).TrimEnd(Path.DirectorySeparatorChar),
                root.Path.TrimEnd(Path.DirectorySeparatorChar));
        }
        finally
        {
            Directory.Delete(worktree, recursive: true);
        }
    }

    // Given any resolved output path, when it is confined,
    // then it resides within the repository root.
    [Fact]
    public void Given_a_path_inside_the_repository_When_confined_Then_it_resolves_to_an_absolute_path()
    {
        using var repository = new TemporaryRepository();
        var resolver = new SafePathResolver(Locator.Locate(repository.Path));

        var resolved = resolver.Resolve("docs/AGENTS.md");

        Assert.True(Path.IsPathRooted(resolved));
        Assert.StartsWith(
            Path.GetFullPath(repository.Path).TrimEnd(Path.DirectorySeparatorChar),
            resolved,
            StringComparison.OrdinalIgnoreCase);
    }

    // Given a configured output path containing .. segments that resolve outside the
    // repository root, when the write is confined, then it is refused with exit 3.
    [Fact]
    public void Given_traversal_segments_escaping_the_root_When_confined_Then_the_write_is_refused()
    {
        using var repository = new TemporaryRepository();
        var resolver = new SafePathResolver(Locator.Locate(repository.Path));

        var failure = Assert.Throws<PathEscapesRepositoryException>(
            () => resolver.Resolve(Path.Combine("..", "..", "escaped.md")));

        Assert.Equal(ExitCode.Configuration, failure.ExitCode);
        Assert.Contains("escaped.md", failure.Error.Subject, StringComparison.OrdinalIgnoreCase);
    }

    // Given a configured output path that is an absolute path outside the repository root,
    // when the write is confined, then it is refused with exit 3.
    [Fact]
    public void Given_an_absolute_path_outside_the_root_When_confined_Then_the_write_is_refused()
    {
        using var repository = new TemporaryRepository();
        var resolver = new SafePathResolver(Locator.Locate(repository.Path));
        var outside = Path.Combine(Path.GetTempPath(), "primer-outside-" + Guid.NewGuid().ToString("N") + ".md");

        var failure = Assert.Throws<PathEscapesRepositoryException>(() => resolver.Resolve(outside));

        Assert.Equal(ExitCode.Configuration, failure.ExitCode);
    }

    // Given a symbolic link inside the repository that resolves to a target outside it,
    // when generation would write through that link, then the write is refused.
    [Fact]
    public void Given_a_link_resolving_outside_the_root_When_confined_Then_the_write_is_refused()
    {
        using var repository = new TemporaryRepository();
        var outsideDirectory = Path.Combine(Path.GetTempPath(), "primer-link-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outsideDirectory);

        var linkPath = Path.Combine(repository.Path, "escape-link");

        try
        {
            Directory.CreateSymbolicLink(linkPath, outsideDirectory);
        }
        catch (Exception creation) when (creation is IOException or UnauthorizedAccessException)
        {
            // Creating a symbolic link needs a privilege this host does not grant.
            Assert.Skip("Symbolic links cannot be created in this environment.");
            return;
        }

        try
        {
            var resolver = new SafePathResolver(Locator.Locate(repository.Path));

            var failure = Assert.Throws<PathEscapesRepositoryException>(
                () => resolver.Resolve(Path.Combine("escape-link", "AGENTS.md")));

            Assert.Equal(ExitCode.Configuration, failure.ExitCode);
        }
        finally
        {
            Directory.Delete(outsideDirectory, recursive: true);
        }
    }
}
