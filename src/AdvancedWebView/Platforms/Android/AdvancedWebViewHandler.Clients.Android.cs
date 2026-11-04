using Android.Graphics;
using Microsoft.Maui.Platform;
using AWebView = Android.Webkit.WebView;

namespace Mafesoft.Maui.Controls.Handlers
{
    // Android: MAUI's WebViewClient and WebChromeClient, extended. Subclassing instead of replacing them keeps
    // navigation events, cookies, alerts, file pickers and fullscreen video working.
    public partial class AdvancedWebViewHandler
    {
        AdvancedWebViewClient? webViewClient;
        AdvancedWebChromeClient? webChromeClient;

        // Mapped to the "WebViewClient" and "WebChromeClient" keys of the MAUI mapper, replacing MAUI's own mapping.
        static void MapAdvancedWebViewClient(AdvancedWebViewHandler handler, AdvancedWebView view) =>
            handler.PlatformView.SetWebViewClient(handler.webViewClient ??= new AdvancedWebViewClient(handler));

        static void MapAdvancedWebChromeClient(AdvancedWebViewHandler handler, AdvancedWebView view) =>
            handler.PlatformView.SetWebChromeClient(handler.webChromeClient ??= new AdvancedWebChromeClient(handler));

        // MAUI's DisconnectHandler disconnects and detaches the clients; they cannot be reused afterwards.
        void ForgetClients()
        {
            webViewClient = null;
            webChromeClient = null;
        }

        sealed class AdvancedWebViewClient : MauiWebViewClient
        {
            readonly WeakReference<AdvancedWebViewHandler> handler;

            public AdvancedWebViewClient(AdvancedWebViewHandler handler) : base(handler) =>
                this.handler = new WeakReference<AdvancedWebViewHandler>(handler);

            AdvancedWebView? View => handler.TryGetTarget(out var h) ? h.Advanced : null;

            public override void OnPageStarted(AWebView? view, string? url, Bitmap? favicon)
            {
                base.OnPageStarted(view, url, favicon);
                View?.OnLoadStarted(url);
            }

            public override void OnPageFinished(AWebView? view, string? url)
            {
                base.OnPageFinished(view, url);
                View?.OnLoadFinished(view?.Url ?? url);
            }

            // Also called for history.pushState / replaceState, which do not start a page load.
            public override void DoUpdateVisitedHistory(AWebView? view, string? url, bool isReload)
            {
                base.DoUpdateVisitedHistory(view, url, isReload);
                View?.SetCurrentUrl(view?.Url ?? url);
            }
        }

        sealed class AdvancedWebChromeClient : MauiWebChromeClient
        {
            readonly WeakReference<AdvancedWebViewHandler> handler;

            public AdvancedWebChromeClient(AdvancedWebViewHandler handler) : base(handler) =>
                this.handler = new WeakReference<AdvancedWebViewHandler>(handler);

            AdvancedWebView? View => handler.TryGetTarget(out var h) ? h.Advanced : null;

            public override void OnProgressChanged(AWebView? view, int newProgress)
            {
                base.OnProgressChanged(view, newProgress);
                if (View is not { } v)
                    return;

                v.SetProgress(newProgress / 100d);

                // Also ends the load when OnPageFinished is not reported, for example after a cancelled Navigating.
                if (newProgress >= 100)
                    v.SetIsLoading(false);
            }

            public override void OnReceivedTitle(AWebView? view, string? title)
            {
                base.OnReceivedTitle(view, title);
                View?.SetTitle(title);
            }
        }
    }
}
