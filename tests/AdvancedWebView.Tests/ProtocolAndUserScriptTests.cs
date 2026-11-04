using System.Text.Json;
using Jint;

namespace Mafesoft.Maui.Controls.Tests;

public class ProtocolTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[1,2]")]
    [InlineData("""{"type":"message"}""")]                                  // no pageId
    [InlineData("""{"type":"invoke","pageId":"p","id":"1"}""")]            // no method
    [InlineData("""{"type":"invoke","pageId":"p","method":"m"}""")]        // no id
    [InlineData("""{"type":"callResult","pageId":"p"}""")]                 // no id
    [InlineData("""{"type":"unknown","pageId":"p"}""")]
    public void Invalid_envelopes_are_ignored(string? text) => Assert.Null(BridgeProtocol.Parse(text));

    [Fact]
    public void Invoke_envelope_is_parsed()
    {
        var e = BridgeProtocol.Parse("""{"type":"invoke","pageId":"p","id":"3","method":"getUser","args":{"id":1}}""")!;

        Assert.Equal(("invoke", "p", "3", "getUser"), (e.Type, e.PageId, e.Id, e.Method));
        Assert.Equal(1, e.Payload.GetProperty("id").GetInt32());
    }

    [Fact]
    public void Error_envelope_has_code_message_and_optional_details()
    {
        var json = JsonDocument.Parse(BridgeProtocol.ErrorEnvelope("p", "1", "NotFound", "nope")).RootElement;

        Assert.Equal("error", json.GetProperty("type").GetString());
        Assert.Equal("NotFound", json.GetProperty("error").GetProperty("code").GetString());
        Assert.False(json.GetProperty("error").TryGetProperty("details", out _));
    }

    [Fact]
    public void Arguments_must_be_a_json_array()
    {
        Assert.Equal("""[1,"x"]""", BridgeProtocol.NormalizeArray("""[ 1, "x" ]"""));
        Assert.Throws<ArgumentException>(() => BridgeProtocol.NormalizeArray("""{"a":1}"""));
        Assert.ThrowsAny<JsonException>(() => BridgeProtocol.NormalizeArray("[1,"));
    }

    [Fact]
    public void Scripts_escape_html_sensitive_characters()
    {
        var script = BridgeProtocol.ReceiveScript(BridgeProtocol.MessageEnvelope(BridgeProtocol.JsonString("</script><b>")));

        Assert.DoesNotContain("</script>", script);
        Assert.DoesNotContain("<b>", script);
    }
}

public class UserScriptCodeTests
{
    static bool Runs(string code, string protocol, string host, string port = "", bool top = true)
    {
        var engine = new Engine();
        engine.Execute($$"""
            var window = globalThis;
            window.location = { protocol: "{{protocol}}", hostname: "{{host}}", port: "{{port}}" };
            window.top = {{(top ? "window" : "{}")}};
            var ran = false;
            """);
        engine.Execute(code);
        return engine.Evaluate("ran").AsBoolean();
    }

    static OriginRule[] Rules(params string[] rules) => rules.Select(OriginRule.Parse).ToArray();

    [Fact]
    public void Without_guards_the_source_is_unchanged() =>
        Assert.Equal("ran = true;", UserScriptCode.Wrap("ran = true;", [], mainFrameOnly: false));

    [Theory]
    [InlineData("https:", "shop.example.com", "", true)]
    [InlineData("https:", "example.com", "", false)]
    [InlineData("https:", "example.com.evil.net", "", false)]
    [InlineData("http:", "shop.example.com", "", false)]
    [InlineData("https:", "shop.example.com", "8443", false)]
    [InlineData("https:", "app.local", "8443", true)]
    public void Origin_guard_matches_like_OriginRule(string protocol, string host, string port, bool expected)
    {
        var code = UserScriptCode.Wrap("ran = true;", Rules("https://*.example.com", "https://app.local:8443"), mainFrameOnly: false);

        Assert.Equal(expected, Runs(code, protocol, host, port));
    }

    [Fact]
    public void Main_frame_guard_skips_iframes()
    {
        var code = UserScriptCode.Wrap("ran = true;", [], mainFrameOnly: true);

        Assert.True(Runs(code, "https:", "a.com"));
        Assert.False(Runs(code, "https:", "a.com", top: false));
    }

    [Fact]
    public void Var_declarations_stay_global_inside_the_guard()
    {
        var code = UserScriptCode.Wrap("var helper = 42; ran = true;", Rules("https://a.com"), mainFrameOnly: true);
        var engine = new Engine();
        engine.Execute("var window = globalThis; window.top = window; window.location = { protocol: 'https:', hostname: 'a.com', port: '' };");
        engine.Execute(code);

        Assert.Equal(42, engine.Evaluate("window.helper").AsNumber());
    }

    [Fact]
    public void Style_sheets_are_combined_and_escaped()
    {
        Assert.Equal(string.Empty, UserScriptCode.StyleSheets(["", "  "]));

        var code = UserScriptCode.StyleSheets([".a { color: red }", ".b::after { content: \"</style>\" }"]);
        Assert.Contains(".a { color: red }", code);
        Assert.DoesNotContain("</style>", code);
    }
}
