using System.Net;

namespace Mafesoft.Maui.Controls.Tests;

public class CookieRulesTests
{
    static readonly Uri Page = new("https://shop.example.com/cart/items");

    [Fact]
    public void Parses_the_android_cookie_header()
    {
        var cookies = CookieRules.ParseCookieHeader("session=abc123; theme=dark; flag; empty=", Page);

        Assert.Equal(["session", "theme", "flag", "empty"], cookies.Select(c => c.Name));
        Assert.Equal(["abc123", "dark", "", ""], cookies.Select(c => c.Value));
        Assert.All(cookies, c => Assert.Equal("shop.example.com", c.Domain));
        Assert.All(cookies, c => Assert.Equal("/", c.Path));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Empty_header_has_no_cookies(string? header) =>
        Assert.Empty(CookieRules.ParseCookieHeader(header, Page));

    [Fact]
    public void Value_with_equals_sign_is_kept() =>
        Assert.Equal("a=b==", CookieRules.ParseCookieHeader("token=a=b==", Page)[0].Value);

    [Fact]
    public void Set_cookie_has_every_attribute()
    {
        var cookie = new Cookie("session", "abc", "/app", "example.com")
        {
            Secure = true,
            HttpOnly = true,
            Expires = new DateTime(2030, 1, 2, 3, 4, 5, DateTimeKind.Utc),
        };

        Assert.Equal("session=abc; Domain=example.com; Path=/app; Expires=Wed, 02 Jan 2030 03:04:05 GMT; Secure; HttpOnly",
            CookieRules.ToSetCookie(cookie));
    }

    [Fact]
    public void Session_cookie_has_no_expiry() =>
        Assert.Equal("a=1; Domain=example.com; Path=/", CookieRules.ToSetCookie(new Cookie("a", "1", "", "example.com")));

    [Theory]
    [InlineData(false, "/", "example.com", "http://example.com/")]
    [InlineData(true, "/app", ".example.com", "https://example.com/app")]
    public void Url_for_a_cookie(bool secure, string path, string domain, string expected) =>
        Assert.Equal(expected, CookieRules.UrlFor(new Cookie("a", "1", path, domain) { Secure = secure }));

    [Fact]
    public void Url_needs_a_domain() =>
        Assert.Throws<ArgumentException>(() => CookieRules.UrlFor(new Cookie("a", "1")));

    [Theory]
    [InlineData("shop.example.com", "/", false, true)]      // host-only, same host
    [InlineData("example.com", "/", false, true)]           // parent domain
    [InlineData(".example.com", "/", false, true)]          // leading dot
    [InlineData("other.example.com", "/", false, false)]    // sibling
    [InlineData("ample.com", "/", false, false)]            // not a label boundary
    [InlineData("example.com", "/cart", false, true)]       // path prefix at a boundary
    [InlineData("example.com", "/cart/", false, true)]
    [InlineData("example.com", "/car", false, false)]       // not a path boundary
    [InlineData("example.com", "/account", false, false)]
    [InlineData("example.com", "/", true, true)]            // secure on https
    public void Matching(string domain, string path, bool secure, bool expected) =>
        Assert.Equal(expected, CookieRules.Matches(domain, path, secure, Page));

    [Fact]
    public void Secure_cookie_is_not_sent_over_http() =>
        Assert.False(CookieRules.Matches("example.com", "/", true, new Uri("http://example.com/")));

    [Fact]
    public void Candidate_domains_for_deletion() =>
        Assert.Equal([null, "shop.example.com", ".shop.example.com", "example.com", ".example.com"],
            CookieRules.CandidateDomains("shop.example.com"));

    [Fact]
    public void Expired_set_cookie() =>
        Assert.Equal("a=; Max-Age=0; Expires=Thu, 01 Jan 1970 00:00:00 GMT; Path=/; Domain=.example.com",
            CookieRules.ToExpiredSetCookie("a", ".example.com", "/"));
}
