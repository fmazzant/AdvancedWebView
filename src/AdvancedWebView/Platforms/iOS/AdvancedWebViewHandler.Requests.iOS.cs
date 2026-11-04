using Foundation;

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
    }
}
