using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Mafesoft.Maui.Controls.Handlers;
using Microsoft.Extensions.Logging;

namespace Mafesoft.Maui.Controls
{
    // JavaScript bridge: messages in both directions and C# handlers the page can call.
    public partial class AdvancedWebView
    {
        WebViewBridge? bridgeCore;

        /// <summary>Creates the control.</summary>
        public AdvancedWebView()
        {
            Navigated += (_, _) => (Handler as AdvancedWebViewHandler)?.OnPageLoaded();
            UserScripts.CollectionChanged += (_, _) => (Handler as AdvancedWebViewHandler)?.UpdateUserScripts();
            UserStyleSheets.CollectionChanged += (_, _) => (Handler as AdvancedWebViewHandler)?.UpdateUserScripts();
        }

        /// <summary>Settings of the JavaScript bridge: allowed origins, iframes, timeout.</summary>
        public BridgeOptions Bridge { get; } = new();

        internal WebViewBridge BridgeCore => bridgeCore ??= new WebViewBridge(Bridge, ExecuteBridgeScriptAsync, OnMessageReceived, LogWarning);

        internal void LogWarning(string message) =>
            Handler?.MauiContext?.Services.GetService<ILogger<AdvancedWebView>>()?.LogWarning("{Message}", message);

        /// <summary>
        /// Raised on the main thread when the page calls <c>AdvancedWebView.postMessage(data)</c>.
        /// </summary>
        public event EventHandler<WebMessageReceivedEventArgs>? MessageReceived;

        #region Messages C# -> page

        /// <summary>
        /// Sends a string to the page. Listeners added with
        /// <c>AdvancedWebView.addEventListener("message", e =&gt; ...)</c> receive it as <c>e.data</c>.
        /// </summary>
        /// <exception cref="InvalidOperationException">The control is not attached to its handler.</exception>
        public Task PostMessageAsync(string message)
        {
            ArgumentNullException.ThrowIfNull(message);
            return ExecuteBridgeScriptAsync(BridgeProtocol.ReceiveScript(BridgeProtocol.MessageEnvelope(BridgeProtocol.JsonString(message))));
        }

        /// <summary>
        /// Sends a value to the page, serialized as JSON (camelCase). The page receives it as an object in <c>e.data</c>.
        /// </summary>
        /// <exception cref="InvalidOperationException">The control is not attached to its handler.</exception>
        [RequiresUnreferencedCode(WebViewBridge.ReflectionWarning)]
        [RequiresDynamicCode(WebViewBridge.ReflectionWarning)]
        public Task PostMessageAsync<T>(T value) =>
            ExecuteBridgeScriptAsync(BridgeProtocol.ReceiveScript(BridgeProtocol.MessageEnvelope(JsonSerializer.Serialize(value, WebViewBridge.JsonOptions))));

        /// <summary>Sends a value to the page, serialized with source-generated metadata (trimming and AOT safe).</summary>
        /// <exception cref="InvalidOperationException">The control is not attached to its handler.</exception>
        public Task PostMessageAsync<T>(T value, JsonTypeInfo<T> typeInfo)
        {
            ArgumentNullException.ThrowIfNull(typeInfo);
            return ExecuteBridgeScriptAsync(BridgeProtocol.ReceiveScript(BridgeProtocol.MessageEnvelope(JsonSerializer.Serialize(value, typeInfo))));
        }

        #endregion

        #region Handlers page -> C#

        /// <summary>
        /// Registers a handler the page calls with <c>await AdvancedWebView.invoke(method, args)</c>.
        /// The arguments are deserialized into <typeparamref name="TRequest"/> and the result is returned to the page as JSON.
        /// Throw <see cref="BridgeException"/> to reject the Promise with a specific <c>error.code</c>.
        /// Registering the same name again replaces the previous handler.
        /// </summary>
        [RequiresUnreferencedCode(WebViewBridge.ReflectionWarning)]
        [RequiresDynamicCode(WebViewBridge.ReflectionWarning)]
        public void RegisterHandler<TRequest, TResponse>(string method, Func<TRequest?, CancellationToken, Task<TResponse>> handler)
        {
            ArgumentNullException.ThrowIfNull(handler);
            BridgeCore.SetHandler(method, async (args, ct) =>
            {
                var request = ReadArguments(method, args, a => a.Deserialize<TRequest>(WebViewBridge.JsonOptions));
                var response = await handler(request, ct);
                return JsonSerializer.Serialize(response, WebViewBridge.JsonOptions);
            });
        }

        /// <summary>
        /// Registers a handler with arguments and result, using source-generated metadata (trimming and AOT safe).
        /// </summary>
        public void RegisterHandler<TRequest, TResponse>(
            string method,
            JsonTypeInfo<TRequest> requestTypeInfo,
            JsonTypeInfo<TResponse> responseTypeInfo,
            Func<TRequest?, CancellationToken, Task<TResponse>> handler)
        {
            ArgumentNullException.ThrowIfNull(requestTypeInfo);
            ArgumentNullException.ThrowIfNull(responseTypeInfo);
            ArgumentNullException.ThrowIfNull(handler);
            BridgeCore.SetHandler(method, async (args, ct) =>
            {
                var request = ReadArguments(method, args, a => a.Deserialize(requestTypeInfo));
                var response = await handler(request, ct);
                return JsonSerializer.Serialize(response, responseTypeInfo);
            });
        }

        /// <summary>Registers a handler without arguments that returns a result.</summary>
        [RequiresUnreferencedCode(WebViewBridge.ReflectionWarning)]
        [RequiresDynamicCode(WebViewBridge.ReflectionWarning)]
        public void RegisterHandler<TResponse>(string method, Func<CancellationToken, Task<TResponse>> handler)
        {
            ArgumentNullException.ThrowIfNull(handler);
            BridgeCore.SetHandler(method, async (_, ct) =>
                JsonSerializer.Serialize(await handler(ct), WebViewBridge.JsonOptions));
        }

        /// <summary>
        /// Registers a handler without arguments that returns a result, using source-generated metadata (trimming and AOT safe).
        /// </summary>
        public void RegisterHandler<TResponse>(string method, JsonTypeInfo<TResponse> responseTypeInfo, Func<CancellationToken, Task<TResponse>> handler)
        {
            ArgumentNullException.ThrowIfNull(responseTypeInfo);
            ArgumentNullException.ThrowIfNull(handler);
            BridgeCore.SetHandler(method, async (_, ct) =>
                JsonSerializer.Serialize(await handler(ct), responseTypeInfo));
        }

        /// <summary>Registers a handler without arguments and without result. The page's Promise resolves to <c>null</c>.</summary>
        public void RegisterHandler(string method, Func<CancellationToken, Task> handler)
        {
            ArgumentNullException.ThrowIfNull(handler);
            BridgeCore.SetHandler(method, async (_, ct) =>
            {
                await handler(ct);
                return null;
            });
        }

        /// <summary>
        /// Registers a handler that works on raw JSON: it receives the arguments as JSON text
        /// (<see langword="null"/> when the page passed none) and returns JSON text, or <see langword="null"/> for <c>null</c>.
        /// </summary>
        public void RegisterRawHandler(string method, Func<string?, CancellationToken, Task<string?>> handler)
        {
            ArgumentNullException.ThrowIfNull(handler);
            BridgeCore.SetHandler(method, (args, ct) =>
                handler(args.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null ? null : args.GetRawText(), ct));
        }

        /// <summary>Removes a handler. Returns <see langword="false"/> when none was registered with that name.</summary>
        public bool UnregisterHandler(string method) => BridgeCore.RemoveHandler(method);

        static T? ReadArguments<T>(string method, JsonElement args, Func<JsonElement, T?> read)
        {
            if (args.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
                return default;

            try
            {
                return read(args);
            }
            catch (JsonException ex)
            {
                throw new BridgeException(BridgeErrorCodes.InvalidArguments, $"Invalid arguments for '{method}': {ex.Message}", ex);
            }
        }

        #endregion

        void OnMessageReceived(WebMessageReceivedEventArgs e) => MessageReceived?.Invoke(this, e);

        Task ExecuteBridgeScriptAsync(string script)
        {
            if (Handler is not AdvancedWebViewHandler handler)
                throw new InvalidOperationException(
                    "AdvancedWebView is not attached to its handler. Call builder.UseAdvancedWebView() in MauiProgram and wait until the control is loaded.");

            var dispatcher = Dispatcher;
            if (dispatcher is null || !dispatcher.IsDispatchRequired)
            {
                handler.ExecuteScript(script);
                return Task.CompletedTask;
            }

            return dispatcher.DispatchAsync(() => handler.ExecuteScript(script));
        }
    }
}
