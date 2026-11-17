using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Mafesoft.Maui.Controls
{
    /// <summary>Conversions shared by the platforms for client certificate authentication (#58).</summary>
    internal static class ClientCertificates
    {
        /// <summary>
        /// Readable names (for example <c>CN=Example CA, O=Example</c>) of DER-encoded distinguished names, as iOS
        /// reports the certificate authorities a server accepts. Names that cannot be decoded are skipped.
        /// </summary>
        public static IReadOnlyList<string> IssuerNames(IEnumerable<byte[]>? derNames)
        {
            if (derNames is null)
                return [];

            var names = new List<string>();
            foreach (var der in derNames)
            {
                if (der is not { Length: > 0 })
                    continue;
                try
                {
                    // Some platforms decode invalid data as an empty name instead of throwing.
                    if (new X500DistinguishedName(der).Name is { Length: > 0 } name)
                        names.Add(name);
                }
                catch (CryptographicException)
                {
                    // Not a distinguished name: nothing useful to report.
                }
            }
            return names;
        }

        /// <summary>The certificate in a PEM block (<c>-----BEGIN CERTIFICATE-----</c>), or null if it cannot be read.</summary>
        public static X509Certificate2? FromPem(string? pem)
        {
            if (string.IsNullOrWhiteSpace(pem))
                return null;
            try
            {
                return X509Certificate2.CreateFromPem(pem);
            }
            catch (Exception ex) when (ex is CryptographicException or ArgumentException)
            {
                return null;
            }
        }

        /// <summary>
        /// The index of the candidate that is the same certificate as <paramref name="certificate"/> (same thumbprint),
        /// or -1. The candidates have no private key: the platform (the Windows store) keeps it.
        /// </summary>
        public static int IndexOf(X509Certificate2 certificate, IReadOnlyList<X509Certificate2?> candidates)
        {
            ArgumentNullException.ThrowIfNull(certificate);
            ArgumentNullException.ThrowIfNull(candidates);

            for (var i = 0; i < candidates.Count; i++)
            {
                if (candidates[i] is { } candidate &&
                    string.Equals(candidate.Thumbprint, certificate.Thumbprint, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// The certificate's private key as PKCS#8 (DER) and its algorithm name for <c>java.security.KeyFactory</c>
        /// (<c>RSA</c> or <c>EC</c>), as Android needs it. Null when the certificate has no private key, the key is of
        /// another type, or it cannot be exported (load the certificate with <see cref="X509KeyStorageFlags.Exportable"/>).
        /// </summary>
        public static (byte[] Pkcs8, string Algorithm)? ExportPrivateKey(X509Certificate2 certificate)
        {
            ArgumentNullException.ThrowIfNull(certificate);
            if (!certificate.HasPrivateKey)
                return null;

            try
            {
                using (var rsa = certificate.GetRSAPrivateKey())
                {
                    if (rsa is not null)
                        return (rsa.ExportPkcs8PrivateKey(), "RSA");
                }

                using (var ecdsa = certificate.GetECDsaPrivateKey())
                {
                    if (ecdsa is not null)
                        return (ecdsa.ExportPkcs8PrivateKey(), "EC");
                }
            }
            catch (CryptographicException)
            {
                // The key is not exportable.
            }
            return null;
        }
    }
}
