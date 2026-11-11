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

        // Same base URL as MAUI's HtmlWebViewSource: the error page is local content and can use the bridge (retry()).
        const string ErrorPageBaseUrl = "file:///android_asset/";

        /// <summary>Raises NavigationFailed for the top-level page and shows ErrorPageHtml when requested.</summary>
        void ReportFailure(AWebView? webView, string? url, WebNavigationError error, int? httpStatusCode, string? description)
        {
            if (webView is null || Advanced is not { } view || !AdvancedWebView.IsReportableUrl(url) || url!.StartsWith(ErrorPageBaseUrl, StringComparison.Ordinal))
                return;

            if (view.OnNavigationFailed(url, error, httpStatusCode, description) is { } html)
            {
                // Posted: loading from inside a WebViewClient callback can be ignored. The history entry keeps the failed URL.
                webView.Post(() => webView.LoadDataWithBaseURL(ErrorPageBaseUrl, html, "text/html", "utf-8", url));
            }
        }

        void OpenNewWindow(string url, bool isUserGesture)
        {
            if (Advanced is { } view && view.OnNewWindowRequested(url, isUserGesture))
                NavigateInView(url);
        }

        internal partial string? GetCurrentPageUrl() => PlatformView?.Url;

        // LoadUrl / PostUrlAsync. PostUrl sends the body as application/x-www-form-urlencoded, without extra headers
        // (checked by PostUrlAsync before getting here).
        internal partial void LoadRequest(WebRequest request) =>
            NavigateInView(request.Url, request.Body is { } body
                ? webView => webView.PostUrl(request.Url, body)
                : webView => webView.LoadUrl(request.Url, new Dictionary<string, string>(request.Headers)));

        /// <summary>
        /// Loads a URL from code (new window in the same view, intent fallback, LoadUrl/PostUrlAsync) with the checks of
        /// a link: Navigating first, then the external-link rules. <paramref name="load"/> defaults to a plain LoadUrl.
        /// </summary>
        void NavigateInView(string url, Action<AWebView>? load = null)
        {
            if (PlatformView is not { } webView || Advanced is not { } view)
                return;

            // MAUI's own check: raises Navigating and, when not cancelled, marks the URL so that OnPageStarted
            // does not raise Navigating a second time.
            if (NavigatingCanceled(url))
                return;

            if (view.TryOpenExternalLink(url, isMainFrame: true, OpenExternal))
            {
                UrlCanceled = null; // no page load follows to clear the mark
                return;
            }

            if (load is null)
                webView.LoadUrl(url);
            else
                load(webView);
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

            // Links, script navigations and redirects (not LoadUrl or reload). MAUI's base raises Navigating first:
            // a URL cancelled there is not opened outside either.
            public override bool ShouldOverrideUrlLoading(AWebView? view, IWebResourceRequest? request)
            {
                if (base.ShouldOverrideUrlLoading(view, request))
                    return true;

                if (!handler.TryGetTarget(out var owner) || owner.Advanced is not { } v)
                    return false;

                var url = request?.Url?.ToString();
                var isMainFrame = request?.IsForMainFrame ?? true;
                if (v.TryOpenExternalLink(url, isMainFrame, owner.OpenExternal))
                    return true;

                // IsRedirect needs Android 7 (API 24). On Android 6 redirects look like links: their headers are not checked.
                var isRedirect = OperatingSystem.IsAndroidVersionAtLeast(24) && request?.IsRedirect == true;
                if (isMainFrame && !isRedirect)
                    owner.RequestHeaders.BeginOtherNavigation();

                // #60: Android sends the LoadUrl headers to the redirect target, also on another origin. The redirect is
                // dropped and its target loaded without them. Only GET: a 307 keeps the method and body, LoadUrl cannot.
                // Navigating was raised above for this URL, and MAUI does not raise it again when the load starts.
                if (isMainFrame && isRedirect && view is not null && request is not null &&
                    string.Equals(request.Method, "GET", StringComparison.OrdinalIgnoreCase) &&
                    owner.RequestHeaders.MustDropOnRedirect(url))
                {
                    view.LoadUrl(url!);
                    return true;
                }

                return false;
            }

            // The renderer process crashed or was killed (#53). MAUI's base raises ProcessTerminated and returns false,
            // which would end the whole app: true tells Android the app handles it (the handler replaces the WebView).
            [System.Runtime.Versioning.SupportedOSPlatform("android26.0")]
            public override bool OnRenderProcessGone(AWebView? view, RenderProcessGoneDetail? detail)
            {
                base.OnRenderProcessGone(view, detail);
                if (handler.TryGetTarget(out var owner))
                    owner.OnRenderProcessGone(view, detail);
                return true;
            }

            public override void OnPageStarted(AWebView? view, string? url, Bitmap? favicon)
            {
                base.OnPageStarted(view, url, favicon);
                // Raised when the page is committed, after any redirect.
                if (handler.TryGetTarget(out var owner))
                    owner.RequestHeaders.OnCommitted(url);
                mainFrameUrl = url;
                View?.OnLoadStarted(url);
            }

            // URL of the current top-level load: tells certificate errors of the page from those of its resources.
            string? mainFrameUrl;

            public override void OnReceivedError(AWebView? view, IWebResourceRequest? request, WebResourceError? error)
            {
                base.OnReceivedError(view, request, error); // MAUI: Navigated with a failure result
                if (request?.IsForMainFrame == true && handler.TryGetTarget(out var owner))
                {
                    var description = error?.Description;
                    owner.ReportFailure(view, request.Url?.ToString(),
                        NavigationErrors.FromAndroid((int)(error?.ErrorCode ?? ClientError.Unknown), description), null, description);
                }
            }

            public override void OnReceivedHttpError(AWebView? view, IWebResourceRequest? request, WebResourceResponse? errorResponse)
            {
                base.OnReceivedHttpError(view, request, errorResponse);
                if (request?.IsForMainFrame == true && errorResponse?.StatusCode >= 400 && handler.TryGetTarget(out var owner))
                    owner.ReportFailure(view, request.Url?.ToString(), WebNavigationError.HttpError, errorResponse.StatusCode, errorResponse.ReasonPhrase);
            }

            // Not calling base: its default is to cancel, which is our default too, but the app decides first.
            public override void OnReceivedSslError(AWebView? view, SslErrorHandler? sslHandler, Android.Net.Http.SslError? error)
            {
                var url = error?.Url ?? view?.Url ?? string.Empty;
                var description = error is null ? "Invalid certificate" : $"Certificate error: {error.PrimaryError}";

                if (View?.OnServerCertificateError(url, description) == true)
                {
                    sslHandler?.Proceed();
                    return;
                }

                sslHandler?.Cancel();

                // Android reports a cancelled certificate only here, not in OnReceivedError.
                if (url == mainFrameUrl && handler.TryGetTarget(out var owner))
                    owner.ReportFailure(view, url, WebNavigationError.SslError, null, description);
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

            // <input type="file">: FileChooserRequested, multiple files and capture; MAUI's picker for the simple case.
            public override bool OnShowFileChooser(AWebView? webView, IValueCallback? filePathCallback, FileChooserParams? fileChooserParams)
            {
                if (filePathCallback is not null && fileChooserParams is not null &&
                    handler.TryGetTarget(out var owner) && owner.ShowFileChooser(filePathCallback, fileChooserParams))
                    return true;

                return base.OnShowFileChooser(webView, filePathCallback, fileChooserParams);
            }

            // Camera and microphone (getUserMedia) and location: PermissionRequested and the OS permissions.
            public override void OnPermissionRequest(PermissionRequest? request)
            {
                if (request is null || !handler.TryGetTarget(out var owner) || !owner.OnPermissionRequest(request))
                    base.OnPermissionRequest(request);
            }

            public override void OnGeolocationPermissionsShowPrompt(string? origin, GeolocationPermissions.ICallback? callback)
            {
                if (handler.TryGetTarget(out var owner))
                    owner.OnGeolocationPermissionsShowPrompt(origin, callback);
                else
                    base.OnGeolocationPermissionsShowPrompt(origin, callback);
            }

            // Fullscreen video / element.requestFullscreen. MAUI's base shows the view over the activity and hides
            // the system bars; AdvancedWebView adds FullscreenEnabled, the back button and FullscreenChanged.
            public override void OnShowCustomView(Android.Views.View? view, ICustomViewCallback? callback)
            {
                if (!handler.TryGetTarget(out var owner) || owner.Advanced is not { } v)
                {
                    base.OnShowCustomView(view, callback);
                    return;
                }

                if (!v.FullscreenEnabled)
                {
                    callback?.OnCustomViewHidden(); // tells the page that fullscreen did not happen
                    return;
                }

                base.OnShowCustomView(view, callback);
                owner.OnFullscreenEntered();
            }

            public override void OnHideCustomView()
            {
                base.OnHideCustomView();
                if (handler.TryGetTarget(out var owner))
                    owner.OnFullscreenExited();
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
