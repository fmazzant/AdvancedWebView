using System.Net;
using Foundation;
using WebKit;

namespace Mafesoft.Maui.Controls.Handlers
{
    // iOS: downloads. The navigation is cancelled and the file is downloaded with HttpClient (DownloadRequestedEventArgs.SaveToAsync),
    // with the cookies of WKHTTPCookieStore, so it works like on Android and HttpOnly session cookies are sent.
    public partial class AdvancedWebViewHandler
    {
        /// <summary>A link with the download attribute (also blob: and data:). iOS 14.5 or later.</summary>
        bool TryStartDownload(WKNavigationAction action)
        {
            if (!OperatingSystem.IsIOSVersionAtLeast(14, 5) || !action.ShouldPerformDownload || action.Request.Url?.AbsoluteString is not { } url)
                return false;

            StartDownload(url, suggestedFileName: null, mimeType: null, contentLength: null);
            return true;
        }

        /// <summary>A response WebKit cannot show, or a Content-Disposition: attachment.</summary>
        bool TryStartDownload(WKNavigationResponse navigationResponse)
        {
            var response = navigationResponse.Response;
            var attachment = response is NSHttpUrlResponse http &&
                (http.AllHeaderFields?["Content-Disposition"] as NSString)?.ToString().TrimStart().StartsWith("attachment", StringComparison.OrdinalIgnoreCase) == true;

            if ((navigationResponse.CanShowMimeType && !attachment) || response.Url?.AbsoluteString is not { } url)
                return false;

            StartDownload(url, response.SuggestedFilename, response.MimeType, response.ExpectedContentLength);
            return true;
        }

        void StartDownload(string url, string? suggestedFileName, string? mimeType, long? contentLength)
        {
            if (Advanced is not { } view)
                return;

            // DownloadBehavior.SystemDownloadManager has no iOS equivalent: the event is all there is.
            view.OnDownloadRequested(url, suggestedFileName, mimeType, contentLength,
                new DownloadSource(view, GetCookieHeaderAsync, GetUserAgentAsync, PlatformView?.Url?.AbsoluteString));
        }

        Task<string?> GetCookieHeaderAsync(string url) => MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (PlatformView?.Configuration.WebsiteDataStore.HttpCookieStore is not { } store || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return null;

            // CookieContainer applies the domain, path and secure rules for this URL.
            var container = new CookieContainer();
            foreach (var cookie in await store.GetAllCookiesAsync())
            {
                try
                {
                    container.Add(new Cookie(cookie.Name, cookie.Value, cookie.Path, cookie.Domain) { Secure = cookie.IsSecure });
                }
                catch (CookieException)
                {
                    // A value .NET does not accept (for example with a comma): skipped.
                }
            }

            var header = container.GetCookieHeader(uri);
            return string.IsNullOrEmpty(header) ? null : header;
        });

        Task<string?> GetUserAgentAsync() => MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (PlatformView is not { } webView)
                return null;
            if (!string.IsNullOrEmpty(webView.CustomUserAgent))
                return webView.CustomUserAgent;
            return (await webView.EvaluateJavaScriptAsync("navigator.userAgent")) is NSString agent ? agent.ToString() : null;
        });
    }
}
