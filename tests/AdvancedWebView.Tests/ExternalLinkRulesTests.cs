namespace Mafesoft.Maui.Controls.Tests;

public class ExternalLinkRulesTests
{
    static readonly NormalizedList DefaultSchemes = new(ExternalLinkRules.NormalizeScheme, ExternalLinkRules.DefaultSchemes);
    static readonly NormalizedList NoHosts = new(ExternalLinkRules.NormalizeHost);

    static ExternalLinkReason? Classify(string url, IEnumerable<string>? hosts = null, bool isMainFrame = true) =>
        ExternalLinkRules.Classify(url, DefaultSchemes, hosts is null ? NoHosts : new NormalizedList(ExternalLinkRules.NormalizeHost, hosts), isMainFrame);

    [Theory]
    [InlineData("tel:+390123456789")]
    [InlineData("TEL:+39 012 345")]
    [InlineData("mailto:info@example.com?subject=Hi")]
    [InlineData("sms:+390123456789")]
    [InlineData("geo:43.77,11.25")]
    [InlineData("whatsapp://send?text=hello")]
    [InlineData("intent://scan/#Intent;scheme=zxing;package=com.google.zxing.client.android;end")]
    [InlineData("market://details?id=com.example")]
    [InlineData("itms-apps://apps.apple.com/app/id123")]
    public void Default_schemes_are_external(string url) =>
        Assert.Equal(ExternalLinkReason.Scheme, Classify(url));

    [Theory]
    [InlineData("https://example.com/page")]
    [InlineData("http://example.com")]
    [InlineData("about:blank")]
    [InlineData("data:text/html,hi")]
    [InlineData("blob:https://example.com/1234")]
    [InlineData("javascript:void(0)")]
    [InlineData("myapp://unknown")]
    [InlineData("")]
    [InlineData("no-scheme")]
    [InlineData("/relative/path")]
    public void Other_urls_stay_in_the_web_view(string url) =>
        Assert.Null(Classify(url));

    [Fact]
    public void Scheme_rule_applies_to_iframes_too() =>
        Assert.Equal(ExternalLinkReason.Scheme, Classify("tel:123", isMainFrame: false));

    [Fact]
    public void Empty_internal_hosts_keep_every_site_inside() =>
        Assert.Null(Classify("https://anything.example.net/"));

    [Theory]
    [InlineData("https://example.com/", null)]
    [InlineData("https://EXAMPLE.com/path", null)]
    [InlineData("https://shop.example.com/", null)]
    [InlineData("https://a.b.example.com/", null)]
    [InlineData("https://www.example.org/", ExternalLinkReason.Host)]
    [InlineData("https://example.com.evil.net/", ExternalLinkReason.Host)]
    [InlineData("https://evilexample.com/", ExternalLinkReason.Host)]
    public void Internal_hosts(string url, ExternalLinkReason? expected) =>
        Assert.Equal(expected, Classify(url, ["example.com", "*.example.com"]));

    [Fact]
    public void Wildcard_does_not_match_the_bare_domain() =>
        Assert.Equal(ExternalLinkReason.Host, Classify("https://example.com/", ["*.example.com"]));

    [Fact]
    public void Host_rule_ignores_iframes() =>
        Assert.Null(Classify("https://ads.other.net/frame", ["example.com"], isMainFrame: false));

    [Fact]
    public void Unicode_host_rules_match_punycode_urls() =>
        Assert.Null(Classify("https://xn--caf-dma.example/", ["café.example"]));

    [Theory]
    [InlineData("tel", "tel")]
    [InlineData("TEL:", "tel")]
    [InlineData(" whatsapp:// ", "whatsapp")]
    [InlineData("itms-apps", "itms-apps")]
    public void Schemes_are_normalized(string input, string expected) =>
        Assert.Equal(expected, ExternalLinkRules.NormalizeScheme(input));

    [Theory]
    [InlineData("https")]
    [InlineData("http")]
    [InlineData("about")]
    [InlineData("1tel")]
    [InlineData("te l")]
    [InlineData("")]
    public void Invalid_or_web_schemes_are_rejected(string scheme) =>
        Assert.Throws<ArgumentException>(() => ExternalLinkRules.NormalizeScheme(scheme));

    [Theory]
    [InlineData(" Example.COM ", "example.com")]
    [InlineData("*.Example.com", "*.example.com")]
    public void Hosts_are_normalized(string input, string expected) =>
        Assert.Equal(expected, ExternalLinkRules.NormalizeHost(input));

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("example.com/path")]
    [InlineData("*example.com")]
    [InlineData("a.*.example.com")]
    [InlineData("*.")]
    [InlineData("")]
    public void Invalid_hosts_are_rejected(string host) =>
        Assert.Throws<ArgumentException>(() => ExternalLinkRules.NormalizeHost(host));

    [Fact]
    public void List_normalizes_and_skips_duplicates()
    {
        var list = new NormalizedList(ExternalLinkRules.NormalizeScheme, ["tel", "TEL:", "mailto"]);
        Assert.Equal(["tel", "mailto"], list);
        Assert.Throws<ArgumentException>(() => list.Add("https"));
    }
}
