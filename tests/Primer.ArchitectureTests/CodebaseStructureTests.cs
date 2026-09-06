// Acceptance Test
// Traces to: L2-038, L2-039, L2-040, L2-041
// Description: Verify the structural rules mechanically rather than by review: production
//              code under src and tests under tests, one command per file, no cross-feature
//              reference, and no handler reaching the console or file system directly.

using System.Text.RegularExpressions;

namespace Primer.ArchitectureTests;

public sealed partial class CodebaseStructureTests
{
    [GeneratedRegex(@"^\s*(internal|public)\s+(sealed\s+)?class\s+(?<name>\w+)", RegexOptions.Multiline)]
    private static partial Regex ClassDeclaration { get; }

    // Given the repository, when the solution is inspected, then every production project
    // resides under src/ and every test project resides under tests/.
    [Fact]
    public void Given_the_solution_When_inspected_Then_production_is_under_src_and_tests_under_tests()
    {
        var production = SourceTree.ProjectFiles(SourceTree.Source);
        var tests = SourceTree.ProjectFiles(SourceTree.Tests);

        Assert.NotEmpty(production);
        Assert.NotEmpty(tests);

        Assert.All(production, path => Assert.DoesNotContain(".Tests", Path.GetFileName(path), StringComparison.Ordinal));
        Assert.All(tests, path => Assert.Contains("Tests", Path.GetFileName(path), StringComparison.Ordinal));
    }

    // Given the repository, when the solution is read, then it references no project
    // outside src/ or tests/.
    [Fact]
    public void Given_the_solution_When_read_Then_it_references_nothing_outside_src_or_tests()
    {
        var solution = File.ReadAllText(Path.Combine(SourceTree.Root, "Primer.slnx"));

        var referenced = Regex
            .Matches(solution, @"Path=""(?<path>[^""]+)""")
            .Select(match => match.Groups["path"].Value.Replace('\\', '/'))
            .ToList();

        Assert.NotEmpty(referenced);
        Assert.All(
            referenced,
            path => Assert.True(
                path.StartsWith("src/", StringComparison.Ordinal) || path.StartsWith("tests/", StringComparison.Ordinal),
                $"The solution references a project outside src/ and tests/: {path}"));
    }

    // Given the integration test project, when it is inspected,
    // then it is named Primer.IntegrationTests and resides at tests/Primer.IntegrationTests.
    [Fact]
    public void Given_the_integration_tests_When_located_Then_they_sit_where_the_specification_says()
    {
        Assert.True(File.Exists(Path.Combine(
            SourceTree.Tests, "Primer.IntegrationTests", "Primer.IntegrationTests.csproj")));
    }

    // Given each command file, when it is inspected,
    // then it declares exactly one command type named after the command.
    [Fact]
    public void Given_a_command_file_When_inspected_Then_it_declares_exactly_one_command_type()
    {
        var commandFiles = SourceTree.CSharpFiles(SourceTree.Features)
            .Where(path => Path.GetFileName(path).EndsWith("Command.cs", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(commandFiles);

        foreach (var file in commandFiles)
        {
            var declared = ClassDeclaration
                .Matches(File.ReadAllText(file))
                .Select(match => match.Groups["name"].Value)
                .Where(name => name.EndsWith("Command", StringComparison.Ordinal))
                .ToList();

            var single = Assert.Single(declared);
            Assert.Equal(Path.GetFileNameWithoutExtension(file), single);
        }
    }

    // Given a new command added to the tool, when it is registered, then no shared
    // registration list is edited: discovery is by assembly scan.
    [Fact]
    public void Given_command_registration_When_inspected_Then_it_is_by_discovery_not_a_shared_table()
    {
        var composition = File.ReadAllText(
            Path.Combine(SourceTree.Source, "Primer", "Shared", "Hosting", "PrimerCli.cs"));

        Assert.Contains("ICommandModule", composition, StringComparison.Ordinal);
        Assert.Contains("Assembly", composition, StringComparison.Ordinal);

        // No file names the concrete commands together, which is what a shared table is.
        foreach (var file in SourceTree.CSharpFiles(Path.Combine(SourceTree.Source, "Primer", "Shared")))
        {
            var text = File.ReadAllText(file);
            var named = new[] { "InitCommand", "CheckCommand", "McpCommand" }
                .Count(name => text.Contains(name, StringComparison.Ordinal));

            Assert.True(named < 2, $"{Path.GetFileName(file)} names more than one command, which is a registration table.");
        }
    }

    // Given two feature folders, when their references are inspected, then neither
    // references a type declared inside the other.
    [Fact]
    public void Given_two_features_When_inspected_Then_neither_references_the_other()
    {
        var features = Directory.Exists(SourceTree.Features)
            ? Directory.GetDirectories(SourceTree.Features).Select(Path.GetFileName).OfType<string>().ToList()
            : [];

        Assert.NotEmpty(features);

        foreach (var feature in features)
        {
            var others = features.Where(other => other != feature).ToList();

            foreach (var file in SourceTree.CSharpFiles(Path.Combine(SourceTree.Features, feature)))
            {
                var text = File.ReadAllText(file);

                foreach (var other in others)
                {
                    Assert.DoesNotContain($"Primer.Features.{other}", text, StringComparison.Ordinal);
                }
            }
        }
    }

    // Given any command handler, when its source is inspected, then it contains no direct
    // call to System.Console or System.IO.File; both are reached through abstractions.
    [Fact]
    public void Given_a_feature_When_inspected_Then_it_reaches_the_console_and_files_only_through_abstractions()
    {
        var offenders = new List<string>();

        foreach (var file in SourceTree.CSharpFiles(SourceTree.Features))
        {
            var text = File.ReadAllText(file);

            if (text.Contains("System.Console", StringComparison.Ordinal)
                || Regex.IsMatch(text, @"(?<![\w.])Console\s*\.")
                || Regex.IsMatch(text, @"(?<![\w.])File\s*\.")
                || Regex.IsMatch(text, @"(?<![\w.])Directory\s*\."))
            {
                offenders.Add(Path.GetFileName(file));
            }
        }

        Assert.Empty(offenders);
    }

    // Given the solution, when it is built, then nullable reference types are enabled and
    // warnings are treated as errors for every project.
    [Fact]
    public void Given_the_build_settings_When_read_Then_nullable_is_enabled_and_warnings_are_errors()
    {
        var props = File.ReadAllText(Path.Combine(SourceTree.Root, "Directory.Build.props"));

        Assert.Contains("<Nullable>enable</Nullable>", props, StringComparison.Ordinal);
        Assert.Contains("<TreatWarningsAsErrors>true</TreatWarningsAsErrors>", props, StringComparison.Ordinal);
        Assert.Contains("<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>", props, StringComparison.Ordinal);
    }

    // Given a feature folder, when it is inspected, then its command and handler live
    // together, so deleting the folder removes the whole slice.
    [Fact]
    public void Given_a_feature_folder_When_inspected_Then_it_holds_its_own_command()
    {
        foreach (var feature in Directory.GetDirectories(SourceTree.Features))
        {
            var files = SourceTree.CSharpFiles(feature);

            Assert.NotEmpty(files);
            Assert.Contains(files, path => Path.GetFileName(path).EndsWith("Command.cs", StringComparison.Ordinal));
        }
    }
}
