using System.Buffers;
using System.Text;
using System.Text.Json;

namespace Mafesoft.Maui.Controls
{
    /// <summary>A message received from the page, already parsed.</summary>
    /// <param name="Type"><c>message</c>, <c>invoke</c>, <c>callResult</c> or <c>callError</c>.</param>
    /// <param name="PageId">Random id of the page load (one per frame) that sent the message.</param>
    /// <param name="Id">Correlation id of an <c>invoke</c> or of the reply to a C# function call.</param>
    /// <param name="Method">Handler name of an <c>invoke</c>.</param>
    /// <param name="Payload"><c>data</c>, <c>args</c>, <c>result</c> or <c>error</c>, depending on the type.</param>
    internal sealed record PageEnvelope(string Type, string PageId, string? Id, string? Method, JsonElement Payload);

    /// <summary>
    /// Wire format between the page and C#. Platform-independent and reflection-free.
    /// <list type="bullet">
    /// <item>Page to C#: <c>{ type: "message", pageId, data }</c>, <c>{ type: "invoke", pageId, id, method, args }</c>,
    /// <c>{ type: "callResult", pageId, id, result }</c>, <c>{ type: "callError", pageId, id, error: { message, stack } }</c>.</item>
    /// <item>C# to page: <c>{ type: "message", data }</c>, <c>{ type: "result", pageId, id, result }</c>,
    /// <c>{ type: "error", pageId, id, error: { code, message, details? } }</c>, or a call to <c>__call(id, name, args)</c>.</item>
    /// </list>
    /// </summary>
    internal static class BridgeProtocol
    {
        public const string Message = "message";
        public const string Invoke = "invoke";
        public const string CallResult = "callResult";
        public const string CallError = "callError";

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
                if (type is null || string.IsNullOrEmpty(pageId))
                    return null;

                var id = GetString(root, "id");

                switch (type)
                {
                    case Message:
                        return new PageEnvelope(type, pageId, null, null, GetPayload(root, "data"));

                    case Invoke:
                        var method = GetString(root, "method");
                        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(method))
                            return null;
                        return new PageEnvelope(type, pageId, id, method, GetPayload(root, "args"));

                    case CallResult:
                        return string.IsNullOrEmpty(id) ? null : new PageEnvelope(type, pageId, id, null, GetPayload(root, "result"));

                    case CallError:
                        return string.IsNullOrEmpty(id) ? null : new PageEnvelope(type, pageId, id, null, GetPayload(root, "error"));

                    default:
                        return null;
                }
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>Envelope that delivers a message (raw JSON value) to the page's listeners.</summary>
        public static string MessageEnvelope(string dataJson) =>
            Write(w =>
            {
                w.WriteStartObject();
                w.WriteString("type", Message);
                w.WritePropertyName("data");
                w.WriteRawValue(dataJson);
                w.WriteEndObject();
            });

        /// <summary>Envelope that resolves a pending <c>invoke</c> Promise. <paramref name="resultJson"/> null means <c>null</c>.</summary>
        public static string ResultEnvelope(string pageId, string id, string? resultJson) =>
            Write(w =>
            {
                w.WriteStartObject();
                w.WriteString("type", "result");
                w.WriteString("pageId", pageId);
                w.WriteString("id", id);
                w.WritePropertyName("result");
                if (resultJson is null)
                    w.WriteNullValue();
                else
                    w.WriteRawValue(resultJson);
                w.WriteEndObject();
            });

        /// <summary>Envelope that rejects a pending <c>invoke</c> Promise.</summary>
        public static string ErrorEnvelope(string pageId, string id, string code, string message, string? details = null) =>
            Write(w =>
            {
                w.WriteStartObject();
                w.WriteString("type", "error");
                w.WriteString("pageId", pageId);
                w.WriteString("id", id);
                w.WriteStartObject("error");
                w.WriteString("code", code);
                w.WriteString("message", message);
                if (details is not null)
                    w.WriteString("details", details);
                w.WriteEndObject();
                w.WriteEndObject();
            });

        /// <summary>Script that hands an envelope to the bridge in the page.</summary>
        // The default encoder escapes <, >, &, quotes and non-ASCII characters,
        // so the JSON is safe to embed as a JavaScript expression.
        public static string ReceiveScript(string envelopeJson) =>
            "window.AdvancedWebView&&window.AdvancedWebView.__receive(" + envelopeJson + ");";

        /// <summary>Script that calls a page function through the bridge; the answer comes back as callResult/callError.</summary>
        public static string CallScript(string id, string functionName, string argumentsJson) =>
            "window.AdvancedWebView&&window.AdvancedWebView.__call(" + JsonString(id) + "," + JsonString(functionName) + "," + argumentsJson + ");";

        /// <summary>Serializes a string as a JSON string value.</summary>
        public static string JsonString(string value) => Write(w => w.WriteStringValue(value));

        /// <summary>Validates that <paramref name="json"/> is a JSON array and returns it re-serialized (escaped, compact).</summary>
        public static string NormalizeArray(string json)
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                throw new ArgumentException("The arguments must be a JSON array, for example [1, \"two\"].", nameof(json));
            return Write(w => doc.RootElement.WriteTo(w));
        }

        static string Write(Action<Utf8JsonWriter> write)
        {
            var buffer = new ArrayBufferWriter<byte>();
            using (var writer = new Utf8JsonWriter(buffer))
                write(writer);
            return Encoding.UTF8.GetString(buffer.WrittenSpan);
        }

        static string? GetString(JsonElement obj, string name) =>
            obj.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

        static JsonElement GetPayload(JsonElement obj, string name) =>
            obj.TryGetProperty(name, out var p) ? p.Clone() : default;
    }
}
