using Android.Webkit;
using AndroidOverScrollMode = Android.Views.OverScrollMode;

namespace Mafesoft.Maui.Controls.Handlers
{
    public partial class AdvancedWebViewHandler
    {
        static void MapJavaScriptEnabled(AdvancedWebViewHandler handler, AdvancedWebView view) =>
            handler.PlatformView.Settings.JavaScriptEnabled = view.JavaScriptEnabled;

        static void MapJavaScriptCanOpenWindowsAutomatically(AdvancedWebViewHandler handler, AdvancedWebView view) =>
            handler.PlatformView.Settings.JavaScriptCanOpenWindowsAutomatically = view.JavaScriptCanOpenWindowsAutomatically;

        static void MapScrollBouncesEnabled(AdvancedWebViewHandler handler, AdvancedWebView view) =>
            handler.PlatformView.OverScrollMode = view.ScrollBouncesEnabled
                ? AndroidOverScrollMode.Always
                : AndroidOverScrollMode.Never;

        static void MapMediaPlaybackRequiresUserGesture(AdvancedWebViewHandler handler, AdvancedWebView view) =>
            handler.PlatformView.Settings.MediaPlaybackRequiresUserGesture = view.MediaPlaybackRequiresUserGesture;

        static void MapAllowMixedContent(AdvancedWebViewHandler handler, AdvancedWebView view) =>
            handler.PlatformView.Settings.MixedContentMode = view.AllowMixedContent
                ? MixedContentHandling.AlwaysAllow
                : MixedContentHandling.NeverAllow;

        static void MapAllowFileAccess(AdvancedWebViewHandler handler, AdvancedWebView view)
        {
            var settings = handler.PlatformView.Settings;
            settings.AllowFileAccess = view.AllowFileAccess;
            settings.AllowContentAccess = view.AllowFileAccess;
        }

        static void MapCacheMode(AdvancedWebViewHandler handler, AdvancedWebView view) =>
            handler.PlatformView.Settings.CacheMode = view.CacheMode switch
            {
                WebViewCacheMode.NoCache => CacheModes.NoCache,
                WebViewCacheMode.CacheOnly => CacheModes.CacheOnly,
                WebViewCacheMode.CacheElseNetwork => CacheModes.CacheElseNetwork,
                _ => CacheModes.Default,
            };

        static void MapClearCache(AdvancedWebViewHandler handler, AdvancedWebView view, object? args) =>
            handler.PlatformView.ClearCache(true);

        // WebView.SetWebContentsDebuggingEnabled is process-wide: keep it on while any connected view asks for it.
        static int inspectableViews;
        bool inspectableRegistered;

        static void MapIsInspectable(AdvancedWebViewHandler handler, AdvancedWebView view) =>
            handler.SetInspectable(view.IsInspectable);

        void SetInspectable(bool inspectable)
        {
            if (inspectable == inspectableRegistered)
                return;

            inspectableRegistered = inspectable;
            inspectableViews += inspectable ? 1 : -1; // mapper and disconnect run on the main thread
            Android.Webkit.WebView.SetWebContentsDebuggingEnabled(inspectableViews > 0);
        }
    }
}
