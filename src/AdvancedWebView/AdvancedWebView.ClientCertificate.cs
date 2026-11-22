using System.Security.Cryptography.X509Certificates;

namespace Mafesoft.Maui.Controls
{
    /// <summary>
    /// A server asks for a client certificate (mutual TLS). Set <see cref="Certificate"/> to answer with one, or leave
    /// it null to continue without. For an asynchronous answer (for example loading the certificate from secure
    /// storage), call <see cref="GetDeferral"/> before the first <c>await</c> and dispose it after.
    /// </summary>
    public sealed class ClientCertificateRequestedEventArgs : EventArgs
    {
        readonly EventDeferral deferral = new();

        internal ClientCertificateRequestedEventArgs(string host, int port, IReadOnlyList<string> acceptableIssuers,
            IReadOnlyList<X509Certificate2> availableCertificates)
        {
            Host = host;
            Port = port;
            AcceptableIssuers = acceptableIssuers;
            AvailableCertificates = availableCertificates;
        }

        /// <summary>The host that asks, for example <c>api.example.com</c>.</summary>
        public string Host { get; }

        /// <summary>The port of the connection, for example <c>443</c>; 0 when the platform does not report it.</summary>
        public int Port { get; }

        /// <summary>
        /// The distinguished names of the certificate authorities the server accepts (for example
        /// <c>CN=Example CA, O=Example</c>). Empty when the server names none: any certificate can be tried.
        /// </summary>
        public IReadOnlyList<string> AcceptableIssuers { get; }

        /// <summary>
        /// Windows only: the certificates of the Windows certificate store that the server accepts, without their private
        /// keys (the store keeps them). Pick one of them for <see cref="Certificate"/>. Empty on the other platforms.
        /// </summary>
        public IReadOnlyList<X509Certificate2> AvailableCertificates { get; }

        /// <summary>
        /// The certificate to send, with its private key, or null (the default) to continue without a certificate.
        /// </summary>
        /// <remarks>
        /// On Android, iOS and Mac Catalyst the private key is handed to the platform: load the certificate with
        /// <see cref="X509KeyStorageFlags.Exportable"/>, for example from a PKCS#12 file. WebView2 on Windows can only
        /// send a certificate of the Windows certificate store: use one of <see cref="AvailableCertificates"/> (or the
        /// same certificate loaded from the store); any other certificate is ignored with a warning in the log.
        /// </remarks>
        public X509Certificate2? Certificate { get; set; }

        /// <summary>
        /// Intermediate certificates sent after <see cref="Certificate"/>, when the server does not know the issuer of the
        /// client certificate. Android, iOS and Mac Catalyst only: Windows builds the chain from the certificate store.
        /// </summary>
        public X509Certificate2Collection IntermediateCertificates { get; } = [];

        /// <summary>
        /// Delays the answer until the returned object is disposed, so <see cref="Certificate"/> can be set after an <c>await</c>.
        /// </summary>
        /// <example>
        /// <code>
        /// using var deferral = e.GetDeferral();
        /// e.Certificate = await LoadCertificateAsync(e.Host);
        /// </code>
        /// </example>
        public IDisposable GetDeferral() => deferral.GetDeferral();

        /// <summary>Completes once the event handlers have returned and every deferral is disposed.</summary>
        internal Task WhenAnsweredAsync() => deferral.WhenDoneAsync();
    }

    // Client certificate authentication: mutual TLS (#58).
    public partial class AdvancedWebView
    {
        /// <summary>
        /// Raised on the main thread when a server asks for a client certificate (mutual TLS).
        /// Without a handler the platform's default applies: Android and iOS continue without a certificate, WebView2 on
        /// Windows lets the user pick one from the Windows certificate store.
        /// </summary>
        /// <remarks>
        /// The platforms remember the answer for the host and port while the app runs (Android and WebView2 until the
        /// app restarts, WKWebView for the TLS session), so the event is usually raised once per server.
        /// </remarks>
        public event EventHandler<ClientCertificateRequestedEventArgs>? ClientCertificateRequested;

        /// <summary>Whether a handler is attached: without one, the platform's default handling is kept.</summary>
        internal bool HasClientCertificateHandler => ClientCertificateRequested is not null;

        /// <summary>
        /// Raises <see cref="ClientCertificateRequested"/> and waits for its deferrals. Returns the answered arguments, or
        /// null when a handler failed (the request then continues without a certificate).
        /// </summary>
        internal async Task<ClientCertificateRequestedEventArgs?> RequestClientCertificateAsync(
            string host, int port, IReadOnlyList<string> acceptableIssuers, IReadOnlyList<X509Certificate2> availableCertificates)
        {
            var e = new ClientCertificateRequestedEventArgs(host, port, acceptableIssuers, availableCertificates);
            try
            {
                ClientCertificateRequested?.Invoke(this, e);
            }
            catch (Exception ex)
            {
                LogWarning($"ClientCertificateRequested handler failed, the request continues without a certificate: {ex.Message}");
                return null;
            }

            await e.WhenAnsweredAsync();
            return e;
        }
    }
}
