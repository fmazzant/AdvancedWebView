using Foundation;
using Microsoft.Extensions.Logging;
using WebKit;

namespace Mafesoft.Maui.Controls.Handlers
{
    // iOS: LoadUrl / PostUrlAsync. LoadRequest goes through DecidePolicy, where MAUI raises Navigating
    // and the external-link rules apply, as for a link.
    public partial class AdvancedWebViewHandler
    {
        internal partial void LoadRequest(WebRequest request)
        {
            if (PlatformView is not { } webView || NSUrl.FromString(request.Url) is not { } url)
                return;

            var nativeRequest = new NSMutableUrlRequest(url);
            var headers = new NSMutableDictionary();
            foreach (var (name, value) in request.Headers)
                headers[new NSString(name)] = new NSString(value);

            if (request.Body is { } body)
            {
                nativeRequest.HttpMethod = "POST";
                nativeRequest.Body = NSData.FromArray(body);
                if (request.ContentType is { } contentType)
                    headers[new NSString("Content-Type")] = new NSString(contentType);
            }

            nativeRequest.Headers = headers;
            webView.LoadRequest(nativeRequest);
        }

        internal partial string? GetCurrentPageUrl() => PlatformView?.Url?.AbsoluteString;

        void Log(string message) =>
            MauiContext?.Services.GetService<ILogger<AdvancedWebViewHandler>>()?.LogWarning("{Message}", message);

        // URL last loaded again without leaked headers: if WebKit still sends them, the load is stopped instead of
        // being retried forever.
        string? lastCleanedUrl;

        /// <summary>
        /// What to do with a top-level request that carries LoadUrl headers which must not go to its origin:
        /// <c>(true, request)</c> to cancel it and load <c>request</c> (the same request without them), <c>(true, null)</c>
        /// to only cancel it, <c>(false, null)</c> when it carries none. WebKit removes <c>Authorization</c> on a
        /// cross-origin redirect, but keeps the other headers. The new request keeps the method and body (a 307
        /// redirect of PostUrlAsync). It goes through DecidePolicy again, so Navigating is raised a second time.
        /// </summary>
        (bool Cancel, NSUrlRequest? Retry) CheckLeakedHeaders(NSUrlRequest request)
        {
            var url = request.Url?.AbsoluteString;
            if (request.Headers is not { Count: > 0 } headers || request.Url is not { } nsUrl)
            {
                lastCleanedUrl = null;
                return (false, null);
            }

            var pairs = new List<KeyValuePair<string, string>>();
            foreach (var (key, value) in headers)
                pairs.Add(new(key.ToString(), value?.ToString() ?? string.Empty));

            var leaked = RequestHeaders.FindLeakedHeaders(url, pairs);
            if (leaked.Count == 0)
            {
                lastCleanedUrl = null;
                return (false, null);
            }

            if (lastCleanedUrl == url)
            {
                Log($"The headers of LoadUrl could not be removed from the request for {url}: the load was stopped.");
                lastCleanedUrl = null;
                return (true, null);
            }

            // A new request, not a mutable copy: setting Headers on a copy merges them with the old ones.
            var clean = new NSMutableUrlRequest(nsUrl, request.CachePolicy, request.TimeoutInterval)
            {
                HttpMethod = request.HttpMethod,
                MainDocumentURL = request.MainDocumentURL,
            };
            if (request.Body is { } body)
                clean.Body = body;

            var kept = new NSMutableDictionary();
            foreach (var (key, value) in headers)
            {
                if (!leaked.Contains(key.ToString(), StringComparer.OrdinalIgnoreCase))
                    kept[key] = value;
            }
            clean.Headers = kept;

            lastCleanedUrl = url;
            return (true, clean);
        }

        // Other parts: AdvancedWebViewHandler.NewWindow.iOS.cs and AdvancedWebViewHandler.Errors.iOS.cs.
        sealed partial class AdvancedNavigationDelegate
        {
            // A new top-level document was committed, after any redirect: tells RequestHeaders which page has headers.
            [Export("webView:didCommitNavigation:")]
            public void DidCommitNavigation(WKWebView webView, WKNavigation navigation)
            {
                if (handler.TryGetTarget(out var owner))
                    owner.RequestHeaders.OnCommitted(webView.Url?.AbsoluteString);
            }
        }
    }
}
