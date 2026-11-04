using AndroidX.WebKit;
using Microsoft.Extensions.Logging;
using AWebView = Android.Webkit.WebView;

namespace Mafesoft.Maui.Controls.Handlers
{
    // Android transport of the JS bridge and of user scripts (androidx.webkit).
    public partial class AdvancedWebViewHandler
    {
        // Native injection is not filtered by origin: the bridge core checks Bridge.AllowedOrigins,
        // which also has to admit local content, and user scripts carry their own origin guard.
        static readonly string[] AllOrigins = ["*"];

        BridgeMessageListener? bridgeListener;
        IScriptHandler? bridgeScriptHandler;
        readonly List<IScriptHandler> userScriptHandlers = [];
        List<string> pageLoadScripts = [];
        bool documentStartSupported;
        bool bridgeOnPageLoad;

        /// <inheritdoc/>
        protected override void ConnectHandler(AWebView platformView)
        {
            base.ConnectHandler(platformView);

            documentStartSupported = WebViewFeature.IsFeatureSupported(WebViewFeature.DocumentStartScript);

            if (WebViewFeature.IsFeatureSupported(WebViewFeature.WebMessageListener))
            {
                bridgeListener = new BridgeMessageListener(this);
                WebViewCompat.AddWebMessageListener(platformView, BridgeScript.AndroidObjectName, AllOrigins, bridgeListener);

                if (documentStartSupported)
                    bridgeScriptHandler = WebViewCompat.AddDocumentStartJavaScript(platformView, BridgeScript.Source, AllOrigins);
                else
                    bridgeOnPageLoad = true; // older WebView: inject after each load
            }
            else
            {
                Log("This Android System WebView does not support WebMessageListener: the JavaScript bridge is disabled. Update Android System WebView.");
            }

            UpdateUserScripts();
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
            bridgeOnPageLoad = false;
            RemoveUserScripts();

            ExitFullscreen();
            Advanced?.BridgeCore.Reset();
            base.DisconnectHandler(platformView);
            ForgetClients();
            SetInspectable(false); // release this view's vote on the process-wide debugging switch
        }

        internal partial void ExecuteScript(string script) => PlatformView?.EvaluateJavascript(script, null);

        internal partial void UpdateUserScripts()
        {
            if (PlatformView is not { } webView || Advanced is not { } view)
                return;

            RemoveUserScripts();
            var onLoad = new List<string>();

            foreach (var script in view.ResolveUserScripts())
            {
                if (script.InjectionTime == UserScriptInjectionTime.DocumentStart && documentStartSupported)
                {
                    // Document-start scripts are injected into every frame; the guard keeps main-frame-only scripts out of iframes.
                    var code = UserScriptCode.Wrap(script.Code, [], script.MainFrameOnly);
                    if (WebViewCompat.AddDocumentStartJavaScript(webView, code, AllOrigins) is { } scriptHandler)
                        userScriptHandlers.Add(scriptHandler);
                }
                else
                {
                    // DocumentEnd scripts, and DocumentStart ones on older WebViews, run when the page has loaded (top-level page only).
                    onLoad.Add(script.Code);
                }
            }

            pageLoadScripts = onLoad;
        }

        partial void OnPageLoadedCore()
        {
            if (bridgeOnPageLoad)
                ExecuteScript(BridgeScript.Source);

            foreach (var script in pageLoadScripts)
                ExecuteScript(script);
        }

        void RemoveUserScripts()
        {
            foreach (var handler in userScriptHandlers)
                handler.Remove();
            userScriptHandlers.Clear();
            pageLoadScripts = [];
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
                Uri.TryCreate(sourceOrigin?.ToString(), UriKind.Absolute, out var origin);

                // Replies go back through the proxy of the frame that sent the message (works for iframes too).
                Func<string, Task>? reply = replyProxy is null
                    ? null
                    : envelope => MainThread.InvokeOnMainThreadAsync(() => replyProxy.PostMessage(envelope));

                handler?.Advanced?.BridgeCore.OnMessageFromPage(message?.Data, origin, isMainFrame, reply);
            }
        }
    }
}
