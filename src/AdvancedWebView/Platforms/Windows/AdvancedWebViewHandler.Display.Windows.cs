using Microsoft.Web.WebView2.Core;

namespace Mafesoft.Maui.Controls.Handlers
{
    // Windows: display options.
    public partial class AdvancedWebViewHandler
    {
        // Ctrl + mouse wheel, Ctrl +/- and the touch pinch.
        static void MapZoomEnabled(AdvancedWebViewHandler handler, AdvancedWebView view)
        {
            if (handler.core is not { } c)
                return;
            c.Settings.IsZoomControlEnabled = view.ZoomEnabled;
            c.Settings.IsPinchZoomEnabled = view.ZoomEnabled;
        }

        // The WinUI WebView2 does not expose the controller's ZoomFactor: the CSS zoom of the page scales the whole page,
        // as iOS does. Applied now and on every page load (DOMContentLoaded).
        static void MapTextZoom(AdvancedWebViewHandler handler, AdvancedWebView view)
        {
            if (handler.core is not null)
                handler.ExecuteScript(TextZoomScript(view.TextZoom));
        }

        void ApplyTextZoom()
        {
            if (Advanced is { TextZoom: not 100 } view)
                ExecuteScript(TextZoomScript(view.TextZoom));
        }

        static string TextZoomScript(int percent) =>
            "document.documentElement && (document.documentElement.style.zoom = " +
            (percent == 100 ? "\"\"" : "\"" + percent.ToString(System.Globalization.CultureInfo.InvariantCulture) + "%\"") + ");";

        static void MapIsTransparent(AdvancedWebViewHandler handler, AdvancedWebView view) =>
            handler.PlatformView.DefaultBackgroundColor = view.IsTransparent ? Microsoft.UI.Colors.Transparent : Microsoft.UI.Colors.White;

        // The color scheme belongs to the WebView2 profile: it is shared by the web views of the app.
        static void MapPreferredColorScheme(AdvancedWebViewHandler handler, AdvancedWebView view)
        {
            if (handler.core is not { } c)
                return;
            c.Profile.PreferredColorScheme = view.PreferredColorScheme switch
            {
                WebViewColorScheme.Light => CoreWebView2PreferredColorScheme.Light,
                WebViewColorScheme.Dark => CoreWebView2PreferredColorScheme.Dark,
                _ => CoreWebView2PreferredColorScheme.Auto,
            };
        }

        // Android only.
        static void MapAlgorithmicDarkening(AdvancedWebViewHandler handler, AdvancedWebView view) { }

        // iOS only.
        static void MapLinkPreviewEnabled(AdvancedWebViewHandler handler, AdvancedWebView view) { }
    }
}
