using System.Net;
using Android.Webkit;

namespace Mafesoft.Maui.Controls.Handlers
{
    // Android: HTTP authentication (#54). WebViewClient.OnReceivedHttpAuthRequest reports host and realm only: no
    // scheme, no proxy flag, no failure count (counted here per host and realm until a page finishes loading).
    public partial class AdvancedWebViewHandler
    {
        readonly HttpAuthenticationAttempts httpAuthenticationAttempts = new();

        async void AnswerHttpAuthentication(AdvancedWebView view, HttpAuthHandler authHandler, string host, string? realm)
        {
            NetworkCredential? credentials = null;
            try
            {
                credentials = await view.RequestHttpAuthenticationAsync(
                    host, realm, HttpAuthenticationScheme.Unknown, isProxy: false, httpAuthenticationAttempts.Next(host, realm));
            }
            catch (Exception ex)
            {
                Log($"HTTP authentication failed: {ex.Message}");
            }

            if (credentials is null)
                authHandler.Cancel();
            else
                authHandler.Proceed(credentials.UserName, credentials.Password);
        }
    }
}
