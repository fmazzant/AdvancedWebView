using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Mafesoft.Maui.Controls
{
    /// <summary>A message the page sent with <c>AdvancedWebView.postMessage(data)</c>.</summary>
    public sealed class WebMessageReceivedEventArgs : EventArgs
    {
        readonly JsonElement data;

        internal WebMessageReceivedEventArgs(JsonElement data, Uri? origin, bool isMainFrame)
        {
            this.data = data;
            Origin = origin;
            IsMainFrame = isMainFrame;
        }

        /// <summary>
        /// The message as text: the string itself when the page posted a string,
        /// otherwise the JSON of the posted value (for example <c>{"type":"checkout"}</c>).
        /// </summary>
        public string Message => data.ValueKind switch
        {
            JsonValueKind.String => data.GetString() ?? string.Empty,
            JsonValueKind.Undefined => "null",
            _ => data.GetRawText(),
        };

        /// <summary>The JSON of the posted value, also when it is a string (<c>"text"</c> with quotes).</summary>
        public string Json => data.ValueKind == JsonValueKind.Undefined ? "null" : data.GetRawText();

        /// <summary>
        /// Origin of the page that sent the message (for example <c>https://example.com</c>),
        /// or <see langword="null"/> for local content without an origin.
        /// </summary>
        public Uri? Origin { get; }

        /// <summary>Whether the message came from the top-level page rather than an iframe.</summary>
        public bool IsMainFrame { get; }

        /// <summary>Deserializes the posted value. Property names are matched case-insensitively.</summary>
        [RequiresUnreferencedCode(WebViewBridge.ReflectionWarning)]
        [RequiresDynamicCode(WebViewBridge.ReflectionWarning)]
        public T? GetJson<T>() =>
            data.ValueKind == JsonValueKind.Undefined ? default : data.Deserialize<T>(WebViewBridge.JsonOptions);

        /// <summary>Deserializes the posted value with source-generated metadata (trimming and AOT safe).</summary>
        public T? GetJson<T>(JsonTypeInfo<T> typeInfo) =>
            data.ValueKind == JsonValueKind.Undefined ? default : data.Deserialize(typeInfo);
    }
}
