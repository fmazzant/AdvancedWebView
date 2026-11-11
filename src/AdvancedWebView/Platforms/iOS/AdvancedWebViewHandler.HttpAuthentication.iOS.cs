using Foundation;
using WebKit;

namespace Mafesoft.Maui.Controls.Handlers
{
    // iOS / Mac Catalyst: HTTP authentication (#54), from the navigation delegate's authentication challenge
    // (AdvancedWebViewHandler.Errors.iOS.cs). The credentials are kept for the session.
    public partial class AdvancedWebViewHandler
    {
        static bool IsHttpAuthentication(string? method) =>
            method == NSUrlProtectionSpace.AuthenticationMethodHTTPBasic ||
            method == NSUrlProtectionSpace.AuthenticationMethodHTTPDigest ||
            method == NSUrlProtectionSpace.AuthenticationMethodNTLM ||
            method == NSUrlProtectionSpace.AuthenticationMethodNegotiate;

        async void AnswerHttpAuthentication(AdvancedWebView view, NSUrlAuthenticationChallenge challenge,
            Action<NSUrlSessionAuthChallengeDisposition, NSUrlCredential?> completionHandler)
        {
            var space = challenge.ProtectionSpace;
            System.Net.NetworkCredential? credentials = null;
            try
            {
                credentials = await view.RequestHttpAuthenticationAsync(
                    space.Host, space.Realm, HttpAuthenticationChallenge.ParseScheme(space.AuthenticationMethod),
                    space.IsProxy, (int)challenge.PreviousFailureCount);
            }
            catch (Exception ex)
            {
                view.LogWarning($"HTTP authentication failed: {ex.Message}");
            }

            if (credentials is null)
                // As without a handler: WebKit sends no credentials.
                completionHandler(NSUrlSessionAuthChallengeDisposition.PerformDefaultHandling, null);
            else
                completionHandler(NSUrlSessionAuthChallengeDisposition.UseCredential,
                    new NSUrlCredential(credentials.UserName, credentials.Password, NSUrlCredentialPersistence.ForSession));
        }
    }
}
