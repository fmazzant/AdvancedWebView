using UIKit;

namespace Mafesoft.Maui.Controls.Handlers
{
    // iOS: display options.
    public partial class AdvancedWebViewHandler
    {
        // WKWebView has no zoom switch: the viewport's user-scalable=no, added by the display script, turns pinch zoom off.
        static void MapZoomEnabled(AdvancedWebViewHandler handler, AdvancedWebView view) => MapDisplayScript(handler, view);

        static void MapTextZoom(AdvancedWebViewHandler handler, AdvancedWebView view)
        {
            if (OperatingSystem.IsIOSVersionAtLeast(14))
                handler.PlatformView.PageZoom = view.TextZoom / 100f;
        }

        // MauiWKWebView already has a clear background color; an opaque WKWebView still paints white behind the page.
        static void MapIsTransparent(AdvancedWebViewHandler handler, AdvancedWebView view)
        {
            handler.PlatformView.Opaque = !view.IsTransparent;
            if (view.IsTransparent)
                handler.PlatformView.ScrollView.BackgroundColor = UIColor.Clear;
        }

        static void MapPreferredColorScheme(AdvancedWebViewHandler handler, AdvancedWebView view) =>
            handler.PlatformView.OverrideUserInterfaceStyle = view.PreferredColorScheme switch
            {
                WebViewColorScheme.Light => UIUserInterfaceStyle.Light,
                WebViewColorScheme.Dark => UIUserInterfaceStyle.Dark,
                _ => UIUserInterfaceStyle.Unspecified,
            };

        // Android only.
        static void MapAlgorithmicDarkening(AdvancedWebViewHandler handler, AdvancedWebView view) { }

        static void MapLinkPreviewEnabled(AdvancedWebViewHandler handler, AdvancedWebView view) =>
            handler.PlatformView.AllowsLinkPreview = view.LinkPreviewEnabled;
    }
}
