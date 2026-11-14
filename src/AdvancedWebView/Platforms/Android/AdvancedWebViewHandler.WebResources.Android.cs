using Android.Webkit;
using AWebResourceResponse = Android.Webkit.WebResourceResponse;

namespace Mafesoft.Maui.Controls.Handlers
{
    // Android: custom schemes and WebResourceRequested (#55) through WebViewClient.ShouldInterceptRequest, which sees
    // every request of the page (http, https and custom schemes) on a background thread of the web view. The thread
    // waits for the handler and its deferrals.
    public partial class AdvancedWebViewHandler
    {
        /// <summary>The response for <paramref name="request"/>, or null to let the web view load it.</summary>
        AWebResourceResponse? InterceptRequest(IWebResourceRequest request)
        {
            if (Advanced is not { } view || request.Url?.ToString() is not { } url)
                return null;

            var isCustomScheme = view.IsCustomSchemeUrl(url);
            if (!isCustomScheme && !view.HasWebResourceRequestedHandler)
                return null;

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (request.RequestHeaders is { } requestHeaders)
            {
                foreach (var (name, value) in requestHeaders)
                    headers[name] = value;
            }

            WebResourceResponse? response = null;
            try
            {
                var type = request.IsForMainFrame ? WebResourceType.Document : WebResourceType.Unknown;
                var e = view.RequestWebResourceAsync(url, request.Method ?? "GET", headers, type).GetAwaiter().GetResult();
                response = e?.Response;
            }
            catch (Exception ex)
            {
                Log($"WebResourceRequested failed for {url}: {ex.Message}");
            }

            if (response is null && isCustomScheme)
                response = AdvancedWebView.CustomSchemeNotFound(); // nothing else can load it

            return response is null ? null : ToAndroidResponse(response);
        }

        static AWebResourceResponse ToAndroidResponse(WebResourceResponse response)
        {
            var (mimeType, charset) = WebResourceRules.SplitContentType(response.ContentType);
            var headers = WebResourceRules.BuildHeaders(response);
            headers.Remove("Content-Type"); // given as mimeType and encoding
            // The binding wraps the .NET stream in a Java InputStream, which the web view reads and closes. An empty body
            // rather than none: a null stream can be taken as "not found".
            return new AWebResourceResponse(mimeType, charset, response.StatusCode, response.ReasonPhrase, headers, response.Content ?? new MemoryStream());
        }
    }
}
