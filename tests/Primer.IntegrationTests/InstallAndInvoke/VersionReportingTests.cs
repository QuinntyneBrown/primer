// Acceptance Test
// Traces to: L2-002
// Description: Verify primer --version reports a SemVer 2.0.0 informational version
//              identifying the exact build, in text and JSON, deterministically.

using System.Text.Json;
using System.Text.RegularExpressions;
using Primer.Shared.Hosting;

namespace Primer.IntegrationTests.InstallAndInvoke;

public sealed partial class VersionReportingTests
{
    [GeneratedRegex(@"^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?(\+[0-9A-Za-z.-]+)?$")]
    private static partial Regex SemanticVersion { get; }

    // Given an installed tool, when `primer --version` is run,
    // then stdout contains a SemVer 2.0.0 version and the process exits 0.
    [Fact]
    public async Task Version_is_reported_as_semver_on_stdout_with_success_exit()
    {
        var result = await PrimerCliHarness.RunAsync("--version");

        Assert.Equal((int)ExitCode.Success, result.ExitCode);
        Assert.Matches(SemanticVersion, result.StandardOutput.Trim());
        Assert.Empty(result.StandardError);
    }

    // Given an installed tool, when `primer --version --format json` is run,
    // then stdout is a single JSON object containing version and commit properties.
    [Fact]
    public async Task Version_in_json_format_is_one_object_carrying_version_and_commit()
    {
        var result = await PrimerCliHarness.RunAsync("--version", "--format", "json");

        Assert.Equal((int)ExitCode.Success, result.ExitCode);

        using var document = JsonDocument.Parse(result.StandardOutput);
        Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
        Assert.True(document.RootElement.TryGetProperty("version", out var version));
        Assert.True(document.RootElement.TryGetProperty("commit", out var commit));
        Assert.Matches(SemanticVersion, version.GetString()!);
        Assert.False(string.IsNullOrWhiteSpace(commit.GetString()));
    }

    // Given the same installed build, when `primer --version` is run twice,
    // then the two outputs are byte-identical.
    [Fact]
    public async Task Version_output_is_identical_across_runs()
    {
        var first = await PrimerCliHarness.RunAsync("--version");
        var second = await PrimerCliHarness.RunAsync("--version");

        Assert.Equal(first.StandardOutput, second.StandardOutput);
        Assert.Equal(first.ExitCode, second.ExitCode);
    }
}
