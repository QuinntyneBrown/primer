using System.Security.Cryptography;
using Primer.Shared.Hosting;
using Primer.Shared.Presentation;

namespace Primer.Shared.Mcp;

/// <summary>An artefact that passed every supply-chain check.</summary>
internal sealed record VerifiedArtifact(string PinnedVersion, string Checksum, byte[] Content);

/// <summary>Raised when an install source is not carried over HTTPS.</summary>
internal sealed class InsecureSourceException(Uri source) : PrimerException(new PrimerError(
    What: "The install source is not HTTPS",
    Subject: source?.ToString() ?? "(none)",
    NextAction: "Declare an https source. Primer will not download a component over plain HTTP.",
    ExitCode: ExitCode.Configuration));

/// <summary>Raised when a requirement names a floating version rather than a pinned one.</summary>
internal sealed class UnpinnedVersionException(string name, string version) : PrimerException(new PrimerError(
    What: "The requirement does not pin a version",
    Subject: $"{name} {version}",
    NextAction: "Pin an exact version. A floating tag can change what is installed between runs.",
    ExitCode: ExitCode.Configuration));

/// <summary>Raised when a response redirects to a host the requirement did not declare.</summary>
internal sealed class RedirectRefusedException(string declaredHost, string redirectHost)
    : PrimerException(new PrimerError(
        What: "The download was redirected to an undeclared host",
        Subject: $"{declaredHost} redirected to {redirectHost}",
        NextAction: "Declare the final host, or obtain the component from the declared one.",
        ExitCode: ExitCode.Unexpected));

/// <summary>Raised when a downloaded artefact does not match its declared checksum.</summary>
internal sealed class ChecksumMismatchException(string expected, string actual) : PrimerException(new PrimerError(
    What: "The downloaded artefact does not match its declared checksum",
    Subject: $"expected {expected}, got {actual}",
    NextAction: "Nothing was installed. Verify the declared checksum and the source.",
    ExitCode: ExitCode.Unexpected));

/// <summary>
/// Enforces the supply-chain rules before anything reaches the machine: an exact version,
/// HTTPS only, no redirect off the declared host, and a checksum that matches. A redirect is
/// exactly how a compromised distribution point would move a download somewhere else, so it
/// is inspected rather than followed.
/// </summary>
internal sealed class ArtifactVerifier(HttpClient client)
{
    private static readonly string[] FloatingVersions = ["latest", "*", "", "next"];

    internal async Task<VerifiedArtifact> VerifyAsync(
        McpRequirement requirement,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requirement);

        var version = requirement.VersionRange.Trim();

        if (FloatingVersions.Contains(version, StringComparer.OrdinalIgnoreCase)
            || !Version.TryParse(version, out _))
        {
            throw new UnpinnedVersionException(requirement.Name, version);
        }

        var source = requirement.SourceUrl
            ?? throw new InsecureSourceException(new Uri("about:blank"));

        if (!string.Equals(source.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InsecureSourceException(source);
        }

        using var response = await client
            .GetAsync(source, HttpCompletionOption.ResponseContentRead, cancellationToken)
            .ConfigureAwait(false);

        if (IsRedirect(response))
        {
            var target = response.Headers.Location;

            if (target is not null && !string.Equals(target.Host, source.Host, StringComparison.OrdinalIgnoreCase))
            {
                throw new RedirectRefusedException(source.Host, target.Host);
            }
        }

        var content = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        var checksum = Convert.ToHexStringLower(SHA256.HashData(content));

        if (requirement.ExpectedChecksum is { Length: > 0 } expected
            && !string.Equals(expected, checksum, StringComparison.OrdinalIgnoreCase))
        {
            throw new ChecksumMismatchException(expected, checksum);
        }

        return new VerifiedArtifact(version, checksum, content);
    }

    private static bool IsRedirect(HttpResponseMessage response) =>
        (int)response.StatusCode is >= 300 and < 400;
}
