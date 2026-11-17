using System.Security.Cryptography.X509Certificates;
using Microsoft.Web.WebView2.Core;

namespace Mafesoft.Maui.Controls.Handlers
{
    // Windows: client certificate authentication (#58). WebView2 raises ClientCertificateRequested with the certificates
    // of the Windows store that the server accepts (MutuallyTrustedCertificates); only one of them can be sent, so the
    // certificate the app picks is matched by thumbprint. Without a ClientCertificateRequested handler WebView2 keeps
    // its own certificate picker.
    public partial class AdvancedWebViewHandler
    {
        void SetUpClientCertificate(CoreWebView2 target) =>
            target.ClientCertificateRequested += OnClientCertificateRequested;

        void TearDownClientCertificate(CoreWebView2 target) =>
            target.ClientCertificateRequested -= OnClientCertificateRequested;

        async void OnClientCertificateRequested(CoreWebView2 sender, CoreWebView2ClientCertificateRequestedEventArgs args)
        {
            if (Advanced is not { HasClientCertificateHandler: true } view)
                return;

            var deferral = args.GetDeferral();
            try
            {
                var trusted = args.MutuallyTrustedCertificates.ToList();
                // Same index as trusted; null when the certificate cannot be read.
                var converted = trusted.Select(item => ClientCertificates.FromPem(item.ToPemEncoding())).ToList();
                var available = converted.OfType<X509Certificate2>().ToList();

                var answer = await view.RequestClientCertificateAsync(
                    args.Host, args.Port, args.AllowedCertificateAuthorities.ToList(), available);

                if (answer?.Certificate is { } certificate)
                {
                    var index = ClientCertificates.IndexOf(certificate, converted);
                    if (index >= 0)
                        args.SelectedCertificate = trusted[index];
                    else
                        view.LogWarning("ClientCertificateRequested: WebView2 can only send a certificate of the Windows " +
                            "certificate store that the server accepts (AvailableCertificates); the request continues without a certificate.");
                }

                // Handled with no SelectedCertificate continues without a certificate, without WebView2's picker.
                args.Handled = true;
            }
            catch (Exception ex)
            {
                Log($"Client certificate request failed: {ex.Message}");
                args.Handled = true;
            }
            finally
            {
                deferral.Complete();
            }
        }
    }
}
