using Microsoft.Maui.Platform;
using WebKit;

namespace Mafesoft.Maui.Controls.Handlers
{
    // iOS: new windows. Links with target="_blank" reach the navigation delegate (no target frame),
    // window.open reaches the UI delegate (CreateWebView). Both are subclasses of MAUI's delegates.
    public partial class AdvancedWebViewHandler
    {
        AdvancedUIDelegate? uiDelegate;

        // Mapped to the "WKUIDelegate" key of the MAUI mapper, replacing MAUI's own mapping.
        static void MapAdvancedUIDelegate(AdvancedWebViewHandler handler, AdvancedWebView view) =>
            handler.PlatformView.UIDelegate = handler.uiDelegate ??= new AdvancedUIDelegate(handler);

        void OpenNewWindow(WKWebView webView, WKNavigationAction action)
        {
            var url = action.Request.Url?.AbsoluteString;
            if (Advanced is not { } view || !AdvancedWebView.IsNewWindowUrl(url))
                return;

            // WebKit does not expose the user gesture of window.open. With JavaScriptCanOpenWindowsAutomatically off,
            // it only lets window.open through during a user action.
            var isUserGesture = action.NavigationType == WKNavigationType.LinkActivated || !view.JavaScriptCanOpenWindowsAutomatically;

            // LoadRequest goes through DecidePolicy, where MAUI raises Navigating. Reusing the request keeps a form's POST body.
            if (view.OnNewWindowRequested(url!, isUserGesture))
                webView.LoadRequest(action.Request);
        }

        // Other part: AdvancedWebViewHandler.Errors.iOS.cs (load failures, HTTP errors, certificates).
        sealed partial class AdvancedNavigationDelegate : MauiWebViewNavigationDelegate
        {
            readonly WeakReference<AdvancedWebViewHandler> handler;

            public AdvancedNavigationDelegate(AdvancedWebViewHandler handler) : base(handler) =>
                this.handler = new WeakReference<AdvancedWebViewHandler>(handler);

            public override void DecidePolicy(WKWebView webView, WKNavigationAction navigationAction, Action<WKNavigationActionPolicy> decisionHandler)
            {
                // A link with the download attribute: DownloadRequested instead of a navigation.
                if (handler.TryGetTarget(out var downloader) && downloader.TryStartDownload(navigationAction))
                {
                    decisionHandler(WKNavigationActionPolicy.Cancel);
                    return;
                }

                // No target frame: the page asked for a new window. MAUI would load it in this view; decide here instead.
                if (navigationAction.TargetFrame is null && handler.TryGetTarget(out var owner))
                {
                    decisionHandler(WKNavigationActionPolicy.Cancel);
                    owner.OpenNewWindow(webView, navigationAction);
                    return;
                }

                // MAUI raises Navigating and decides first; a navigation it allows may still leave the app
                // (tel:, mailto:, hosts outside InternalHosts). Launcher also opens universal links in their app.
                base.DecidePolicy(webView, navigationAction, policy =>
                {
                    if (policy == WKNavigationActionPolicy.Allow && handler.TryGetTarget(out var target) && target.Advanced is { } view)
                    {
                        var isMainFrame = navigationAction.TargetFrame?.MainFrame ?? true;
                        if (view.TryOpenExternalLink(navigationAction.Request.Url?.AbsoluteString, isMainFrame))
                        {
                            decisionHandler(WKNavigationActionPolicy.Cancel);
                            return;
                        }

                        // #60: LoadUrl headers on their way to another origin (a redirect, or a reload or back navigation
                        // of a redirected page). The request is loaded again without them.
                        if (isMainFrame && target.WithoutLeakedHeaders(navigationAction.Request) is { } clean)
                        {
                            decisionHandler(WKNavigationActionPolicy.Cancel);
                            webView.LoadRequest(clean);
                            return;
                        }
                    }

                    decisionHandler(policy);
                });
            }
        }

        sealed class AdvancedUIDelegate : MauiWebViewUIDelegate
        {
            readonly WeakReference<AdvancedWebViewHandler> handler;

            public AdvancedUIDelegate(AdvancedWebViewHandler handler) : base(handler) =>
                this.handler = new WeakReference<AdvancedWebViewHandler>(handler);

            // getUserMedia: PermissionRequested decides between WebKit's dialog (Prompt), Grant and Deny.
            public override void RequestMediaCapturePermission(WKWebView webView, WKSecurityOrigin origin, WKFrameInfo frame, WKMediaCaptureType type, Action<WKPermissionDecision> decisionHandler)
            {
                if (handler.TryGetTarget(out var owner))
                    owner.RequestMediaCapturePermission(origin, type, decisionHandler);
                else
                    decisionHandler(WKPermissionDecision.Prompt);
            }

            // window.open: returning null tells WebKit that no window was created.
            public override WKWebView? CreateWebView(WKWebView webView, WKWebViewConfiguration configuration, WKNavigationAction navigationAction, WKWindowFeatures windowFeatures)
            {
                if (handler.TryGetTarget(out var owner))
                    owner.OpenNewWindow(webView, navigationAction);
                return null;
            }
        }
    }
}
