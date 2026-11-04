using AndroidX.WebKit;
using Microsoft.Extensions.Logging;
using AWebView = Android.Webkit.WebView;

namespace Mafesoft.Maui.Controls.Handlers
{
    // Android transport of the JS bridge (androidx.webkit).
    public partial class AdvancedWebViewHandler
    {
        static readonly string[] AllOrigins = ["*"];

        BridgeMessageListener? bridgeListener;
        IScriptHandler? bridgeScriptHandler;
        bool injectBridgeOnPageLoad;

        /// <inheritdoc/>
        protected override void ConnectHandler(AWebView platformView)
        {
            base.ConnectHandler(platformView);

            if (WebViewFeature.IsFeatureSupported(WebViewFeature.WebMessageListener))
            {
                bridgeListener = new BridgeMessageListener(this);
                WebViewCompat.AddWebMessageListener(platformView, BridgeScript.AndroidObjectName, AllOrigins, bridgeListener);
            }
            else
            {
                Log("This Android System WebView does not support WebMessageListener: the JavaScript bridge is disabled. Update Android System WebView.");
                return;
            }

            if (WebViewFeature.IsFeatureSupported(WebViewFeature.DocumentStartScript))
            {
                bridgeScriptHandler = WebViewCompat.AddDocumentStartJavaScript(platformView, BridgeScript.Source, AllOrigins);
            }
            else
            {
                // Older WebView: inject after each load. Page scripts must wait for AdvancedWebView.ready
                // or the "advancedwebviewready" event.
                injectBridgeOnPageLoad = true;
            }
        }

        /// <inheritdoc/>
        protected override void DisconnectHandler(AWebView platformView)
        {
            if (bridgeListener is not null)
            {
                WebViewCompat.RemoveWebMessageListener(platformView, BridgeScript.AndroidObjectName);
                bridgeListener.Detach();
                bridgeListener = null;
            }

            bridgeScriptHandler?.Remove();
            bridgeScriptHandler = null;
            injectBridgeOnPageLoad = false;

            Advanced?.BridgeCore.Reset();
            base.DisconnectHandler(platformView);
        }

        internal partial void ExecuteScript(string script) => PlatformView?.EvaluateJavascript(script, null);

        partial void OnPageLoadedCore()
        {
            if (injectBridgeOnPageLoad)
                ExecuteScript(BridgeScript.Source);
        }

        void Log(string message) =>
            MauiContext?.Services.GetService<ILogger<AdvancedWebViewHandler>>()?.LogWarning("{Message}", message);

        sealed class BridgeMessageListener : Java.Lang.Object, WebViewCompat.IWebMessageListener
        {
            AdvancedWebViewHandler? handler;

            public BridgeMessageListener(AdvancedWebViewHandler handler) => this.handler = handler;

            public void Detach() => handler = null;

            public void OnPostMessage(AWebView? view, WebMessageCompat? message, Android.Net.Uri? sourceOrigin, bool isMainFrame, JavaScriptReplyProxy? replyProxy)
            {
                var origin = sourceOrigin?.ToString();
                Uri.TryCreate(origin, UriKind.Absolute, out var originUri);
                handler?.Advanced?.BridgeCore.OnMessageFromPage(message?.Data, originUri, isMainFrame);
            }
        }
    }
}
