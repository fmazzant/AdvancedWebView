namespace Mafesoft.Maui.Controls.Tests;

// #60: LoadUrl headers go only to the origin of the URL they were given with.
public class RequestHeadersTests
{
    static Dictionary<string, string> Headers(string token = "Bearer secret") =>
        new(StringComparer.OrdinalIgnoreCase) { ["Authorization"] = token, ["X-Tenant"] = "42" };

    static KeyValuePair<string, string>[] Sent(string token = "Bearer secret") =>
        [new("Authorization", token), new("X-Tenant", "42"), new("Accept", "text/html")];

    [Theory]
    [InlineData("https://api.example.com/account", "https://api.example.com/other?x=1", true)]
    [InlineData("https://api.example.com/account", "https://API.Example.com:443/", true)]
    [InlineData("https://api.example.com/account", "https://evil.net/", false)]
    [InlineData("https://api.example.com/account", "https://www.example.com/", false)]
    [InlineData("https://api.example.com/account", "http://api.example.com/", false)]
    [InlineData("https://api.example.com/account", "https://api.example.com:8443/", false)]
    [InlineData("http://10.0.2.2:5100/page", "http://10.0.2.2:5101/page", false)]
    [InlineData("https://api.example.com/account", "about:blank", false)]
    [InlineData("https://api.example.com/account", null, false)]
    [InlineData("file:///android_asset/index.html", "file:///android_asset/index.html", false)]
    public void Headers_are_allowed_only_on_their_origin(string loaded, string? target, bool expected) =>
        Assert.Equal(expected, new RequestHeaderScope(loaded, Headers()).IsAllowed(target));

    [Fact]
    public void Headers_sent_to_another_origin_are_reported_as_leaked()
    {
        var state = new RequestHeaderState();
        state.BeginRequest("https://api.example.com/account", Headers(), isPost: false);

        Assert.Equal(["Authorization", "X-Tenant"], state.FindLeakedHeaders("https://evil.net/landing", Sent()));
        Assert.Empty(state.FindLeakedHeaders("https://api.example.com/landing", Sent()));
    }

    [Fact]
    public void Only_headers_with_the_value_sent_by_LoadUrl_are_leaked()
    {
        var state = new RequestHeaderState();
        state.BeginRequest("https://api.example.com/account", Headers(), isPost: false);

        // Same names, other values: set by someone else (the engine, the page), not ours.
        Assert.Empty(state.FindLeakedHeaders("https://evil.net/", [new("Authorization", "Basic other"), new("X-Tenant", "7")]));
    }

    [Fact]
    public void A_header_given_to_both_origins_is_kept()
    {
        var state = new RequestHeaderState();
        state.BeginRequest("https://a.example.com/", Headers(), isPost: false);
        state.BeginRequest("https://b.example.com/", Headers(), isPost: false);

        Assert.Empty(state.FindLeakedHeaders("https://b.example.com/x", Sent()));
        Assert.Equal(["Authorization", "X-Tenant"], state.FindLeakedHeaders("https://c.example.com/x", Sent()));
    }

    [Fact]
    public void Nothing_is_leaked_without_LoadUrl_headers()
    {
        var state = new RequestHeaderState();
        state.BeginRequest("https://api.example.com/", new Dictionary<string, string>(), isPost: false);

        Assert.Empty(state.FindLeakedHeaders("https://evil.net/", Sent()));
    }

    [Fact]
    public void Reload_sends_the_headers_again_on_the_page_loaded_with_them()
    {
        var state = new RequestHeaderState();
        state.BeginRequest("https://api.example.com/start", Headers(), isPost: false);
        state.OnCommitted("https://api.example.com/home"); // same-origin redirect

        Assert.Equal("Bearer secret", state.GetReloadHeaders("https://api.example.com/home")!["Authorization"]);
        Assert.Null(state.GetReloadHeaders("https://evil.net/"));
    }

    [Fact]
    public void Reload_does_not_send_the_headers_after_a_redirect_to_another_origin()
    {
        var state = new RequestHeaderState();
        state.BeginRequest("https://api.example.com/start", Headers(), isPost: false);
        state.OnCommitted("https://evil.net/landing");

        Assert.Null(state.GetReloadHeaders("https://evil.net/landing"));
    }

    [Fact]
    public void Reload_does_not_send_the_headers_once_another_page_is_committed()
    {
        var state = new RequestHeaderState();
        state.BeginRequest("https://api.example.com/start", Headers(), isPost: false);
        state.OnCommitted("https://api.example.com/start");
        state.BeginOtherNavigation();
        state.OnCommitted("https://api.example.com/next"); // a link on the same site

        Assert.Null(state.GetReloadHeaders("https://api.example.com/next"));
    }

    [Fact]
    public void Reload_of_a_POST_is_left_to_the_engine()
    {
        var state = new RequestHeaderState();
        state.BeginRequest("https://api.example.com/login", Headers(), isPost: true);
        state.OnCommitted("https://api.example.com/login");

        Assert.Null(state.GetReloadHeaders("https://api.example.com/login"));
        // Its headers are still recognized on another origin.
        Assert.NotEmpty(state.FindLeakedHeaders("https://evil.net/", Sent()));
    }

    [Fact]
    public void A_redirect_of_the_LoadUrl_navigation_to_another_origin_is_dropped()
    {
        var state = new RequestHeaderState();
        state.BeginRequest("https://api.example.com/start", Headers(), isPost: false);

        Assert.False(state.MustDropOnRedirect("https://api.example.com/home"));
        Assert.True(state.MustDropOnRedirect("https://evil.net/landing"));
        Assert.True(state.MustDropOnRedirect("http://api.example.com/home"));
    }

    [Fact]
    public void A_redirect_during_a_reload_of_the_page_with_headers_is_dropped()
    {
        var state = new RequestHeaderState();
        state.BeginRequest("https://api.example.com/start", Headers(), isPost: false);
        state.OnCommitted("https://api.example.com/start");

        // Native reload or back navigation: the engine can send the headers again, and the page can redirect.
        Assert.True(state.MustDropOnRedirect("https://evil.net/landing"));
    }

    [Fact]
    public void A_redirect_of_a_link_is_not_touched()
    {
        var state = new RequestHeaderState();
        state.BeginRequest("https://api.example.com/start", Headers(), isPost: false);
        state.OnCommitted("https://api.example.com/start");
        state.BeginOtherNavigation();

        Assert.False(state.MustDropOnRedirect("https://evil.net/landing"));
    }

    [Fact]
    public void Old_scopes_are_forgotten_after_sixteen_requests()
    {
        var state = new RequestHeaderState();
        state.BeginRequest("https://first.example.com/", Headers("Bearer first"), isPost: false);
        for (var i = 0; i < 16; i++)
            state.BeginRequest($"https://site{i}.example.com/", Headers($"Bearer {i}"), isPost: false);

        // X-Tenant has the same value in every scope, so it is still recognized; the first token is not.
        Assert.DoesNotContain("Authorization", state.FindLeakedHeaders("https://evil.net/", Sent("Bearer first")));
        Assert.Contains("Authorization", state.FindLeakedHeaders("https://evil.net/", Sent("Bearer 15")));
    }
}
