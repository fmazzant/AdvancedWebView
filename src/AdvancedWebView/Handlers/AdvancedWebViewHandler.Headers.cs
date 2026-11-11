using Microsoft.Maui.Handlers;

namespace Mafesoft.Maui.Controls.Handlers
{
    // LoadUrl / PostUrlAsync headers (#60): sent only to the origin of the URL they were given with, and Reload()
    // behaves the same on every platform. The platform files call RequestHeaders where their engine reports
    // a commit, a redirect or the headers of a request.
    public partial class AdvancedWebViewHandler
    {
        internal RequestHeaderState RequestHeaders { get; } = new();

        /// <summary>Loads a request from LoadUrl/PostUrlAsync. Must be called on the main thread.</summary>
        internal void Load(WebRequest request)
        {
            RequestHeaders.BeginRequest(request.Url, request.Headers, request.Body is not null);
            LoadRequest(request);
        }

        // Reload(): when the page was loaded by LoadUrl and is still on the origin of its headers, the request is sent
        // again with them. The engines differ otherwise: WebView2 drops them on the first page of the view, Android
        // after a redirect. Loading the current URL again replaces the history entry, as a reload does.
        static void MapReload(AdvancedWebViewHandler handler, AdvancedWebView view, object? args)
        {
            var url = handler.GetCurrentPageUrl();
            if (url is not null && handler.RequestHeaders.GetReloadHeaders(url) is { } headers)
                handler.Load(new WebRequest(url, headers));
            else
                WebViewHandler.MapReload(handler, view, args);
        }

        /// <summary>The URL of the top-level page, from the platform view.</summary>
        internal partial string? GetCurrentPageUrl();
    }
}
