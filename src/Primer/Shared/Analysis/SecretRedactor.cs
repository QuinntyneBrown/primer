using System.Text.RegularExpressions;

namespace Primer.Shared.Analysis;

/// <summary>Removes credential-shaped values from anything the tool retains.</summary>
internal interface ISecretRedactor
{
    string Redact(string value);
}

/// <summary>
/// Excludes values matching known credential shapes. A secret that reaches generated
/// guidance is a secret published to every agent that reads the repository.
/// </summary>
internal sealed partial class PatternSecretRedactor : ISecretRedactor
{
    /// <summary>What replaces a matched value.</summary>
    internal const string Marker = "[redacted]";

    [GeneratedRegex(@"gh[pousr]_[A-Za-z0-9]{16,}", RegexOptions.CultureInvariant)]
    private static partial Regex GitHubToken { get; }

    [GeneratedRegex(@"AKIA[0-9A-Z]{16}", RegexOptions.CultureInvariant)]
    private static partial Regex AwsAccessKey { get; }

    [GeneratedRegex(
        @"(?i)\b(api[_-]?key|secret|password|passwd|token)\b\s*[:=]\s*\S+",
        RegexOptions.CultureInvariant)]
    private static partial Regex AssignedCredential { get; }

    [GeneratedRegex(@"-----BEGIN [A-Z ]*PRIVATE KEY-----", RegexOptions.CultureInvariant)]
    private static partial Regex PrivateKeyHeader { get; }

    public string Redact(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var redacted = GitHubToken.Replace(value, Marker);
        redacted = AwsAccessKey.Replace(redacted, Marker);
        redacted = PrivateKeyHeader.Replace(redacted, Marker);
        redacted = AssignedCredential.Replace(redacted, Marker);

        return redacted;
    }
}
