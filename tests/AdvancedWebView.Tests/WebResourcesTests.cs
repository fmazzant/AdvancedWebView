namespace Mafesoft.Maui.Controls.Tests;

// #55: custom URL schemes, URL matching, responses, and the bridge on custom-scheme pages.
public class WebResourcesTests
{
    static readonly string[] Schemes = ["app", "myapp"];

    [Theory]
    [InlineData("app", "app")]
    [InlineData(" APP ", "app")]
    [InlineData("app:", "app")]
    [InlineData("app://", "app")]
    [InlineData("my-app.v2+x", "my-app.v2+x")]
    public void Custom_schemes_are_normalized(string input, string expected) =>
        Assert.Equal(expected, WebResourceRules.NormalizeCustomScheme(input));

    [Theory]
    [InlineData("")]
    [InlineData("1app")]
    [InlineData("a pp")]
    [InlineData("app/x")]
    [InlineData("http")]
    [InlineData("HTTPS")]
    [InlineData("file")]
    [InlineData("about")]
    [InlineData("data")]
    [InlineData("blob")]
    [InlineData("javascript")]
    [InlineData("ws")]
    public void Invalid_or_reserved_schemes_are_rejected(string scheme) =>
        Assert.Throws<ArgumentException>(() => WebResourceRules.NormalizeCustomScheme(scheme));

    [Fact]
    public void Custom_scheme_list_normalizes_and_rejects_entries()
    {
        var list = new NormalizedList(WebResourceRules.NormalizeCustomScheme, ["App", "app:", "myapp"]);

        Assert.Equal(["app", "myapp"], list);
        Assert.Throws<ArgumentException>(() => list.Add("https"));
    }

    [Theory]
    [InlineData("app://localhost/index.html", "app")]
    [InlineData("APP://localhost/css/site.css?v=1#top", "app")]
    [InlineData("myapp://assets/logo.png", "myapp")]
    [InlineData("app:index.html", "app")]
    [InlineData("https://localhost/index.html", null)]
    [InlineData("application://localhost/", null)]
    [InlineData("ap://localhost/", null)]
    [InlineData("/index.html", null)]
    [InlineData("index.html", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Urls_are_matched_by_scheme(string? url, string? scheme)
    {
        Assert.Equal(scheme, WebResourceRules.GetCustomScheme(url, Schemes));
        Assert.Equal(scheme is not null, WebResourceRules.IsCustomSchemeUrl(url, Schemes));
    }

    [Fact]
    public void Nothing_matches_without_custom_schemes() =>
        Assert.False(WebResourceRules.IsCustomSchemeUrl("app://localhost/index.html", []));

    [Theory]
    [InlineData("app://localhost/index.html", "app://localhost")]
    [InlineData("APP://LocalHost/a/b.js?x=1", "app://localhost")]
    [InlineData("app://localhost:8080/index.html", "app://localhost:8080")]
    [InlineData("myapp://assets/", "myapp://assets")]
    [InlineData("app:index.html", null)]
    [InlineData("https://example.com/", null)]
    [InlineData(null, null)]
    public void Custom_scheme_origin_is_scheme_and_host(string? url, string? origin) =>
        Assert.Equal(origin is null ? null : new Uri(origin), WebResourceRules.GetCustomSchemeOrigin(url, Schemes));

    [Theory]
    [InlineData("index.html", "text/html")]
    [InlineData("app://localhost/css/site.CSS?v=3#x", "text/css")]
    [InlineData("js/app.js", "text/javascript")]
    [InlineData("images\\logo.svg", "image/svg+xml")]
    [InlineData("photo.jpeg", "image/jpeg")]
    [InlineData("font.woff2", "font/woff2")]
    [InlineData("data.json", "application/json")]
    [InlineData("README", "application/octet-stream")]
    [InlineData("archive.unknown", "application/octet-stream")]
    [InlineData("app://localhost/folder.v2/file", "application/octet-stream")]
    public void Content_type_comes_from_the_extension(string path, string expected) =>
        Assert.Equal(expected, WebResourceResponse.GetContentType(path));

    [Theory]
    [InlineData("text/html", "text/html", null)]
    [InlineData("text/html; charset=utf-8", "text/html", "utf-8")]
    [InlineData("text/css;charset=\"ISO-8859-1\"", "text/css", "ISO-8859-1")]
    [InlineData("application/json; boundary=x", "application/json", null)]
    public void Content_type_is_split_into_mime_type_and_charset(string contentType, string mimeType, string? charset) =>
        Assert.Equal((mimeType, charset), WebResourceRules.SplitContentType(contentType));

    [Fact]
    public void Response_with_content_has_its_type_and_standard_status()
    {
        using var content = new MemoryStream([1, 2, 3]);
        var response = new WebResourceResponse(content, " text/html; charset=utf-8 ");
        response.Headers["cache-control"] = "no-cache";

        Assert.Same(content, response.Content);
        Assert.Equal("text/html; charset=utf-8", response.ContentType);
        Assert.Equal(200, response.StatusCode);
        Assert.Equal("OK", response.ReasonPhrase);
        Assert.Equal("no-cache", response.Headers["Cache-Control"]);

        var headers = WebResourceRules.BuildHeaders(response);
        Assert.Equal("text/html; charset=utf-8", headers["content-type"]);
        Assert.Equal("no-cache", headers["Cache-Control"]);
    }

    [Theory]
    [InlineData(403, null, "Forbidden")]
    [InlineData(404, null, "Not Found")]
    [InlineData(404, " Missing ", "Missing")]
    [InlineData(418, null, "Client Error")]
    [InlineData(500, "", "Internal Server Error")]
    public void Response_without_content_blocks_with_a_status(int status, string? reason, string expected)
    {
        var response = new WebResourceResponse(status, reason);

        Assert.Null(response.Content);
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(expected, response.ReasonPhrase);
        Assert.False(WebResourceRules.BuildHeaders(response).ContainsKey("Content-Type"));
    }

    [Theory]
    [InlineData(99)]
    [InlineData(301)]
    [InlineData(302)]
    [InlineData(600)]
    public void Response_rejects_redirects_and_invalid_status_codes(int status) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new WebResourceResponse(status));

    [Fact]
    public void Response_needs_content_and_type()
    {
        Assert.Throws<ArgumentNullException>(() => new WebResourceResponse(null!, "text/html"));
        Assert.Throws<ArgumentException>(() => new WebResourceResponse(new MemoryStream(), " "));
    }

    [Fact]
    public void Origin_rules_accept_custom_schemes()
    {
        var rule = OriginRule.Parse("app://localhost");

        Assert.True(rule.Matches(new Uri("app://localhost")));
        Assert.True(rule.Matches(WebResourceRules.GetCustomSchemeOrigin("app://localhost/index.html", Schemes)));
        Assert.False(rule.Matches(new Uri("app://other")));
        Assert.False(rule.Matches(new Uri("https://localhost")));
        Assert.False(rule.Matches(new Uri("myapp://localhost")));
    }

    [Fact]
    public void Custom_scheme_pages_are_app_content_for_the_bridge()
    {
        var options = new BridgeOptions { LocalSchemes = () => Schemes };

        Assert.True(options.IsOriginAllowed(new Uri("app://localhost")));
        Assert.True(options.IsOriginAllowed(new Uri("MYAPP://assets")));
        Assert.False(options.IsOriginAllowed(new Uri("other://localhost")));
        Assert.False(options.IsOriginAllowed(new Uri("https://example.com")));

        var withoutSchemes = new BridgeOptions();
        Assert.False(withoutSchemes.IsOriginAllowed(new Uri("app://localhost")));

        var listed = new BridgeOptions { AllowedOrigins = ["app://localhost"] };
        Assert.True(listed.IsOriginAllowed(new Uri("app://localhost")));
        Assert.False(listed.IsOriginAllowed(new Uri("app://other")));
    }

    [Fact]
    public async Task Bridge_works_from_a_custom_scheme_page()
    {
        var options = new BridgeOptions { LocalSchemes = () => Schemes };
        var page = new FakePage { Origin = new Uri("app://localhost") };
        var events = new List<WebMessageReceivedEventArgs>();
        var bridge = new WebViewBridge(options, page.Enqueue, events.Add);
        page.Bridge = bridge;
        bridge.SetHandler("ping", (_, _) => Task.FromResult<string?>("\"pong\""));

        page.Execute("""
            var result = null;
            AdvancedWebView.postMessage('from app://');
            AdvancedWebView.invoke('ping').then(function (v) { result = v; });
            """);
        await page.SettleAsync(100);

        var message = Assert.Single(events);
        Assert.Equal("from app://", message.Message);
        Assert.Equal(new Uri("app://localhost"), message.Origin);
        Assert.Equal("pong", page.Get<string>("result"));
    }
}
