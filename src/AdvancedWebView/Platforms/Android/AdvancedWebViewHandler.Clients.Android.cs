using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Webkit;
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

        static void MapAdvancedWebChromeClient(AdvancedWebViewHandler handler, AdvancedWebView view)
        {
            // Without multiple windows, Android loads target="_blank" and window.open in this view without telling us.
            handler.PlatformView.Settings.SetSupportMultipleWindows(true);
            handler.PlatformView.SetWebChromeClient(handler.webChromeClient ??= new AdvancedWebChromeClient(handler));
        }

        void OpenNewWindow(string url, bool isUserGesture)
        {
            if (Advanced is { } view && view.OnNewWindowRequested(url, isUserGesture))
                NavigateInView(url);
        }

        /// <summary>
        /// Loads a URL the app decided to open (new window in the same view, intent fallback) with the checks of a
        /// normal navigation. LoadUrl skips ShouldOverrideUrlLoading, where MAUI raises Navigating.
        /// </summary>
        void NavigateInView(string url)
        {
            if (PlatformView is not { } webView || Advanced is not { } view)
                return;

            if (((IWebView)view).Navigating(WebNavigationEvent.NewPage, url))
                return; // cancelled by the app

            if (!view.TryOpenExternalLink(url, isMainFrame: true, OpenExternal))
                webView.LoadUrl(url);
        }

        /// <summary>Opens an external URL: intent:// with Android's own parser, anything else with the shared Launcher code.</summary>
        void OpenExternal(string url)
        {
            if (ExternalLinkRules.GetScheme(url) == "intent")
                OpenIntent(url);
            else
                Advanced?.OpenOutside(url);
        }

        // intent://host/path#Intent;scheme=...;package=...;S.browser_fallback_url=...;end
        // Opened like Chrome does: only browsable activities, no explicit component, fallback URL when no app handles it.
        void OpenIntent(string url)
        {
            if (PlatformView?.Context is not { } context)
                return;

            Intent intent;
            try
            {
                intent = Intent.ParseUri(url, IntentUriType.Scheme)!;
            }
            catch (Exception ex)
            {
                Log($"Invalid intent URL {url}: {ex.Message}");
                return;
            }

            intent.AddCategory(Intent.CategoryBrowsable);
            intent.SetComponent(null);
            intent.Selector = null;
            if (context is not Android.App.Activity)
                intent.AddFlags(ActivityFlags.NewTask);

            try
            {
                context.StartActivity(intent);
            }
            catch (ActivityNotFoundException)
            {
                var fallback = intent.GetStringExtra("browser_fallback_url");
                if (ExternalLinkRules.GetScheme(fallback) is "http" or "https")
                    NavigateInView(fallback!);
                else
                    Log($"No app can open {url}, and it has no browser_fallback_url.");
            }
        }

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

            // Links and script navigations (not LoadUrl). MAUI's base raises Navigating first: a URL cancelled there
            // is not opened outside either.
            public override bool ShouldOverrideUrlLoading(AWebView? view, IWebResourceRequest? request)
            {
                if (base.ShouldOverrideUrlLoading(view, request))
                    return true;

                return handler.TryGetTarget(out var owner) && owner.Advanced is { } v &&
                    v.TryOpenExternalLink(request?.Url?.ToString(), request?.IsForMainFrame ?? true, owner.OpenExternal);
            }

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

            // target="_blank" and window.open. The URL is not passed here: hand the page a hidden web view and catch
            // the first URL it tries to load.
            public override bool OnCreateWindow(AWebView? view, bool isDialog, bool isUserGesture, Message? resultMsg)
            {
                if (view?.Context is not { } context || resultMsg?.Obj is not AWebView.WebViewTransport transport ||
                    !handler.TryGetTarget(out var owner))
                    return false;

                var popup = new AWebView(context);
                popup.SetWebViewClient(new PopupUrlCatcher(url => owner.OpenNewWindow(url, isUserGesture)));
                transport.WebView = popup;
                resultMsg.SendToTarget();
                return true;
            }
        }

        /// <summary>WebViewClient of the hidden popup web view: reports its first real URL, then destroys it.</summary>
        sealed class PopupUrlCatcher : WebViewClient
        {
            Action<string>? onUrl;

            public PopupUrlCatcher(Action<string> onUrl) => this.onUrl = onUrl;

            public override bool ShouldOverrideUrlLoading(AWebView? view, IWebResourceRequest? request)
            {
                Catch(view, request?.Url?.ToString());
                return true;
            }

            public override void OnPageStarted(AWebView? view, string? url, Bitmap? favicon)
            {
                base.OnPageStarted(view, url, favicon);
                Catch(view, url);
            }

            void Catch(AWebView? view, string? url)
            {
                // window.open() starts with about:blank; window.open() without a URL never gets further.
                if (onUrl is not { } callback || !AdvancedWebView.IsNewWindowUrl(url))
                    return;

                onUrl = null;
                view?.StopLoading();
                view?.Post(view.Destroy);
                callback(url!);
            }
        }
    }
}
