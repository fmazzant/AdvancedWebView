using System.Security.Cryptography.X509Certificates;
using Android.Webkit;
using Java.Security;
using Java.Security.Spec;
using JavaCertificate = Java.Security.Cert.X509Certificate;
using JavaCertificateFactory = Java.Security.Cert.CertificateFactory;

namespace Mafesoft.Maui.Controls.Handlers
{
    // Android: client certificate authentication (#58). WebViewClient.OnReceivedClientCertRequest gives host, port and
    // the accepted issuers; ClientCertRequest.Proceed takes the private key and the chain as Java objects, rebuilt here
    // from the X509Certificate2 (PKCS#8 key, DER certificates). WebView remembers the answer for host and port until the
    // app restarts.
    public partial class AdvancedWebViewHandler
    {
        async void AnswerClientCertificate(AdvancedWebView view, ClientCertRequest request)
        {
            ClientCertificateRequestedEventArgs? answer = null;
            try
            {
                var issuers = request.GetPrincipals()?
                    .Select(principal => principal?.Name)
                    .OfType<string>()
                    .ToList() ?? [];
                answer = await view.RequestClientCertificateAsync(request.Host ?? string.Empty, request.Port, issuers, []);
            }
            catch (Exception ex)
            {
                Log($"Client certificate request failed: {ex.Message}");
            }

            if (answer?.Certificate is not { } certificate)
            {
                // Continue without a certificate; Ignore (unlike Cancel) does not remember the choice.
                request.Ignore();
                return;
            }

            try
            {
                if (ClientCertificates.ExportPrivateKey(certificate) is not { } key)
                {
                    view.LogWarning("ClientCertificateRequested: the certificate has no exportable private key (load it with " +
                        "X509KeyStorageFlags.Exportable); the request continues without a certificate.");
                    request.Ignore();
                    return;
                }

                var privateKey = KeyFactory.GetInstance(key.Algorithm)!.GeneratePrivate(new PKCS8EncodedKeySpec(key.Pkcs8));
                var factory = JavaCertificateFactory.GetInstance("X.509")!;
                var chain = new[] { certificate }
                    .Concat(answer.IntermediateCertificates)
                    .Select(item => (JavaCertificate)factory.GenerateCertificate(new MemoryStream(item.RawData))!)
                    .ToArray();
                request.Proceed(privateKey, chain);
            }
            catch (Exception ex)
            {
                view.LogWarning($"ClientCertificateRequested: the certificate cannot be used ({ex.Message}); the request continues without a certificate.");
                request.Ignore();
            }
        }
    }
}
