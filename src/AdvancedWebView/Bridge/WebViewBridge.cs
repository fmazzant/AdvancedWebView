using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;

namespace Mafesoft.Maui.Controls
{
    /// <summary>
    /// Platform-independent core of the bridge: checks origins, parses page messages, runs handlers,
    /// tracks C# calls to page functions and builds the replies.
    /// The platform handlers feed it raw strings and deliver the envelopes it produces.
    /// </summary>
    internal sealed class WebViewBridge
    {
        internal const string ReflectionWarning =
            "Reflection-based JSON serialization may need types that trimming removes. Use the overload that takes a JsonTypeInfo.";

        /// <summary>camelCase names, case-insensitive matching: the usual conventions on both sides of the bridge.</summary>
        internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        internal delegate Task<string?> Handler(JsonElement args, CancellationToken cancellationToken);

        readonly BridgeOptions options;
        readonly Func<string, Task> executeScript;
        readonly Action<WebMessageReceivedEventArgs> raiseMessage;
        readonly Action<string>? log;
        readonly ConcurrentDictionary<string, Handler> handlers = new(StringComparer.Ordinal);
        readonly ConcurrentDictionary<string, TaskCompletionSource<string?>> calls = new(StringComparer.Ordinal);
        readonly ConcurrentDictionary<string, byte> loggedOrigins = new(StringComparer.OrdinalIgnoreCase);
        readonly object gate = new();

        string? mainPageId;
        CancellationTokenSource pageCts = new();

        /// <param name="options">Bridge settings, read on every message.</param>
        /// <param name="executeScript">Runs a script in the top-level page (main thread handled by the caller).</param>
        /// <param name="raiseMessage">Raises <c>MessageReceived</c>.</param>
        /// <param name="log">Receives warnings, for example messages dropped because of their origin.</param>
        public WebViewBridge(BridgeOptions options, Func<string, Task> executeScript, Action<WebMessageReceivedEventArgs> raiseMessage, Action<string>? log = null)
        {
            this.options = options;
            this.executeScript = executeScript;
            this.raiseMessage = raiseMessage;
            this.log = log;
        }

        public void SetHandler(string method, Handler handler)
        {
            ArgumentException.ThrowIfNullOrEmpty(method);
            ArgumentNullException.ThrowIfNull(handler);
            handlers[method] = handler;
        }

        public bool RemoveHandler(string method) => handlers.TryRemove(method, out _);

        /// <summary>Entry point for raw strings posted by the page.</summary>
        /// <param name="text">The string the page posted.</param>
        /// <param name="origin">Origin of the sending frame; null for local content.</param>
        /// <param name="isMainFrame">Whether the sender is the top-level page.</param>
        /// <param name="replyToFrame">
        /// Delivers an envelope back to the sending frame. When null, replies go to the top-level page.
        /// </param>
        public void OnMessageFromPage(string? text, Uri? origin, bool isMainFrame, Func<string, Task>? replyToFrame = null)
        {
            var envelope = BridgeProtocol.Parse(text);
            if (envelope is null)
                return;

            Func<string, Task> reply = replyToFrame ?? (e => executeScript(BridgeProtocol.ReceiveScript(e)));

            // Answers to InvokeJavaScriptAsync: the app called the page itself, so they are accepted from any origin,
            // but only from the top-level page, which is where the call was made.
            if (envelope.Type is BridgeProtocol.CallResult or BridgeProtocol.CallError)
            {
                if (isMainFrame)
                    CompleteCall(envelope);
                return;
            }

            if (!isMainFrame && !options.AllowSubframes)
            {
                LogOnce(origin, "subframe", "Bridge message from an iframe ({0}) ignored. Set Bridge.AllowSubframes = true to allow iframes.");
                return;
            }

            if (!options.IsOriginAllowed(origin))
            {
                LogOnce(origin, "origin", "Bridge message from {0} ignored: the origin is not in Bridge.AllowedOrigins.");
                if (envelope.Type == BridgeProtocol.Invoke)
                {
                    _ = SendAsync(reply, BridgeProtocol.ErrorEnvelope(envelope.PageId, envelope.Id!, BridgeErrorCodes.OriginNotAllowed,
                        $"The origin {origin?.GetLeftPart(UriPartial.Authority) ?? "(local)"} is not allowed to use the AdvancedWebView bridge."));
                }
                return;
            }

            var pageToken = isMainFrame ? EnterPage(envelope.PageId) : CurrentPageToken();

            if (envelope.Type == BridgeProtocol.Message)
                raiseMessage(new WebMessageReceivedEventArgs(envelope.Payload, origin, isMainFrame));
            else
                _ = InvokeAsync(envelope, pageToken, reply);
        }

        /// <summary>
        /// Calls a function of the top-level page, awaiting its Promise if it returns one.
        /// Returns the JSON of the result, or null for <c>null</c>/<c>undefined</c>.
        /// </summary>
        public async Task<string?> CallFunctionAsync(string functionName, string argumentsJson)
        {
            ArgumentException.ThrowIfNullOrEmpty(functionName);

            var id = Guid.NewGuid().ToString("N");
            var tcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
            calls[id] = tcs;

            try
            {
                await executeScript(BridgeProtocol.CallScript(id, functionName, argumentsJson));

                var timeout = options.InvokeTimeout;
                return timeout == Timeout.InfiniteTimeSpan ? await tcs.Task : await tcs.Task.WaitAsync(timeout);
            }
            catch (TimeoutException)
            {
                throw new TimeoutException(string.Create(CultureInfo.InvariantCulture,
                    $"The page did not answer the call to '{functionName}' within {options.InvokeTimeout.TotalSeconds:0.###} seconds. Check that JavaScript is enabled and that the page has finished loading."));
            }
            finally
            {
                calls.TryRemove(id, out _);
            }
        }

        /// <summary>Cancels running handlers and pending calls, for example when the native view is disconnected.</summary>
        public void Reset()
        {
            lock (gate)
            {
                pageCts.Cancel();
                pageCts.Dispose();
                pageCts = new CancellationTokenSource();
                mainPageId = null;
            }

            foreach (var call in calls.Values)
                call.TrySetCanceled();
        }

        // A new pageId from the top-level page means the previous page is gone: cancel the calls it started.
        CancellationToken EnterPage(string pageId)
        {
            lock (gate)
            {
                if (mainPageId != pageId)
                {
                    pageCts.Cancel();
                    pageCts.Dispose();
                    pageCts = new CancellationTokenSource();
                    mainPageId = pageId;
                }

                return pageCts.Token;
            }
        }

        CancellationToken CurrentPageToken()
        {
            lock (gate)
                return pageCts.Token;
        }

        void CompleteCall(PageEnvelope envelope)
        {
            if (!calls.TryGetValue(envelope.Id!, out var tcs))
                return;

            if (envelope.Type == BridgeProtocol.CallResult)
            {
                tcs.TrySetResult(envelope.Payload.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
                    ? null
                    : envelope.Payload.GetRawText());
                return;
            }

            var error = envelope.Payload;
            var message = error.ValueKind == JsonValueKind.Object && error.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String
                ? m.GetString()
                : null;
            var stack = error.ValueKind == JsonValueKind.Object && error.TryGetProperty("stack", out var s) && s.ValueKind == JsonValueKind.String
                ? s.GetString()
                : null;
            tcs.TrySetException(new JavaScriptException(string.IsNullOrEmpty(message) ? "The JavaScript function failed." : message, stack));
        }

        async Task InvokeAsync(PageEnvelope call, CancellationToken pageToken, Func<string, Task> reply)
        {
            var method = call.Method!;
            var pageId = call.PageId;
            var id = call.Id!;
            string envelope;

            if (!handlers.TryGetValue(method, out var handler))
            {
                await SendAsync(reply, BridgeProtocol.ErrorEnvelope(pageId, id, BridgeErrorCodes.MethodNotFound,
                    $"No handler is registered for '{method}'."));
                return;
            }

            using var callCts = CancellationTokenSource.CreateLinkedTokenSource(pageToken);
            var timeout = options.InvokeTimeout;

            try
            {
                var task = handler(call.Payload, callCts.Token);
                var result = timeout == Timeout.InfiniteTimeSpan
                    ? await task.WaitAsync(pageToken)
                    : await task.WaitAsync(timeout, pageToken);

                envelope = BridgeProtocol.ResultEnvelope(pageId, id, result);
            }
            catch (OperationCanceledException) when (pageToken.IsCancellationRequested)
            {
                // The page that made the call is gone: nobody is waiting for the answer.
                // Cancel the handler explicitly: cancellation callbacks run in reverse order, so this method can resume
                // (and dispose callCts) before the link from pageToken has cancelled the handler's token.
                callCts.Cancel();
                return;
            }
            catch (TimeoutException)
            {
                callCts.Cancel();
                envelope = BridgeProtocol.ErrorEnvelope(pageId, id, BridgeErrorCodes.Timeout,
                    string.Create(CultureInfo.InvariantCulture, $"'{method}' did not complete within {timeout.TotalSeconds:0.###} seconds."));
            }
            catch (BridgeException ex)
            {
                envelope = BridgeProtocol.ErrorEnvelope(pageId, id, ex.Code, ex.Message, Details(ex));
            }
            catch (Exception ex)
            {
                envelope = BridgeProtocol.ErrorEnvelope(pageId, id, BridgeErrorCodes.HandlerError, ex.Message, Details(ex));
            }

            await SendAsync(reply, envelope);
        }

        string? Details(Exception ex) => options.IncludeExceptionDetails ? ex.ToString() : null;

        void LogOnce(Uri? origin, string reason, string format)
        {
            var text = origin?.GetLeftPart(UriPartial.Authority) ?? "local content";
            if (log is not null && loggedOrigins.TryAdd(reason + "|" + text, 0))
                log(string.Format(CultureInfo.InvariantCulture, format, text));
        }

        static async Task SendAsync(Func<string, Task> reply, string envelope)
        {
            try
            {
                await reply(envelope);
            }
            catch (Exception)
            {
                // The view or the frame went away while the handler ran; the reply has nowhere to go.
            }
        }
    }
}
