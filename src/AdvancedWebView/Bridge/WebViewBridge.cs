using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;

namespace Mafesoft.Maui.Controls
{
    /// <summary>
    /// Platform-independent core of the bridge: parses page messages, runs handlers and builds the replies.
    /// The platform handlers feed it raw strings and execute the scripts it produces.
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
        readonly ConcurrentDictionary<string, Handler> handlers = new(StringComparer.Ordinal);
        readonly object gate = new();

        string? currentPageId;
        CancellationTokenSource pageCts = new();

        public WebViewBridge(BridgeOptions options, Func<string, Task> executeScript, Action<WebMessageReceivedEventArgs> raiseMessage)
        {
            this.options = options;
            this.executeScript = executeScript;
            this.raiseMessage = raiseMessage;
        }

        public void SetHandler(string method, Handler handler)
        {
            ArgumentException.ThrowIfNullOrEmpty(method);
            ArgumentNullException.ThrowIfNull(handler);
            handlers[method] = handler;
        }

        public bool RemoveHandler(string method) => handlers.TryRemove(method, out _);

        /// <summary>Entry point for raw strings posted by the page.</summary>
        public void OnMessageFromPage(string? text, Uri? origin, bool isMainFrame)
        {
            // Iframes cannot use the bridge (yet): only the top-level page is trusted.
            if (!isMainFrame)
                return;

            var envelope = BridgeProtocol.Parse(text);
            if (envelope is null)
                return;

            var pageToken = EnterPage(envelope.PageId);

            if (envelope.Type == "message")
                raiseMessage(new WebMessageReceivedEventArgs(envelope.Payload, origin, isMainFrame));
            else
                _ = InvokeAsync(envelope, pageToken);
        }

        /// <summary>Cancels running handlers, for example when the native view is disconnected.</summary>
        public void Reset()
        {
            lock (gate)
            {
                pageCts.Cancel();
                pageCts.Dispose();
                pageCts = new CancellationTokenSource();
                currentPageId = null;
            }
        }

        // A new pageId means the previous page is gone: cancel the calls it started.
        CancellationToken EnterPage(string pageId)
        {
            lock (gate)
            {
                if (currentPageId != pageId)
                {
                    pageCts.Cancel();
                    pageCts.Dispose();
                    pageCts = new CancellationTokenSource();
                    currentPageId = pageId;
                }

                return pageCts.Token;
            }
        }

        async Task InvokeAsync(PageEnvelope call, CancellationToken pageToken)
        {
            var method = call.Method!;
            var pageId = call.PageId;
            var id = call.Id!;
            string script;

            if (!handlers.TryGetValue(method, out var handler))
            {
                script = BridgeProtocol.ErrorScript(pageId, id, BridgeErrorCodes.MethodNotFound,
                    $"No handler is registered for '{method}'.");
                await SendAsync(script);
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

                script = BridgeProtocol.ResultScript(pageId, id, result);
            }
            catch (OperationCanceledException) when (pageToken.IsCancellationRequested)
            {
                // The page that made the call is gone: nobody is waiting for the answer.
                return;
            }
            catch (TimeoutException)
            {
                callCts.Cancel();
                script = BridgeProtocol.ErrorScript(pageId, id, BridgeErrorCodes.Timeout,
                    string.Create(CultureInfo.InvariantCulture, $"'{method}' did not complete within {timeout.TotalSeconds:0.###} seconds."));
            }
            catch (BridgeException ex)
            {
                script = BridgeProtocol.ErrorScript(pageId, id, ex.Code, ex.Message, Details(ex));
            }
            catch (Exception ex)
            {
                script = BridgeProtocol.ErrorScript(pageId, id, BridgeErrorCodes.HandlerError, ex.Message, Details(ex));
            }

            await SendAsync(script);
        }

        string? Details(Exception ex) => options.IncludeExceptionDetails ? ex.ToString() : null;

        async Task SendAsync(string script)
        {
            try
            {
                await executeScript(script);
            }
            catch (Exception)
            {
                // The view was disconnected while the handler ran; the reply has nowhere to go.
            }
        }
    }
}
