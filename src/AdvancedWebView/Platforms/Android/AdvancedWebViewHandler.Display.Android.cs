using AndroidX.WebKit;
using AColor = Android.Graphics.Color;

namespace Mafesoft.Maui.Controls.Handlers
{
    // Android: display options.
    public partial class AdvancedWebViewHandler
    {
        // Pinch zoom through the built-in zoom, without the on-screen +/- buttons.
        static void MapZoomEnabled(AdvancedWebViewHandler handler, AdvancedWebView view)
        {
            var settings = handler.PlatformView.Settings;
            settings.SetSupportZoom(view.ZoomEnabled);
            settings.BuiltInZoomControls = view.ZoomEnabled;
            settings.DisplayZoomControls = false;
        }

        static void MapTextZoom(AdvancedWebViewHandler handler, AdvancedWebView view) =>
            handler.PlatformView.Settings.TextZoom = view.TextZoom;

        static void MapIsTransparent(AdvancedWebViewHandler handler, AdvancedWebView view)
        {
            if (view.IsTransparent)
                handler.PlatformView.SetBackgroundColor(AColor.Transparent);
            else if (view.Background is null && view.BackgroundColor is null)
                handler.PlatformView.SetBackgroundColor(AColor.White); // the WebView's own default
            else
                handler.UpdateValue(nameof(IView.Background));
        }

        // The web view follows the app's theme (prefers-color-scheme): nothing to set per view.
        static void MapPreferredColorScheme(AdvancedWebViewHandler handler, AdvancedWebView view) { }

        static void MapAlgorithmicDarkening(AdvancedWebViewHandler handler, AdvancedWebView view)
        {
            if (WebViewFeature.IsFeatureSupported(WebViewFeature.AlgorithmicDarkening))
                WebSettingsCompat.SetAlgorithmicDarkeningAllowed(handler.PlatformView.Settings, view.AlgorithmicDarkening);
            else if (view.AlgorithmicDarkening)
                handler.Log("This Android System WebView does not support algorithmic darkening. Update Android System WebView.");
        }

        // iOS only.
        static void MapLinkPreviewEnabled(AdvancedWebViewHandler handler, AdvancedWebView view) { }
    }
}
