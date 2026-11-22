using Foundation;
using Security;
using WebKit;

namespace Mafesoft.Maui.Controls.Handlers
{
    // iOS / Mac Catalyst: client certificate authentication (#58), from the navigation delegate's authentication
    // challenge (AdvancedWebViewHandler.Errors.iOS.cs). The X509Certificate2 is imported as a SecIdentity (through
    // PKCS#12) and sent with the intermediates as an NSUrlCredential for the session.
    public partial class AdvancedWebViewHandler
    {
        static bool IsClientCertificate(string? method) =>
            method == NSUrlProtectionSpace.AuthenticationMethodClientCertificate;

        async void AnswerClientCertificate(AdvancedWebView view, NSUrlAuthenticationChallenge challenge,
            Action<NSUrlSessionAuthChallengeDisposition, NSUrlCredential?> completionHandler)
        {
            var space = challenge.ProtectionSpace;
            NSUrlCredential? credential = null;
            try
            {
                var issuers = ClientCertificates.IssuerNames(space.DistinguishedNames?.Select(name => name.ToArray()));
                var answer = await view.RequestClientCertificateAsync(space.Host, (int)space.Port, issuers, []);
                if (answer?.Certificate is { } certificate)
                    credential = CreateClientCredential(view, certificate, answer.IntermediateCertificates);
            }
            catch (Exception ex)
            {
                view.LogWarning($"Client certificate request failed: {ex.Message}");
            }

            if (credential is null)
                // As without a handler: WebKit continues without a certificate.
                completionHandler(NSUrlSessionAuthChallengeDisposition.PerformDefaultHandling, null);
            else
                completionHandler(NSUrlSessionAuthChallengeDisposition.UseCredential, credential);
        }

        static NSUrlCredential? CreateClientCredential(AdvancedWebView view,
            System.Security.Cryptography.X509Certificates.X509Certificate2 certificate,
            System.Security.Cryptography.X509Certificates.X509Certificate2Collection intermediates)
        {
            if (!certificate.HasPrivateKey)
            {
                view.LogWarning("ClientCertificateRequested: the certificate has no private key; the request continues without a certificate.");
                return null;
            }

            try
            {
                var identity = SecIdentity.Import(certificate);
                var chain = intermediates.Select(item => new SecCertificate(item)).ToArray();
                return new NSUrlCredential(identity, chain, NSUrlCredentialPersistence.ForSession);
            }
            catch (Exception ex)
            {
                view.LogWarning($"ClientCertificateRequested: the certificate cannot be used ({ex.Message}); load it with " +
                    "X509KeyStorageFlags.Exportable. The request continues without a certificate.");
                return null;
            }
        }
    }
}
