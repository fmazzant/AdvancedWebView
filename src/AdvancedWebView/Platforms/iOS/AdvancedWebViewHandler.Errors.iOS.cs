using Foundation;
using Security;
using WebKit;

namespace Mafesoft.Maui.Controls.Handlers
{
    // iOS: NavigationFailed, the custom error page and certificate errors.
    public partial class AdvancedWebViewHandler
    {
        // Cancelling a response to show the error page makes WebKit fail the navigation with "frame load interrupted":
        // that failure is ours, not the page's.
        bool ignoreNextInterruptedLoad;

        /// <summary>Raises NavigationFailed for the top-level page. Returns the error page to show, or null.</summary>
        string? ErrorPageFor(string? url, WebNavigationError error, int? httpStatusCode, string? description)
        {
            if (Advanced is not { } view || !AdvancedWebView.IsReportableUrl(url) || url!.StartsWith(ErrorPageBaseUrl.AbsoluteString!, StringComparison.Ordinal))
                return null;

            return view.OnNavigationFailed(url, error, httpStatusCode, description);
        }

        // Same kind of base URL as MAUI's HtmlWebViewSource: the error page is local content and can use the bridge (retry()).
        static NSUrl ErrorPageBaseUrl => new(NSBundle.MainBundle.BundlePath, isDir: true);

        void ReportFailure(WKWebView webView, NSError error)
        {
            if (error.Domain == "WebKitErrorDomain" && error.Code == 102 && ignoreNextInterruptedLoad)
            {
                ignoreNextInterruptedLoad = false;
                return;
            }

            // Provisional failures happen before the page changes: webView.Url is still the previous page.
            var url = (error.UserInfo?["NSErrorFailingURLStringKey"] as NSString)?.ToString()
                ?? (error.UserInfo?["NSErrorFailingURLKey"] as NSUrl)?.AbsoluteString
                ?? webView.Url?.AbsoluteString;

            if (ErrorPageFor(url, NavigationErrors.FromNSError(error.Domain, error.Code), null, error.LocalizedDescription) is { } html)
                webView.LoadHtmlString(html, ErrorPageBaseUrl);
        }

        sealed partial class AdvancedNavigationDelegate
        {
            AdvancedWebViewHandler? Owner => handler.TryGetTarget(out var h) ? h : null;

            public override void DidFailProvisionalNavigation(WKWebView webView, WKNavigation navigation, NSError error)
            {
                base.DidFailProvisionalNavigation(webView, navigation, error); // MAUI: Navigated with a failure result
                Owner?.ReportFailure(webView, error);
            }

            public override void DidFailNavigation(WKWebView webView, WKNavigation navigation, NSError error)
            {
                base.DidFailNavigation(webView, navigation, error);
                Owner?.ReportFailure(webView, error);
            }

            // HTTP 4xx/5xx of the top-level page. Not implemented by MAUI.
            [Export("webView:decidePolicyForNavigationResponse:decisionHandler:")]
            public void DecidePolicy(WKWebView webView, WKNavigationResponse navigationResponse, Action<WKNavigationResponsePolicy> decisionHandler)
            {
                // The event is raised before deciding: a handler that keeps the server's page lets the response through.
                if (navigationResponse.IsForMainFrame &&
                    navigationResponse.Response is NSHttpUrlResponse { StatusCode: >= 400 } response &&
                    Owner is { } owner &&
                    owner.ErrorPageFor(response.Url?.AbsoluteString, WebNavigationError.HttpError, (int)response.StatusCode,
                        NSHttpUrlResponse.LocalizedStringForStatusCode(response.StatusCode)) is { } html)
                {
                    owner.ignoreNextInterruptedLoad = true;
                    decisionHandler(WKNavigationResponsePolicy.Cancel);
                    webView.LoadHtmlString(html, ErrorPageBaseUrl);
                    return;
                }

                decisionHandler(WKNavigationResponsePolicy.Allow);
            }

            // Invalid server certificates. Not implemented by MAUI. Other challenges (HTTP auth) keep the default handling.
            [Export("webView:didReceiveAuthenticationChallenge:completionHandler:")]
            public void DidReceiveAuthenticationChallenge(WKWebView webView, NSUrlAuthenticationChallenge challenge, Action<NSUrlSessionAuthChallengeDisposition, NSUrlCredential?> completionHandler)
            {
                var space = challenge.ProtectionSpace;
                if (space.AuthenticationMethod != NSUrlProtectionSpace.AuthenticationMethodServerTrust ||
                    space.ServerSecTrust is not { } trust ||
                    trust.Evaluate(out NSError? trustError) ||
                    Owner?.Advanced is not { } view)
                {
                    completionHandler(NSUrlSessionAuthChallengeDisposition.PerformDefaultHandling, null);
                    return;
                }

                var url = space.Port is > 0 and not 443 ? $"https://{space.Host}:{space.Port}/" : $"https://{space.Host}/";
                var description = trustError?.LocalizedDescription ?? "Invalid certificate";

                if (view.OnServerCertificateError(url, description))
                    completionHandler(NSUrlSessionAuthChallengeDisposition.UseCredential, new NSUrlCredential(trust));
                else
                    // The system rejects the certificate: the load fails with an NSURLErrorDomain certificate error (SslError).
                    completionHandler(NSUrlSessionAuthChallengeDisposition.PerformDefaultHandling, null);
            }
        }
    }
}
