// Acceptance Test
// Traces to: L2-001
// Description: Verify the Primer project packs as an installable .NET tool named primer,
//              installs globally and as a local tool, and carries complete package metadata.

using System.IO.Compression;
using System.Xml.Linq;
using Primer.IntegrationTests.TestSupport;

namespace Primer.IntegrationTests.InstallAndInvoke;

/// <summary>
/// Packs the tool once for the whole class. Packing is expensive, and every criterion
/// in L2-001 is asserted against the same produced package.
/// </summary>
public sealed class ToolPackageFixture : IAsyncLifetime
{
    internal string WorkspaceDirectory { get; private set; } = string.Empty;
    internal string ArtifactsDirectory => Path.Combine(WorkspaceDirectory, "artifacts");
    internal string DotnetHomeDirectory => Path.Combine(WorkspaceDirectory, "home");
    internal string NupkgPath { get; private set; } = string.Empty;
    internal string PackOutput { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        WorkspaceDirectory = Path.Combine(Path.GetTempPath(), "primer-pack-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(ArtifactsDirectory);
        Directory.CreateDirectory(DotnetHomeDirectory);

        var pack = await ProcessRunner.RunAsync(
            "dotnet",
            ["pack", RepositoryPaths.ToolProject, "-c", "Release", "-o", ArtifactsDirectory],
            RepositoryPaths.Root,
            environment: null,
            TestContext.Current.CancellationToken);

        PackOutput = pack.Combined;

        if (pack.ExitCode == 0)
        {
            NupkgPath = Directory.GetFiles(ArtifactsDirectory, "*.nupkg").FirstOrDefault() ?? string.Empty;
        }
    }

    public ValueTask DisposeAsync()
    {
        try
        {
            if (Directory.Exists(WorkspaceDirectory))
            {
                Directory.Delete(WorkspaceDirectory, recursive: true);
            }
        }
        catch (IOException)
        {
            // A locked file in a temporary directory is not a test failure.
        }

        return ValueTask.CompletedTask;
    }
}

[Trait("tier", "slow")]
public sealed class ToolPackagingTests(ToolPackageFixture fixture) : IClassFixture<ToolPackageFixture>
{
    // Given the solution at a clean checkout, when dotnet pack -c Release is run, then a .nupkg
    // is produced whose nuspec declares a package type of DotnetTool and a tool command named primer.
    [Fact]
    public void Pack_produces_a_dotnet_tool_package_with_the_primer_command()
    {
        Assert.True(File.Exists(fixture.NupkgPath), $"No .nupkg was produced. Pack output:\n{fixture.PackOutput}");

        var nuspec = ReadNuspec(fixture.NupkgPath);
        var nuspecNamespace = nuspec.Root!.Name.Namespace;
        var packageTypes = nuspec.Descendants(nuspecNamespace + "packageType")
            .Select(element => element.Attribute("name")?.Value)
            .ToList();

        Assert.Contains("DotnetTool", packageTypes);

        using var archive = ZipFile.OpenRead(fixture.NupkgPath);
        var settings = archive.Entries.SingleOrDefault(entry => entry.Name == "DotnetToolSettings.xml");
        Assert.NotNull(settings);

        using var reader = new StreamReader(settings.Open());
        var toolSettings = XDocument.Parse(reader.ReadToEnd());
        var commandName = toolSettings.Descendants("Command").Single().Attribute("Name")?.Value;
        Assert.Equal("primer", commandName);
    }

    // Given the package metadata, when it is inspected, then package id, description,
    // repository URL, licence expression, and target framework are all populated and non-empty.
    [Fact]
    public void Package_metadata_is_complete()
    {
        Assert.True(File.Exists(fixture.NupkgPath), $"No .nupkg was produced. Pack output:\n{fixture.PackOutput}");

        var nuspec = ReadNuspec(fixture.NupkgPath);
        var nuspecNamespace = nuspec.Root!.Name.Namespace;
        var metadata = nuspec.Descendants(nuspecNamespace + "metadata").Single();

        Assert.Equal("Primer", Value(metadata, nuspecNamespace, "id"));
        Assert.False(string.IsNullOrWhiteSpace(Value(metadata, nuspecNamespace, "description")));
        Assert.False(string.IsNullOrWhiteSpace(Value(metadata, nuspecNamespace, "repository")));
        Assert.False(string.IsNullOrWhiteSpace(Value(metadata, nuspecNamespace, "license")));

        // A tool package declares no dependency groups, so the target framework is
        // evidenced by the tools/<tfm>/ layout the package is laid out with.
        using var archive = ZipFile.OpenRead(fixture.NupkgPath);
        Assert.Contains(
            archive.Entries,
            entry => entry.FullName.StartsWith("tools/net10.0/", StringComparison.OrdinalIgnoreCase));
    }

    // Given the produced package, when it is installed with dotnet tool install --global
    // --add-source <dir> Primer, then invoking primer --version from an unrelated working
    // directory exits 0 and writes a version string to stdout.
    [Fact]
    public async Task Global_install_makes_primer_runnable_from_an_unrelated_directory()
    {
        Assert.True(File.Exists(fixture.NupkgPath), $"No .nupkg was produced. Pack output:\n{fixture.PackOutput}");

        // DOTNET_CLI_HOME redirects the global tools directory, so --global is exercised
        // literally without writing into the developer's real profile (L2-044).
        var environment = new Dictionary<string, string>
        {
            ["DOTNET_CLI_HOME"] = fixture.DotnetHomeDirectory,
            ["DOTNET_NOLOGO"] = "1",
        };

        var install = await ProcessRunner.RunAsync(
            "dotnet",
            ["tool", "install", "--global", "--add-source", fixture.ArtifactsDirectory, "Primer"],
            fixture.WorkspaceDirectory,
            environment,
            TestContext.Current.CancellationToken);

        Assert.True(install.ExitCode == 0, $"Install failed:\n{install.Combined}");

        var toolPath = Path.Combine(
            fixture.DotnetHomeDirectory,
            ".dotnet",
            "tools",
            OperatingSystem.IsWindows() ? "primer.exe" : "primer");

        Assert.True(File.Exists(toolPath), $"Installed tool not found at {toolPath}.\n{install.Combined}");

        var run = await ProcessRunner.RunAsync(
            toolPath,
            ["--version"],
            Path.GetTempPath(),
            environment,
            TestContext.Current.CancellationToken);

        Assert.Equal(0, run.ExitCode);
        Assert.False(string.IsNullOrWhiteSpace(run.StandardOutput));
    }

    // Given the produced package, when it is installed as a local tool against a tool
    // manifest, then dotnet primer --version exits 0.
    [Fact]
    public async Task Local_tool_install_makes_dotnet_primer_runnable()
    {
        Assert.True(File.Exists(fixture.NupkgPath), $"No .nupkg was produced. Pack output:\n{fixture.PackOutput}");

        var localRoot = Path.Combine(fixture.WorkspaceDirectory, "local");
        Directory.CreateDirectory(localRoot);

        var environment = new Dictionary<string, string>
        {
            ["DOTNET_CLI_HOME"] = fixture.DotnetHomeDirectory,
            ["DOTNET_NOLOGO"] = "1",
        };

        var manifest = await ProcessRunner.RunAsync(
            "dotnet", ["new", "tool-manifest"], localRoot, environment, TestContext.Current.CancellationToken);
        Assert.True(manifest.ExitCode == 0, $"Manifest creation failed:\n{manifest.Combined}");

        var install = await ProcessRunner.RunAsync(
            "dotnet",
            ["tool", "install", "--add-source", fixture.ArtifactsDirectory, "Primer"],
            localRoot,
            environment,
            TestContext.Current.CancellationToken);
        Assert.True(install.ExitCode == 0, $"Local install failed:\n{install.Combined}");

        var run = await ProcessRunner.RunAsync(
            "dotnet", ["primer", "--version"], localRoot, environment, TestContext.Current.CancellationToken);

        Assert.Equal(0, run.ExitCode);
        Assert.False(string.IsNullOrWhiteSpace(run.StandardOutput));
    }

    private static XDocument ReadNuspec(string nupkgPath)
    {
        using var archive = ZipFile.OpenRead(nupkgPath);
        var entry = archive.Entries.Single(e => e.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));
        using var reader = new StreamReader(entry.Open());
        return XDocument.Parse(reader.ReadToEnd());
    }

    private static string Value(XElement metadata, XNamespace nuspecNamespace, string elementName)
    {
        var element = metadata.Element(nuspecNamespace + elementName);
        return element is null ? string.Empty : (element.Attribute("url")?.Value ?? element.Value);
    }
}
