using Microsoft.Maui.Handlers;

namespace Mafesoft.Maui.Controls.Handlers
{
    /// <summary>
    /// Handler for <see cref="AdvancedWebView"/>. Extends the built-in MAUI <see cref="WebViewHandler"/>,
    /// so navigation events, cookies, user agent and JavaScript alerts keep working.
    /// </summary>
    public partial class AdvancedWebViewHandler : WebViewHandler
    {
        /// <summary>Property mapper: the MAUI WebView mappings plus the AdvancedWebView settings.</summary>
        public static new readonly IPropertyMapper<AdvancedWebView, AdvancedWebViewHandler> Mapper =
            new PropertyMapper<AdvancedWebView, AdvancedWebViewHandler>(WebViewHandler.Mapper)
            {
                [nameof(AdvancedWebView.JavaScriptEnabled)] = MapJavaScriptEnabled,
                [nameof(AdvancedWebView.JavaScriptCanOpenWindowsAutomatically)] = MapJavaScriptCanOpenWindowsAutomatically,
                [nameof(AdvancedWebView.ScrollBouncesEnabled)] = MapScrollBouncesEnabled,
                [nameof(AdvancedWebView.MediaPlaybackRequiresUserGesture)] = MapMediaPlaybackRequiresUserGesture,
                [nameof(AdvancedWebView.AllowMixedContent)] = MapAllowMixedContent,
                [nameof(AdvancedWebView.AllowFileAccess)] = MapAllowFileAccess,
                [nameof(AdvancedWebView.CacheMode)] = MapCacheMode,
#if ANDROID
                // Same keys as the MAUI mapper: our subclasses of MauiWebViewClient/MauiWebChromeClient replace MAUI's.
                [nameof(Android.Webkit.WebViewClient)] = MapAdvancedWebViewClient,
                [nameof(Android.Webkit.WebChromeClient)] = MapAdvancedWebChromeClient,
#endif
            };

        /// <summary>Command mapper: the MAUI WebView commands plus <see cref="AdvancedWebView.ClearCache"/>.</summary>
        public static new readonly CommandMapper<AdvancedWebView, AdvancedWebViewHandler> CommandMapper =
            new(WebViewHandler.CommandMapper)
            {
                [nameof(AdvancedWebView.ClearCache)] = MapClearCache,
            };

        /// <summary>Creates the handler with the default mappers.</summary>
        public AdvancedWebViewHandler() : base(Mapper, CommandMapper)
        {
        }

        /// <summary>Creates the handler with custom mappers.</summary>
        public AdvancedWebViewHandler(IPropertyMapper? mapper, CommandMapper? commandMapper)
            : base(mapper ?? Mapper, commandMapper ?? CommandMapper)
        {
        }

        /// <summary>Runs a script in the page without waiting for its result. Must be called on the main thread.</summary>
        internal partial void ExecuteScript(string script);

        /// <summary>Called after each page load (Navigated). Android uses it when document-start scripts are not supported.</summary>
        internal void OnPageLoaded() => OnPageLoadedCore();

        partial void OnPageLoadedCore();

        /// <summary>Re-applies <see cref="AdvancedWebView.UserScripts"/> and <see cref="AdvancedWebView.UserStyleSheets"/>.</summary>
        internal partial void UpdateUserScripts();

        AdvancedWebView? Advanced => VirtualView as AdvancedWebView;
    }
}
