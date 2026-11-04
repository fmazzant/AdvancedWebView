namespace Mafesoft.Maui.Controls.Tests;

// AdvancedWebView.retry(), used by the button of ErrorPageHtml.
public class RetryTests
{
    [Fact]
    public void Retry_loads_the_failed_url_set_by_the_error_page()
    {
        var page = new FakePage();
        var html = ErrorPage.Render("", "https://example.com/failed?x=1", WebNavigationError.NoInternet, null, "");
        var script = html[(html.IndexOf("<script>", StringComparison.Ordinal) + 8)..html.IndexOf("</script>", StringComparison.Ordinal)];

        page.Execute(script);
        page.Execute("AdvancedWebView.retry();");

        Assert.Equal("https://example.com/failed?x=1", page.Get<string>("window.location.href"));
    }

    [Fact]
    public void Retry_reloads_outside_the_error_page()
    {
        var page = new FakePage();
        page.Execute("window.reloaded = false; window.location.reload = function () { window.reloaded = true; };");

        page.Execute("AdvancedWebView.retry();");

        Assert.True(page.Get<bool>("window.reloaded"));
    }
}
