using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Primer.Shared.Presentation;

/// <summary>
/// Renders a payload as one JSON document. Output carries no escape sequence and no
/// incidental ordering, so two runs over unchanged inputs produce identical bytes.
/// </summary>
internal sealed class JsonOutputFormatter : IOutputFormatter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,

        // A CLI writes to a pipe, not to HTML. Relaxed escaping keeps a value such as
        // "0.1.0+6864ca1" readable instead of rendering '+' as an escape.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false,
    };

    public string Render(object payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        return payload is OutputEnvelope envelope
            ? JsonSerializer.Serialize(envelope, Options)
            : JsonSerializer.Serialize(payload, payload.GetType(), Options);
    }
}
