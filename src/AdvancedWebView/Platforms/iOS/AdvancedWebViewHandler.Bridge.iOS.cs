using Foundation;
using WebKit;

namespace Mafesoft.Maui.Controls.Handlers
{
    // iOS transport of the JS bridge (WKScriptMessageHandler) and of user scripts (WKUserScript).
    public partial class AdvancedWebViewHandler
    {
        BridgeScriptMessageHandler? bridgeMessageHandler;

        // Called from CreatePlatformView: the configuration is copied when the WKWebView is created.
        void SetUpBridge(WKWebViewConfiguration config)
        {
            bridgeMessageHandler = new BridgeScriptMessageHandler(this);
            config.UserContentController.AddScriptMessageHandler(bridgeMessageHandler, BridgeScript.IosHandlerName);
            ApplyUserScripts(config.UserContentController);
        }

        /// <inheritdoc/>
        protected override void DisconnectHandler(WKWebView platformView)
        {
            StopObservingPageState();

            // WKUserContentController keeps a strong reference to its handlers: remove ours to avoid a leak.
            var controller = platformView.Configuration.UserContentController;
            controller.RemoveScriptMessageHandler(BridgeScript.IosHandlerName);
            controller.RemoveAllUserScripts();
            bridgeMessageHandler?.Detach();
            bridgeMessageHandler = null;

            Advanced?.BridgeCore.Reset();
            base.DisconnectHandler(platformView);
        }

        internal partial void ExecuteScript(string script) =>
            PlatformView?.EvaluateJavaScript(script, (_, _) => { });

        internal partial void UpdateUserScripts()
        {
            if (PlatformView is { } webView)
                ApplyUserScripts(webView.Configuration.UserContentController);
        }

        void ApplyUserScripts(WKUserContentController controller)
        {
            controller.RemoveAllUserScripts();

            // The bridge goes into every frame; the core decides whether iframes may use it (Bridge.AllowSubframes).
            controller.AddUserScript(new WKUserScript(new NSString(BridgeScript.Source), WKUserScriptInjectionTime.AtDocumentStart, isForMainFrameOnly: false));

            if (Advanced is not { } view)
                return;

            foreach (var script in view.ResolveUserScripts())
            {
                var time = script.InjectionTime == UserScriptInjectionTime.DocumentStart
                    ? WKUserScriptInjectionTime.AtDocumentStart
                    : WKUserScriptInjectionTime.AtDocumentEnd;
                controller.AddUserScript(new WKUserScript(new NSString(script.Code), time, script.MainFrameOnly));
            }
        }

        sealed class BridgeScriptMessageHandler : NSObject, IWKScriptMessageHandler
        {
            WeakReference<AdvancedWebViewHandler>? handler;

            public BridgeScriptMessageHandler(AdvancedWebViewHandler handler) =>
                this.handler = new WeakReference<AdvancedWebViewHandler>(handler);

            public void Detach() => handler = null;

            public void DidReceiveScriptMessage(WKUserContentController userContentController, WKScriptMessage message)
            {
                if (handler is null || !handler.TryGetTarget(out var target))
                    return;

                var frame = message.FrameInfo;
                var webView = message.WebView;

                // Replies go back to the frame that sent the message (works for iframes too).
                Func<string, Task>? reply = webView is null
                    ? null
                    : envelope => MainThread.InvokeOnMainThreadAsync(() =>
                        webView.EvaluateJavaScript(BridgeProtocol.ReceiveScript(envelope), frame, WKContentWorld.Page, (_, _) => { }));

                target.Advanced?.BridgeCore.OnMessageFromPage(
                    (message.Body as NSString)?.ToString(),
                    ToUri(frame.SecurityOrigin),
                    frame.MainFrame,
                    reply);
            }

            static Uri? ToUri(WKSecurityOrigin? origin)
            {
                if (origin is null || string.IsNullOrEmpty(origin.Protocol) || string.IsNullOrEmpty(origin.Host))
                    return null;

                var builder = new UriBuilder(origin.Protocol, origin.Host);
                if (origin.Port > 0)
                    builder.Port = (int)origin.Port;
                return builder.Uri;
            }
        }
    }
}
