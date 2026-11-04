using CoreGraphics;
using Foundation;
using Microsoft.Maui.Platform;
using WebKit;

namespace Mafesoft.Maui.Controls.Handlers
{
    public partial class AdvancedWebViewHandler
    {
        /// <summary>
        /// Creates the native view. On iOS some settings live in <see cref="WKWebViewConfiguration"/>,
        /// which WebKit copies when the view is created, so they are applied here.
        /// </summary>
        protected override WKWebView CreatePlatformView()
        {
            var config = MauiWKWebView.CreateConfiguration();

            if (VirtualView is AdvancedWebView view)
            {
                config.DefaultWebpagePreferences ??= new WKWebpagePreferences();
                config.DefaultWebpagePreferences.AllowsContentJavaScript = view.JavaScriptEnabled;
                config.Preferences.JavaScriptCanOpenWindowsAutomatically = view.JavaScriptCanOpenWindowsAutomatically;
                config.AllowsInlineMediaPlayback = view.AllowsInlineMediaPlayback;
                config.MediaTypesRequiringUserActionForPlayback = view.MediaPlaybackRequiresUserGesture
                    ? WKAudiovisualMediaTypes.All
                    : WKAudiovisualMediaTypes.None;
            }

            SetUpBridge(config);

            var webView = new MauiWKWebView(CGRect.Empty, this, config);

            // Replace the delegate MauiWKWebView creates with our subclass, which handles target="_blank" links.
            webView.NavigationDelegate = new AdvancedNavigationDelegate(this);
            return webView;
        }

        // Applied in CreatePlatformView: WebKit does not pick up configuration changes after creation.
        static void MapJavaScriptEnabled(AdvancedWebViewHandler handler, AdvancedWebView view) { }

        static void MapJavaScriptCanOpenWindowsAutomatically(AdvancedWebViewHandler handler, AdvancedWebView view) { }

        static void MapMediaPlaybackRequiresUserGesture(AdvancedWebViewHandler handler, AdvancedWebView view) { }

        static void MapScrollBouncesEnabled(AdvancedWebViewHandler handler, AdvancedWebView view) =>
            handler.PlatformView.ScrollView.Bounces = view.ScrollBouncesEnabled;

        // Android-only settings.
        static void MapAllowMixedContent(AdvancedWebViewHandler handler, AdvancedWebView view) { }

        static void MapAllowFileAccess(AdvancedWebViewHandler handler, AdvancedWebView view) { }

        static void MapCacheMode(AdvancedWebViewHandler handler, AdvancedWebView view) { }

        // WKWebView.inspectable exists from iOS 16.4; earlier versions allow inspecting debug builds only.
        static void MapIsInspectable(AdvancedWebViewHandler handler, AdvancedWebView view)
        {
            if (OperatingSystem.IsIOSVersionAtLeast(16, 4))
                handler.PlatformView.Inspectable = view.IsInspectable;
        }

        static void MapClearCache(AdvancedWebViewHandler handler, AdvancedWebView view, object? args)
        {
            var dataTypes = new NSSet<NSString>(WKWebsiteDataType.DiskCache, WKWebsiteDataType.MemoryCache);
            handler.PlatformView.Configuration.WebsiteDataStore.RemoveDataOfTypes(dataTypes, NSDate.DistantPast, () => { });
        }
    }
}
