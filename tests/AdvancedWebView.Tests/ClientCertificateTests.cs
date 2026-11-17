using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Mafesoft.Maui.Controls.Tests;

// #58: the conversions behind ClientCertificateRequested (issuer names, the Windows store match, the key for Android).
public class ClientCertificateTests
{
    static X509Certificate2 CreateRsaCertificate(string subject = "CN=Client, O=Example")
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));
    }

    static X509Certificate2 CreateEcdsaCertificate()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=Client EC", ecdsa, HashAlgorithmName.SHA256);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));
    }

    [Fact]
    public void Issuer_names_are_decoded_from_der()
    {
        var names = ClientCertificates.IssuerNames(
        [
            new X500DistinguishedName("CN=Example CA, O=Example").RawData,
            new X500DistinguishedName("CN=Other CA").RawData,
        ]);

        Assert.Equal(["CN=Example CA, O=Example", "CN=Other CA"], names);
    }

    [Fact]
    public void Issuer_names_skip_empty_and_invalid_entries()
    {
        var names = ClientCertificates.IssuerNames([[], [0x01, 0x02, 0x03], new X500DistinguishedName("CN=Example CA").RawData]);

        Assert.Equal(["CN=Example CA"], names);
    }

    [Fact]
    public void Issuer_names_of_null_are_empty() =>
        Assert.Empty(ClientCertificates.IssuerNames(null));

    [Fact]
    public void Pem_is_read_back_as_the_same_certificate()
    {
        using var certificate = CreateRsaCertificate();

        using var read = ClientCertificates.FromPem(certificate.ExportCertificatePem());

        Assert.NotNull(read);
        Assert.Equal(certificate.Thumbprint, read.Thumbprint);
        Assert.False(read.HasPrivateKey);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a certificate")]
    [InlineData("-----BEGIN CERTIFICATE-----\nAAAA\n-----END CERTIFICATE-----")]
    public void Invalid_pem_is_null(string? pem) =>
        Assert.Null(ClientCertificates.FromPem(pem));

    [Fact]
    public void Certificate_is_matched_by_thumbprint_in_the_store_list()
    {
        using var other = CreateRsaCertificate("CN=Other");
        using var picked = CreateRsaCertificate();
        // As on Windows: the store copies have no private key, and an entry may be unreadable.
        using var storeCopy = X509Certificate2.CreateFromPem(picked.ExportCertificatePem());

        Assert.Equal(2, ClientCertificates.IndexOf(picked, [other, null, storeCopy]));
    }

    [Fact]
    public void Certificate_not_in_the_store_list_is_not_matched()
    {
        using var other = CreateRsaCertificate("CN=Other");
        using var picked = CreateRsaCertificate();

        Assert.Equal(-1, ClientCertificates.IndexOf(picked, [other]));
        Assert.Equal(-1, ClientCertificates.IndexOf(picked, []));
    }

    [Fact]
    public void Rsa_private_key_is_exported_as_pkcs8()
    {
        using var certificate = CreateRsaCertificate();

        var key = ClientCertificates.ExportPrivateKey(certificate);

        Assert.NotNull(key);
        Assert.Equal("RSA", key.Value.Algorithm);
        using var rsa = RSA.Create();
        rsa.ImportPkcs8PrivateKey(key.Value.Pkcs8, out _);
        using var publicKey = certificate.GetRSAPublicKey()!;
        Assert.Equal(publicKey.ExportParameters(false).Modulus, rsa.ExportParameters(false).Modulus);
    }

    [Fact]
    public void Ec_private_key_is_exported_as_pkcs8()
    {
        using var certificate = CreateEcdsaCertificate();

        var key = ClientCertificates.ExportPrivateKey(certificate);

        Assert.NotNull(key);
        Assert.Equal("EC", key.Value.Algorithm);
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportPkcs8PrivateKey(key.Value.Pkcs8, out _);
    }

    [Fact]
    public void Certificate_without_private_key_exports_nothing()
    {
        using var certificate = CreateRsaCertificate();
        using var publicOnly = X509CertificateLoader.LoadCertificate(certificate.RawData);

        Assert.Null(ClientCertificates.ExportPrivateKey(publicOnly));
    }
}
