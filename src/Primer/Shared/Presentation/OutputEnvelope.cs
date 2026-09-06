using System.Text.Json.Serialization;

namespace Primer.Shared.Presentation;

/// <summary>
/// The outer object every machine-readable result is wrapped in. The schema version lets a
/// consumer detect a shape change rather than discovering one by failing.
/// </summary>
internal sealed record OutputEnvelope
{
    /// <summary>The version of the envelope shape this build emits.</summary>
    internal const string CurrentSchemaVersion = "1.0";

    [JsonPropertyOrder(0)]
    public string SchemaVersion { get; init; } = CurrentSchemaVersion;

    [JsonPropertyOrder(1)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Payload { get; init; }

    [JsonPropertyOrder(2)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; init; }

    [JsonPropertyOrder(3)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ExitCode { get; init; }
}
