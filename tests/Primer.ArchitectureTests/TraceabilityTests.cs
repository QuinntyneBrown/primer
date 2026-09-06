// Acceptance Test
// Traces to: L2-042, L2-043, L2-044, L2-045
// Description: Verify every requirement reaches a test and every traced identifier exists,
//              that the suite is hermetic and parallel-safe, and that continuous
//              integration runs every gate.

namespace Primer.ArchitectureTests;

public sealed class TraceabilityTests
{
    // Given docs/specs and the test projects, when the traceability check is run,
    // then every L2 identifier is referenced by at least one test file.
    [Fact]
    public void Given_the_specification_When_checked_Then_every_requirement_reaches_a_test()
    {
        var covered = TraceCommentScanner.Scan().Values.SelectMany(ids => ids).ToHashSet(StringComparer.Ordinal);

        var uncovered = SpecCatalog.Level2()
            .Select(requirement => requirement.Id)
            .Where(id => !covered.Contains(id))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            uncovered.Count == 0,
            $"These L2 requirements have no covering test: {string.Join(", ", uncovered)}");
    }

    // Given the test projects, when the traceability check is run,
    // then every requirement identifier referenced by a test exists in the specification.
    [Fact]
    public void Given_the_tests_When_checked_Then_every_traced_identifier_exists()
    {
        var known = SpecCatalog.Level1()
            .Concat(SpecCatalog.Level2())
            .Select(requirement => requirement.Id)
            .ToHashSet(StringComparer.Ordinal);

        var dangling = TraceCommentScanner.Scan()
            .SelectMany(entry => entry.Value.Select(id => (File: entry.Key, Id: id)))
            .Where(traced => !known.Contains(traced.Id))
            .ToList();

        Assert.True(
            dangling.Count == 0,
            "These tests trace to identifiers that do not exist: "
                + string.Join(", ", dangling.Select(traced => $"{traced.File} -> {traced.Id}")));
    }

    // Given docs/specs, when the traceability check is run,
    // then every L2 names exactly one existing L1.
    [Fact]
    public void Given_the_specification_When_checked_Then_every_l2_refines_one_existing_l1()
    {
        var level1 = SpecCatalog.Level1().Select(requirement => requirement.Id).ToHashSet(StringComparer.Ordinal);
        var level2 = SpecCatalog.Level2();

        Assert.NotEmpty(level2);
        Assert.All(
            level2,
            requirement => Assert.True(
                requirement.ParentId is not null && level1.Contains(requirement.ParentId),
                $"{requirement.Id} refines '{requirement.ParentId}', which is not a known L1."));
    }

    // Given docs/specs, when the traceability check is run,
    // then every L1 is refined by at least one L2.
    [Fact]
    public void Given_the_specification_When_checked_Then_every_l1_is_refined()
    {
        var refined = SpecCatalog.Level2().Select(requirement => requirement.ParentId).ToHashSet(StringComparer.Ordinal);

        var orphaned = SpecCatalog.Level1()
            .Select(requirement => requirement.Id)
            .Where(id => !refined.Contains(id))
            .ToList();

        Assert.True(orphaned.Count == 0, $"These L1 requirements have no L2: {string.Join(", ", orphaned)}");
    }

    // Given any test file, when it is inspected, then it carries a header comment naming
    // the requirement identifiers it covers.
    [Fact]
    public void Given_a_test_file_When_inspected_Then_it_declares_what_it_covers()
    {
        var traced = TraceCommentScanner.Scan();

        var untraced = SourceTree.CSharpFiles(SourceTree.Tests)
            .Select(Path.GetFileName)
            .OfType<string>()
            .Where(name => name.EndsWith("Tests.cs", StringComparison.Ordinal))
            .Where(name => !traced.ContainsKey(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            untraced.Count == 0,
            $"These test files declare no requirement coverage: {string.Join(", ", untraced)}");
    }

    // Given any integration test, when it runs, then it operates against a temporary
    // repository rather than the developer's own working tree.
    [Fact]
    public void Given_the_integration_tests_When_inspected_Then_they_use_a_temporary_repository()
    {
        var touchingRepositories = SourceTree
            .CSharpFiles(Path.Combine(SourceTree.Tests, "Primer.IntegrationTests"))
            .Where(path => File.ReadAllText(path).Contains("RepositoryRoot =", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(touchingRepositories);
        Assert.All(
            touchingRepositories,
            path => Assert.Contains(
                "TemporaryRepository",
                File.ReadAllText(path),
                StringComparison.Ordinal));
    }

    // Given any test, when it runs, then it writes nothing into the developer's own profile.
    [Fact]
    public void Given_the_tests_When_inspected_Then_none_writes_into_the_user_profile()
    {
        var offenders = SourceTree.CSharpFiles(SourceTree.Tests)
            // This file names the API in order to search for it, so it would otherwise
            // report itself.
            .Where(path => Path.GetFileName(path) != "TraceabilityTests.cs")
            .Where(path => File.ReadAllText(path).Contains("SpecialFolder.UserProfile", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .ToList();

        Assert.Empty(offenders);
    }

    // Given a pull request, when continuous integration runs, then the build, the full test
    // suite, the format check, and the traceability check all execute.
    [Fact]
    public void Given_the_ci_workflow_When_read_Then_it_runs_every_gate()
    {
        var workflow = Path.Combine(SourceTree.Root, ".github", "workflows", "ci.yml");

        Assert.True(File.Exists(workflow), "The continuous-integration workflow is missing.");

        var text = File.ReadAllText(workflow);

        Assert.Contains("dotnet build", text, StringComparison.Ordinal);
        Assert.Contains("dotnet test", text, StringComparison.Ordinal);
        Assert.Contains("dotnet format", text, StringComparison.Ordinal);
        Assert.Contains("dotnet pack", text, StringComparison.Ordinal);
        Assert.Contains("pull_request", text, StringComparison.Ordinal);
    }
}
