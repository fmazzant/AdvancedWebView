using Foundation;
using WebKit;

namespace Mafesoft.Maui.Controls.Handlers
{
    // iOS transport of the JS bridge (WKScriptMessageHandler).
    public partial class AdvancedWebViewHandler
    {
        // Called from CreatePlatformView: user scripts and message handlers belong to the configuration.
        static void AddBridge(WKWebViewConfiguration config, BridgeScriptMessageHandler messageHandler)
        {
            var controller = config.UserContentController;
            controller.AddUserScript(new WKUserScript(
                new NSString(BridgeScript.Source),
                WKUserScriptInjectionTime.AtDocumentStart,
                isForMainFrameOnly: true));
            controller.AddScriptMessageHandler(messageHandler, BridgeScript.IosHandlerName);
        }

        BridgeScriptMessageHandler? bridgeMessageHandler;

        /// <inheritdoc/>
        protected override void DisconnectHandler(WKWebView platformView)
        {
            // WKUserContentController keeps a strong reference to its handlers: remove ours to avoid a leak.
            platformView.Configuration.UserContentController.RemoveScriptMessageHandler(BridgeScript.IosHandlerName);
            bridgeMessageHandler?.Detach();
            bridgeMessageHandler = null;

            Advanced?.BridgeCore.Reset();
            base.DisconnectHandler(platformView);
        }

        internal partial void ExecuteScript(string script) =>
            PlatformView?.EvaluateJavaScript(script, (_, _) => { });

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
                target.Advanced?.BridgeCore.OnMessageFromPage(
                    (message.Body as NSString)?.ToString(),
                    ToUri(frame.SecurityOrigin),
                    frame.MainFrame);
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
