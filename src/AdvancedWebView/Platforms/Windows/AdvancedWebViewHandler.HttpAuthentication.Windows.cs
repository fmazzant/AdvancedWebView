using Microsoft.Web.WebView2.Core;

namespace Mafesoft.Maui.Controls.Handlers
{
    // Windows: HTTP authentication (#54). WebView2 raises BasicAuthenticationRequested with the WWW-Authenticate value
    // (Basic, Digest and NTLM were tested); without a HttpAuthenticationRequested handler it keeps its own login dialog.
    // No failure count: counted here per host and realm. Every refused attempt completes a failed navigation (the 401),
    // so the count is reset only by a successful navigation or by a navigation to another URL.
    public partial class AdvancedWebViewHandler
    {
        readonly HttpAuthenticationAttempts httpAuthenticationAttempts = new();
        string? httpAuthenticationUrl;

        void SetUpHttpAuthentication(CoreWebView2 target)
        {
            target.BasicAuthenticationRequested += OnBasicAuthenticationRequested;
            target.NavigationStarting += OnHttpAuthenticationNavigationStarting;
            target.NavigationCompleted += OnHttpAuthenticationNavigationCompleted;
        }

        void TearDownHttpAuthentication(CoreWebView2 target)
        {
            target.BasicAuthenticationRequested -= OnBasicAuthenticationRequested;
            target.NavigationStarting -= OnHttpAuthenticationNavigationStarting;
            target.NavigationCompleted -= OnHttpAuthenticationNavigationCompleted;
            httpAuthenticationAttempts.Reset();
        }

        void OnHttpAuthenticationNavigationStarting(CoreWebView2 sender, CoreWebView2NavigationStartingEventArgs args)
        {
            if (args.Uri != httpAuthenticationUrl)
                httpAuthenticationAttempts.Reset();
        }

        void OnHttpAuthenticationNavigationCompleted(CoreWebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            if (args.IsSuccess)
                httpAuthenticationAttempts.Reset();
        }

        async void OnBasicAuthenticationRequested(CoreWebView2 sender, CoreWebView2BasicAuthenticationRequestedEventArgs args)
        {
            if (Advanced is not { HasHttpAuthenticationHandler: true } view)
                return;

            var (scheme, realm) = HttpAuthenticationChallenge.Parse(args.Challenge);
            var host = Uri.TryCreate(args.Uri, UriKind.Absolute, out var uri) ? uri.Host : args.Uri;
            httpAuthenticationUrl = args.Uri;

            var deferral = args.GetDeferral();
            try
            {
                var credentials = await view.RequestHttpAuthenticationAsync(
                    host, realm, scheme, isProxy: false, httpAuthenticationAttempts.Next(host, realm));
                if (credentials is null)
                {
                    args.Cancel = true;
                }
                else
                {
                    args.Response.UserName = credentials.UserName;
                    args.Response.Password = credentials.Password;
                }
            }
            catch (Exception ex)
            {
                Log($"HTTP authentication failed: {ex.Message}");
                args.Cancel = true;
            }
            finally
            {
                deferral.Complete();
            }
        }
    }
}
