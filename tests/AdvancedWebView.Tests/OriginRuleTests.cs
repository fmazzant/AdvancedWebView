namespace Mafesoft.Maui.Controls.Tests;

public class OriginRuleTests
{
    [Theory]
    [InlineData("*")]
    [InlineData("https://example.com")]
    [InlineData("https://example.com/")]
    [InlineData("https://example.com:8443")]
    [InlineData("https://*.example.com")]
    [InlineData("http://localhost:5000")]
    [InlineData("HTTPS://Example.COM")]
    public void Valid_rules_parse(string rule) => Assert.True(OriginRule.TryParse(rule, out _));

    [Theory]
    [InlineData("")]
    [InlineData("example.com")]
    [InlineData("https://")]
    [InlineData("https://example.com/path")]
    [InlineData("https://example.com?q=1")]
    [InlineData("https://ex*mple.com")]
    [InlineData("https://*.*.example.com")]
    [InlineData("https://user@example.com")]
    public void Invalid_rules_are_rejected(string rule) => Assert.False(OriginRule.TryParse(rule, out _));

    [Theory]
    [InlineData("https://example.com", "https://example.com", true)]
    [InlineData("https://example.com", "https://EXAMPLE.com", true)]
    [InlineData("https://example.com", "http://example.com", false)]
    [InlineData("https://example.com", "https://example.com:8443", false)]
    [InlineData("https://example.com:8443", "https://example.com:8443", true)]
    [InlineData("https://example.com", "https://shop.example.com", false)]
    [InlineData("https://*.example.com", "https://shop.example.com", true)]
    [InlineData("https://*.example.com", "https://a.b.example.com", true)]
    [InlineData("https://*.example.com", "https://example.com", false)]
    [InlineData("https://*.example.com", "https://example.com.evil.net", false)]
    [InlineData("https://*.example.com", "https://evilexample.com", false)]
    [InlineData("*", "https://anything.net", true)]
    public void Rules_match_origins(string rule, string origin, bool expected) =>
        Assert.Equal(expected, OriginRule.Parse(rule).Matches(new Uri(origin)));

    [Fact]
    public void Local_content_is_recognized()
    {
        Assert.True(OriginRule.IsLocal(null));
        Assert.True(OriginRule.IsLocal(new Uri("file:///android_asset/")));
        Assert.False(OriginRule.IsLocal(new Uri("https://example.com")));
    }

    [Fact]
    public void Options_reject_invalid_rules_when_added()
    {
        var options = new BridgeOptions();

        Assert.Throws<ArgumentException>(() => options.AllowedOrigins.Add("example.com"));
        Assert.Throws<ArgumentException>(() => options.AllowedOrigins = ["https://ok.com", "not a rule"]);
    }

    [Fact]
    public void Options_allow_local_content_and_listed_origins()
    {
        var options = new BridgeOptions { AllowedOrigins = ["https://shop.example.com"] };

        Assert.True(options.IsOriginAllowed(null));
        Assert.True(options.IsOriginAllowed(new Uri("https://shop.example.com")));
        Assert.False(options.IsOriginAllowed(new Uri("https://other.com")));
    }
}
