namespace Mafesoft.Maui.Controls.Tests;

// #54: HTTP authentication challenges and failed attempts.
public class HttpAuthenticationTests
{
    [Theory]
    [InlineData("Basic realm=\"Intranet\"", HttpAuthenticationScheme.Basic, "Intranet")]
    [InlineData("basic realm=\"Intranet\", charset=\"UTF-8\"", HttpAuthenticationScheme.Basic, "Intranet")]
    [InlineData("Digest realm=\"api@example.com\", qop=\"auth\", nonce=\"abc\"", HttpAuthenticationScheme.Digest, "api@example.com")]
    [InlineData("Digest qop=\"auth\", realm=\"Second\"", HttpAuthenticationScheme.Digest, "Second")]
    [InlineData("Digest xrealm=\"no\", realm=\"yes\"", HttpAuthenticationScheme.Digest, "yes")]
    [InlineData("Basic realm=Plain, charset=UTF-8", HttpAuthenticationScheme.Basic, "Plain")]
    [InlineData("Basic realm=\"\"", HttpAuthenticationScheme.Basic, "")]
    [InlineData("NTLM", HttpAuthenticationScheme.Ntlm, null)]
    [InlineData("Negotiate", HttpAuthenticationScheme.Negotiate, null)]
    [InlineData("Bearer realm=\"api\"", HttpAuthenticationScheme.Unknown, "api")]
    [InlineData("", HttpAuthenticationScheme.Unknown, null)]
    [InlineData(null, HttpAuthenticationScheme.Unknown, null)]
    public void Challenges_are_parsed(string? challenge, HttpAuthenticationScheme scheme, string? realm) =>
        Assert.Equal((scheme, realm), HttpAuthenticationChallenge.Parse(challenge));

    [Theory]
    [InlineData("NSURLAuthenticationMethodHTTPBasic", HttpAuthenticationScheme.Basic)]
    [InlineData("NSURLAuthenticationMethodHTTPDigest", HttpAuthenticationScheme.Digest)]
    [InlineData("NSURLAuthenticationMethodNTLM", HttpAuthenticationScheme.Ntlm)]
    [InlineData("NSURLAuthenticationMethodNegotiate", HttpAuthenticationScheme.Negotiate)]
    [InlineData("NSURLAuthenticationMethodServerTrust", HttpAuthenticationScheme.Unknown)]
    public void Apple_authentication_methods_map_to_schemes(string method, HttpAuthenticationScheme scheme) =>
        Assert.Equal(scheme, HttpAuthenticationChallenge.ParseScheme(method));

    [Fact]
    public void Attempts_are_counted_per_host_and_realm_until_reset()
    {
        var attempts = new HttpAuthenticationAttempts();

        Assert.Equal(0, attempts.Next("intranet.example.com", "Intranet"));
        Assert.Equal(1, attempts.Next("intranet.example.com", "Intranet"));
        Assert.Equal(0, attempts.Next("intranet.example.com", "Other"));
        Assert.Equal(0, attempts.Next("api.example.com", "Intranet"));
        Assert.Equal(2, attempts.Next("INTRANET.example.com", "Intranet"));

        attempts.Reset();
        Assert.Equal(0, attempts.Next("intranet.example.com", "Intranet"));
    }
}
