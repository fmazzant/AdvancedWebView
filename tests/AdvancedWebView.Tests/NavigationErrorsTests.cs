namespace Mafesoft.Maui.Controls.Tests;

public class NavigationErrorsTests
{
    [Theory]
    [InlineData(-2, "net::ERR_INTERNET_DISCONNECTED", WebNavigationError.NoInternet)] // airplane mode
    [InlineData(-6, "net::ERR_INTERNET_DISCONNECTED", WebNavigationError.NoInternet)]
    [InlineData(-2, "net::ERR_NAME_NOT_RESOLVED", WebNavigationError.HostNotFound)]
    [InlineData(-6, "net::ERR_CONNECTION_REFUSED", WebNavigationError.HostNotFound)]
    [InlineData(-8, "net::ERR_TIMED_OUT", WebNavigationError.Timeout)]
    [InlineData(-11, "net::ERR_SSL_PROTOCOL_ERROR", WebNavigationError.SslError)]
    [InlineData(-1, "net::ERR_ABORTED", WebNavigationError.Cancelled)]
    [InlineData(-10, "net::ERR_UNKNOWN_URL_SCHEME", WebNavigationError.Unknown)]
    [InlineData(-1, null, WebNavigationError.Unknown)]
    public void Android_errors(int code, string? description, WebNavigationError expected) =>
        Assert.Equal(expected, NavigationErrors.FromAndroid(code, description));

    [Theory]
    [InlineData("NSURLErrorDomain", -1009, WebNavigationError.NoInternet)] // airplane mode
    [InlineData("NSURLErrorDomain", -1020, WebNavigationError.NoInternet)]
    [InlineData("NSURLErrorDomain", -1005, WebNavigationError.NoInternet)]
    [InlineData("NSURLErrorDomain", -1001, WebNavigationError.Timeout)]
    [InlineData("NSURLErrorDomain", -1003, WebNavigationError.HostNotFound)]
    [InlineData("NSURLErrorDomain", -1004, WebNavigationError.HostNotFound)]
    [InlineData("NSURLErrorDomain", -1200, WebNavigationError.SslError)]
    [InlineData("NSURLErrorDomain", -1202, WebNavigationError.SslError)]
    [InlineData("NSURLErrorDomain", -1206, WebNavigationError.SslError)]
    [InlineData("NSURLErrorDomain", -999, WebNavigationError.Cancelled)]
    [InlineData("WebKitErrorDomain", 102, WebNavigationError.Cancelled)]
    [InlineData("WebKitErrorDomain", 101, WebNavigationError.Unknown)]
    [InlineData("NSURLErrorDomain", -1100, WebNavigationError.Unknown)]
    [InlineData("SomeOtherDomain", -1009, WebNavigationError.Unknown)]
    public void Ios_errors(string domain, long code, WebNavigationError expected) =>
        Assert.Equal(expected, NavigationErrors.FromNSError(domain, code));

    [Fact]
    public void Error_page_replaces_placeholders()
    {
        var html = ErrorPage.Render("<p>{{error}} {{statusCode}}: {{description}} at {{url}}</p>",
            "https://example.com/a?b=1&c=2", WebNavigationError.HttpError, 404, "Not found");

        Assert.StartsWith("<p>HttpError 404: Not found at https://example.com/a?b=1&amp;c=2</p>", html);
    }

    [Fact]
    public void Error_page_encodes_placeholders()
    {
        var html = ErrorPage.Render("{{description}}", "https://example.com/", WebNavigationError.Unknown, null, "<img src=x onerror=alert(1)>");

        Assert.StartsWith("&lt;img src=x onerror=alert(1)&gt;", html);
    }

    [Fact]
    public void Error_page_status_code_is_empty_without_http_error() =>
        Assert.StartsWith("[]", ErrorPage.Render("[{{statusCode}}]", "https://example.com/", WebNavigationError.Timeout, null, ""));

    [Fact]
    public void Error_page_script_sets_the_failed_url_after_the_template()
    {
        var html = ErrorPage.Render("<!DOCTYPE html><p>x</p>", "https://example.com/", WebNavigationError.NoInternet, null, "");

        Assert.StartsWith("<!DOCTYPE html>", html); // nothing before the doctype (quirks mode)
        Assert.Contains("window.__advancedWebViewFailedUrl = \"https://example.com/\";", html);
    }

    [Fact]
    public void Failed_url_cannot_close_the_script_element()
    {
        var html = ErrorPage.Render("", "https://example.com/</script><script>alert(1)</script>", WebNavigationError.Unknown, null, "");

        Assert.Single(System.Text.RegularExpressions.Regex.Matches(html, "</script>"));
    }
}
