using System.Buffers;
using System.Text;
using System.Text.Json;

namespace Mafesoft.Maui.Controls
{
    /// <summary>A message received from the page, already parsed.</summary>
    internal sealed record PageEnvelope(string Type, string PageId, string? Id, string? Method, JsonElement Payload);

    /// <summary>
    /// Wire format between the page and C#. Platform-independent and reflection-free.
    /// <para>Page to C#: <c>{ type: "message", pageId, data }</c> or <c>{ type: "invoke", pageId, id, method, args }</c>.</para>
    /// <para>C# to page: <c>{ type: "message", data }</c>, <c>{ type: "result", pageId, id, result }</c> or
    /// <c>{ type: "error", pageId, id, error: { code, message, details? } }</c>.</para>
    /// </summary>
    internal static class BridgeProtocol
    {
        /// <summary>Parses a message from the page. Returns <see langword="null"/> when it is not a valid envelope.</summary>
        public static PageEnvelope? Parse(string? text)
        {
            if (string.IsNullOrEmpty(text))
                return null;

            try
            {
                using var doc = JsonDocument.Parse(text);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                    return null;

                var type = GetString(root, "type");
                var pageId = GetString(root, "pageId");
                if (type is null || pageId is null)
                    return null;

                switch (type)
                {
                    case "message":
                        return new PageEnvelope(type, pageId, null, null, GetPayload(root, "data"));

                    case "invoke":
                        var id = GetString(root, "id");
                        var method = GetString(root, "method");
                        if (id is null || string.IsNullOrEmpty(method))
                            return null;
                        return new PageEnvelope(type, pageId, id, method, GetPayload(root, "args"));

                    default:
                        return null;
                }
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>Script that delivers a message (raw JSON value) to the page's listeners.</summary>
        public static string MessageScript(string dataJson) =>
            Script(w =>
            {
                w.WriteString("type", "message");
                w.WritePropertyName("data");
                w.WriteRawValue(dataJson);
            });

        /// <summary>Script that resolves a pending <c>invoke</c> Promise. <paramref name="resultJson"/> null means <c>null</c>.</summary>
        public static string ResultScript(string pageId, string id, string? resultJson) =>
            Script(w =>
            {
                w.WriteString("type", "result");
                w.WriteString("pageId", pageId);
                w.WriteString("id", id);
                w.WritePropertyName("result");
                if (resultJson is null)
                    w.WriteNullValue();
                else
                    w.WriteRawValue(resultJson);
            });

        /// <summary>Script that rejects a pending <c>invoke</c> Promise.</summary>
        public static string ErrorScript(string pageId, string id, string code, string message, string? details = null) =>
            Script(w =>
            {
                w.WriteString("type", "error");
                w.WriteString("pageId", pageId);
                w.WriteString("id", id);
                w.WriteStartObject("error");
                w.WriteString("code", code);
                w.WriteString("message", message);
                if (details is not null)
                    w.WriteString("details", details);
                w.WriteEndObject();
            });

        /// <summary>Serializes a string as a JSON string value.</summary>
        public static string JsonString(string value) => JsonSerializer.Serialize(value, BridgeJsonContext.Default.String);

        static string Script(Action<Utf8JsonWriter> writeBody)
        {
            var buffer = new ArrayBufferWriter<byte>();
            using (var writer = new Utf8JsonWriter(buffer))
            {
                writer.WriteStartObject();
                writeBody(writer);
                writer.WriteEndObject();
            }

            // The default encoder escapes <, >, &, quotes and non-ASCII characters,
            // so the JSON is safe to embed as a JavaScript expression.
            return "window.AdvancedWebView&&window.AdvancedWebView.__receive(" + Encoding.UTF8.GetString(buffer.WrittenSpan) + ");";
        }

        static string? GetString(JsonElement obj, string name) =>
            obj.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

        static JsonElement GetPayload(JsonElement obj, string name) =>
            obj.TryGetProperty(name, out var p) ? p.Clone() : default;
    }

    [System.Text.Json.Serialization.JsonSerializable(typeof(string))]
    internal sealed partial class BridgeJsonContext : System.Text.Json.Serialization.JsonSerializerContext
    {
    }
}
