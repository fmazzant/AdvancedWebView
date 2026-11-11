using Foundation;
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

        /// <summary>
        /// A copy of <paramref name="request"/> without the LoadUrl headers that must not go to its origin, or null when
        /// it carries none. WebKit removes <c>Authorization</c> on a cross-origin redirect, but keeps the other headers.
        /// The copy keeps the method and body (a 307 redirect of PostUrlAsync). It goes through DecidePolicy again,
        /// so Navigating is raised a second time for the same URL.
        /// </summary>
        NSUrlRequest? WithoutLeakedHeaders(NSUrlRequest request)
        {
            if (request.Headers is not { Count: > 0 } headers)
                return null;

            var pairs = new List<KeyValuePair<string, string>>();
            foreach (var (key, value) in headers)
                pairs.Add(new(key.ToString(), value?.ToString() ?? string.Empty));

            var leaked = RequestHeaders.FindLeakedHeaders(request.Url?.AbsoluteString, pairs);
            if (leaked.Count == 0)
                return null;

            var clean = (NSMutableUrlRequest)request.MutableCopy();
            var kept = new NSMutableDictionary();
            foreach (var (key, value) in headers)
            {
                if (!leaked.Contains(key.ToString(), StringComparer.OrdinalIgnoreCase))
                    kept[key] = value;
            }
            clean.Headers = kept;
            return clean;
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
