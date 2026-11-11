using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;
using WWebView2 = Microsoft.UI.Xaml.Controls.WebView2;

namespace Mafesoft.Maui.Controls.Handlers
{
    // Windows: WebView2. Most settings live on CoreWebView2, which WebView2 creates asynchronously after the control
    // is loaded: the mappers apply what they can, and every setting is applied again when CoreWebView2 is ready.
    public partial class AdvancedWebViewHandler
    {
        // Host name MAUI maps to the app package for local HTML (HtmlWebViewSource): local content, like file:// elsewhere.
        const string MauiLocalHost = "appdir";

        // Properties applied to CoreWebView2: mapped again once it exists.
        static readonly string[] CoreSettings =
        [
            nameof(AdvancedWebView.JavaScriptEnabled),
            nameof(AdvancedWebView.IsInspectable),
            nameof(AdvancedWebView.ZoomEnabled),
            nameof(AdvancedWebView.TextZoom),
            nameof(AdvancedWebView.PreferredColorScheme),
        ];

        static bool browserArgumentsSet;

        CoreWebView2? core;

        /// <summary>The CoreWebView2 of this view, or null before WebView2 has created it.</summary>
        internal CoreWebView2? Core => core;

        /// <inheritdoc/>
        protected override WWebView2 CreatePlatformView()
        {
            SetBrowserArguments();
            return base.CreatePlatformView();
        }

        // Autoplay is a browser argument, read when the first WebView2 of the app creates its browser process.
        void SetBrowserArguments()
        {
            if (browserArgumentsSet || Advanced is not { MediaPlaybackRequiresUserGesture: false })
                return;
            browserArgumentsSet = true;

            const string variable = "WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS";
            const string autoplay = "--autoplay-policy=no-user-gesture-required";
            var current = Environment.GetEnvironmentVariable(variable);
            if (current?.Contains("--autoplay-policy", StringComparison.Ordinal) == true)
                return;
            Environment.SetEnvironmentVariable(variable, string.IsNullOrEmpty(current) ? autoplay : current + " " + autoplay);
        }

        /// <inheritdoc/>
        protected override void ConnectHandler(WWebView2 platformView)
        {
            base.ConnectHandler(platformView);
            platformView.CoreWebView2Initialized += OnCoreWebView2Initialized;
            if (platformView.CoreWebView2 is { } existing)
                SetUpCore(existing);
        }

        /// <inheritdoc/>
        protected override void DisconnectHandler(WWebView2 platformView)
        {
            platformView.CoreWebView2Initialized -= OnCoreWebView2Initialized;
            TearDownCore();
            Advanced?.BridgeCore.Reset();
            base.DisconnectHandler(platformView);
        }

        void OnCoreWebView2Initialized(WWebView2 sender, Microsoft.UI.Xaml.Controls.CoreWebView2InitializedEventArgs args)
        {
            if (args.Exception is { } ex)
                Log($"WebView2 could not start: {ex.Message}. Is the WebView2 Runtime installed?");
            else if (sender.CoreWebView2 is { } created)
                SetUpCore(created);
        }

        void SetUpCore(CoreWebView2 created)
        {
            if (ReferenceEquals(core, created))
                return;

            TearDownCore();
            core = created;
            WebViewCookieManager.Register(created);

            SetUpBridge(created);
            SetUpNavigation(created);
            SetUpDownloads(created);
            SetUpPermissions(created);
            SetUpFullscreen(created);
            SetUpProcessTermination(created);
            SetUpHttpAuthentication(created);

            foreach (var key in CoreSettings)
                UpdateValue(key);
        }

        void TearDownCore()
        {
            if (core is not { } current)
                return;

            TearDownBridge(current);
            TearDownNavigation(current);
            TearDownDownloads(current);
            TearDownPermissions(current);
            TearDownFullscreen(current);
            TearDownProcessTermination(current);
            TearDownHttpAuthentication(current);
            WebViewCookieManager.Unregister(current);
            core = null;
        }

        static void MapJavaScriptEnabled(AdvancedWebViewHandler handler, AdvancedWebView view)
        {
            if (handler.core is { } c)
                c.Settings.IsScriptEnabled = view.JavaScriptEnabled;
        }

        // Read in NewWindowRequested: without a user gesture, window.open is ignored.
        static void MapJavaScriptCanOpenWindowsAutomatically(AdvancedWebViewHandler handler, AdvancedWebView view) { }

        // Browser argument, set before the first WebView2 is created (SetBrowserArguments).
        static void MapMediaPlaybackRequiresUserGesture(AdvancedWebViewHandler handler, AdvancedWebView view) { }

        // Not available in WebView2.
        static void MapScrollBouncesEnabled(AdvancedWebViewHandler handler, AdvancedWebView view) { }

        // Android-only settings.
        static void MapAllowMixedContent(AdvancedWebViewHandler handler, AdvancedWebView view) { }

        static void MapAllowFileAccess(AdvancedWebViewHandler handler, AdvancedWebView view) { }

        static void MapCacheMode(AdvancedWebViewHandler handler, AdvancedWebView view) { }

        // WebView2 enables DevTools (F12) by default: off unless IsInspectable, as on the other platforms.
        static void MapIsInspectable(AdvancedWebViewHandler handler, AdvancedWebView view)
        {
            if (handler.core is { } c)
                c.Settings.AreDevToolsEnabled = view.IsInspectable;
        }

        static void MapClearCache(AdvancedWebViewHandler handler, AdvancedWebView view, object? args)
        {
            if (handler.core is { } c)
                _ = c.Profile.ClearBrowsingDataAsync(CoreWebView2BrowsingDataKinds.DiskCache | CoreWebView2BrowsingDataKinds.CacheStorage);
        }

        internal partial void ExecuteScript(string script)
        {
            if (core is { } c)
                _ = RunScriptAsync(c, script);
        }

        async Task RunScriptAsync(CoreWebView2 target, string script)
        {
            try
            {
                await target.ExecuteScriptAsync(script);
            }
            catch (Exception ex)
            {
                // The page went away, or the web view was closed.
                Log($"Could not run a script in the page: {ex.Message}");
            }
        }

        void Log(string message) =>
            MauiContext?.Services.GetService<ILogger<AdvancedWebViewHandler>>()?.LogWarning("{Message}", message);

        /// <summary>
        /// The origin of a document URL; null for local content (MAUI's app host, about:, data:, file:), as on the
        /// other platforms.
        /// </summary>
        static Uri? ToOrigin(string? url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                uri.Scheme is not ("http" or "https") ||
                uri.Host.Length == 0 ||
                IsMauiLocalUrl(uri))
                return null;

            return new Uri(uri.GetLeftPart(UriPartial.Authority));
        }

        static bool IsMauiLocalUrl(Uri uri) =>
            uri.Scheme == "https" && string.Equals(uri.Host, MauiLocalHost, StringComparison.OrdinalIgnoreCase);

        static bool IsMauiLocalUrl(string? url) =>
            Uri.TryCreate(url, UriKind.Absolute, out var uri) && IsMauiLocalUrl(uri);
    }
}
