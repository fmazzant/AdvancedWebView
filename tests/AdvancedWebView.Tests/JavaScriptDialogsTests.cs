namespace Mafesoft.Maui.Controls.Tests;

// #56: what the page gets back from alert, confirm, prompt and beforeunload handled by the app.
public class JavaScriptDialogsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Alert_is_always_closed_normally(bool result) =>
        Assert.Equal(new JavaScriptDialogAnswer(true, null), JavaScriptDialogs.Resolve(JavaScriptDialogKind.Alert, result, "ignored"));

    [Theory]
    [InlineData(JavaScriptDialogKind.Confirm)]
    [InlineData(JavaScriptDialogKind.BeforeUnload)]
    public void Confirm_and_beforeunload_follow_the_result(JavaScriptDialogKind kind)
    {
        Assert.Equal(new JavaScriptDialogAnswer(true, null), JavaScriptDialogs.Resolve(kind, true, "ignored"));
        Assert.Equal(new JavaScriptDialogAnswer(false, null), JavaScriptDialogs.Resolve(kind, false, "ignored"));
    }

    [Theory]
    [InlineData(true, "Mario", true, "Mario")]
    [InlineData(true, "", true, "")]
    [InlineData(true, null, true, "")]
    [InlineData(false, "Mario", false, null)]
    [InlineData(false, null, false, null)]
    public void Prompt_returns_the_text_only_when_accepted(bool result, string? text, bool accept, string? expected) =>
        Assert.Equal(new JavaScriptDialogAnswer(accept, expected), JavaScriptDialogs.Resolve(JavaScriptDialogKind.Prompt, result, text));

    [Theory]
    [InlineData(JavaScriptDialogKind.Alert, true)]
    [InlineData(JavaScriptDialogKind.Confirm, false)]
    [InlineData(JavaScriptDialogKind.Prompt, false)]
    [InlineData(JavaScriptDialogKind.BeforeUnload, false)]
    public void Cancel_dismisses_every_dialog(JavaScriptDialogKind kind, bool accept)
    {
        var answer = JavaScriptDialogAnswer.Cancel(kind);
        Assert.Equal(accept, answer.Accept);
        Assert.Null(answer.Text);
    }

    [Theory]
    [InlineData("https://example.com/page?x=1#top", "https://example.com")]
    [InlineData("https://shop.example.com:8443/cart", "https://shop.example.com:8443")]
    [InlineData("http://localhost:5000/", "http://localhost:5000")]
    [InlineData("app://localhost/index.html", "app://localhost")]
    [InlineData("file:///android_asset/index.html", null)]
    [InlineData("about:blank", null)]
    [InlineData("data:text/html,hi", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Origin_comes_from_the_page_url(string? url, string? origin) =>
        Assert.Equal(origin is null ? null : new Uri(origin), JavaScriptDialogs.GetOrigin(url));
}
