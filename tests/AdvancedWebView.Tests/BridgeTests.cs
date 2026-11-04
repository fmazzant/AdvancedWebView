using System.Text.Json;

namespace Mafesoft.Maui.Controls.Tests;

/// <summary>End-to-end tests: the bridge script in Jint talking to the real C# core.</summary>
public class BridgeTests
{
    sealed record Setup(WebViewBridge Bridge, FakePage Page, BridgeOptions Options, List<WebMessageReceivedEventArgs> Events, List<string> Logs);

    static Setup Create(BridgeOptions? options = null, bool android = false)
    {
        options ??= new BridgeOptions { AllowedOrigins = ["https://example.com"] };
        var page = new FakePage(android);
        var events = new List<WebMessageReceivedEventArgs>();
        var logs = new List<string>();
        var bridge = new WebViewBridge(options, page.Enqueue, events.Add, logs.Add);
        page.Bridge = bridge;
        return new Setup(bridge, page, options, events, logs);
    }

    /// <summary>Calls AdvancedWebView.invoke in the page and returns how the Promise settled.</summary>
    static async Task<JsonElement> InvokeAsync(FakePage page, string method, string argsJs = "undefined", int settleMs = 200)
    {
        page.Execute($$"""
            var __r = null;
            AdvancedWebView.invoke({{BridgeProtocol.JsonString(method)}}, {{argsJs}}).then(
              function (v) { __r = { ok: true, value: v }; },
              function (e) { __r = { ok: false, code: e.code, message: e.message, details: e.details || null }; });
            """);
        await page.SettleAsync(settleMs);
        var json = page.Get<string>("JSON.stringify(__r)");
        return JsonDocument.Parse(json).RootElement;
    }

    static WebViewBridge.Handler Json(Func<JsonElement, object?> f) =>
        (args, ct) => Task.FromResult<string?>(JsonSerializer.Serialize(f(args)));

    [Fact]
    public void Script_is_idempotent_and_read_only()
    {
        var page = new FakePage();
        page.Execute("var first = AdvancedWebView;");
        page.Execute(BridgeScript.Source);
        page.Execute("try { AdvancedWebView = 1; } catch (e) { }");

        Assert.True(page.Get<bool>("first === AdvancedWebView"));
        Assert.True(page.Get<bool>("Object.isFrozen(AdvancedWebView)"));
    }

    [Fact]
    public async Task PostMessage_raises_MessageReceived_with_text_json_and_origin()
    {
        var s = Create();
        s.Page.Execute("""
            AdvancedWebView.postMessage({ type: "checkout", total: 42.5 });
            AdvancedWebView.postMessage("hello");
            AdvancedWebView.postMessage();
            """);
        await s.Page.SettleAsync(50);

        Assert.Equal(3, s.Events.Count);
        Assert.Equal("""{"type":"checkout","total":42.5}""", s.Events[0].Message);
        Assert.Equal(42.5, JsonDocument.Parse(s.Events[0].Json).RootElement.GetProperty("total").GetDouble());
        Assert.Equal("hello", s.Events[1].Message);
        Assert.Equal("\"hello\"", s.Events[1].Json);
        Assert.Equal("null", s.Events[2].Message);
        Assert.Equal(new Uri("https://example.com"), s.Events[0].Origin);
        Assert.True(s.Events[0].IsMainFrame);
    }

    [Fact]
    public async Task Messages_from_CSharp_reach_page_listeners()
    {
        var s = Create();
        s.Page.Execute("var got = []; AdvancedWebView.addEventListener('message', function (e) { got.push(e.data); });");

        await s.Page.Enqueue(BridgeProtocol.ReceiveScript(BridgeProtocol.MessageEnvelope("""{"hello":"world"}""")));
        var tricky = "quote \" </script> " + (char)0x2028 + " è";
        await s.Page.Enqueue(BridgeProtocol.ReceiveScript(BridgeProtocol.MessageEnvelope(BridgeProtocol.JsonString(tricky))));
        await s.Page.SettleAsync(30);

        Assert.Equal("world", s.Page.Get<string>("got[0].hello"));
        Assert.Equal(tricky, s.Page.Get<string>("got[1]"));
    }

    [Fact]
    public async Task Invoke_resolves_with_the_handler_result()
    {
        var s = Create();
        s.Bridge.SetHandler("add", Json(a => new { sum = a.GetProperty("a").GetInt32() + a.GetProperty("b").GetInt32() }));

        var r = await InvokeAsync(s.Page, "add", "{ a: 2, b: 3 }");

        Assert.True(r.GetProperty("ok").GetBoolean());
        Assert.Equal(5, r.GetProperty("value").GetProperty("sum").GetInt32());
    }

    [Fact]
    public async Task Invoke_without_result_resolves_null()
    {
        var s = Create();
        s.Bridge.SetHandler("logout", (_, _) => Task.FromResult<string?>(null));

        var r = await InvokeAsync(s.Page, "logout");

        Assert.True(r.GetProperty("ok").GetBoolean());
        Assert.Equal(JsonValueKind.Null, r.GetProperty("value").ValueKind);
    }

    [Fact]
    public async Task Concurrent_invokes_resolve_the_right_promise()
    {
        var s = Create();
        s.Bridge.SetHandler("echo", async (a, ct) =>
        {
            await Task.Delay(a.GetInt32() * 10, ct);
            return a.GetRawText();
        });

        s.Page.Execute("""
            var out = [];
            [5, 1, 3].forEach(function (n) { AdvancedWebView.invoke("echo", n).then(function (v) { out.push(n + "=" + v); }); });
            """);
        await s.Page.SettleAsync(200);

        var results = s.Page.Get<string>("out.sort().join(',')");
        Assert.Equal("1=1,3=3,5=5", results);
    }

    [Fact]
    public async Task Exceptions_reject_with_HandlerError_and_message_only()
    {
        var s = Create();
        s.Bridge.SetHandler("boom", (_, _) => throw new InvalidOperationException("kaboom"));

        var r = await InvokeAsync(s.Page, "boom");

        Assert.False(r.GetProperty("ok").GetBoolean());
        Assert.Equal(BridgeErrorCodes.HandlerError, r.GetProperty("code").GetString());
        Assert.Equal("kaboom", r.GetProperty("message").GetString());
        Assert.Equal(JsonValueKind.Null, r.GetProperty("details").ValueKind);
    }

    [Fact]
    public async Task Exception_details_only_when_enabled()
    {
        var s = Create(new BridgeOptions { AllowedOrigins = ["https://example.com"], IncludeExceptionDetails = true });
        s.Bridge.SetHandler("boom", (_, _) => throw new InvalidOperationException("kaboom"));

        var r = await InvokeAsync(s.Page, "boom");

        Assert.Contains("InvalidOperationException", r.GetProperty("details").GetString());
    }

    [Fact]
    public async Task BridgeException_sets_the_error_code()
    {
        var s = Create();
        s.Bridge.SetHandler("getUser", (_, _) => throw new BridgeException("NotFound", "User 7 does not exist."));

        var r = await InvokeAsync(s.Page, "getUser", "{ id: 7 }");

        Assert.Equal("NotFound", r.GetProperty("code").GetString());
        Assert.Equal("User 7 does not exist.", r.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Unknown_method_rejects_with_MethodNotFound()
    {
        var s = Create();

        var r = await InvokeAsync(s.Page, "missing");

        Assert.Equal(BridgeErrorCodes.MethodNotFound, r.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Slow_handler_rejects_with_Timeout_and_is_cancelled()
    {
        var s = Create(new BridgeOptions { AllowedOrigins = ["https://example.com"], InvokeTimeout = TimeSpan.FromMilliseconds(100) });
        var cancelled = new TaskCompletionSource();
        s.Bridge.SetHandler("slow", async (_, ct) =>
        {
            ct.Register(() => cancelled.TrySetResult());
            await Task.Delay(Timeout.Infinite, CancellationToken.None);
            return null;
        });

        var r = await InvokeAsync(s.Page, "slow", settleMs: 400);

        Assert.Equal(BridgeErrorCodes.Timeout, r.GetProperty("code").GetString());
        Assert.Contains("0.1 seconds", r.GetProperty("message").GetString());
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task New_page_cancels_calls_of_the_previous_page()
    {
        var s = Create(new BridgeOptions { AllowedOrigins = ["https://example.com"], InvokeTimeout = Timeout.InfiniteTimeSpan });
        var cancelled = new TaskCompletionSource();
        s.Bridge.SetHandler("long", async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct).ContinueWith(_ => cancelled.TrySetResult());
            return "\"late\"";
        });

        s.Page.Execute("var settled = false; AdvancedWebView.invoke('long').then(function () { settled = true; }, function () { settled = true; });");
        await s.Page.SettleAsync(30);

        // The next page load has a new pageId.
        s.Bridge.OnMessageFromPage("""{"type":"message","pageId":"next-page","data":1}""", s.Page.Origin, isMainFrame: true);

        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        await s.Page.SettleAsync(50);
        Assert.False(s.Page.Get<bool>("settled"));
    }

    [Fact]
    public async Task Remote_origin_is_blocked_by_default()
    {
        var s = Create(new BridgeOptions());
        s.Bridge.SetHandler("add", Json(_ => 1));

        s.Page.Execute("AdvancedWebView.postMessage('hi');");
        var r = await InvokeAsync(s.Page, "add");

        Assert.Empty(s.Events);
        Assert.Equal(BridgeErrorCodes.OriginNotAllowed, r.GetProperty("code").GetString());
        Assert.Contains(s.Logs, l => l.Contains("https://example.com") && l.Contains("AllowedOrigins"));
    }

    [Fact]
    public async Task Local_content_is_allowed_by_default()
    {
        var s = Create(new BridgeOptions());
        s.Page.Origin = null;
        s.Page.Execute("AdvancedWebView.postMessage('local');");
        await s.Page.SettleAsync(30);
        Assert.Single(s.Events);

        var file = Create(new BridgeOptions());
        file.Page.Origin = new Uri("file:///android_asset/");
        file.Page.Execute("AdvancedWebView.postMessage('file');");
        await file.Page.SettleAsync(30);
        Assert.Single(file.Events);
    }

    [Fact]
    public async Task Wildcard_rule_allows_subdomains_only()
    {
        var s = Create(new BridgeOptions { AllowedOrigins = ["https://*.example.com"] });

        s.Page.Origin = new Uri("https://shop.example.com");
        s.Page.Execute("AdvancedWebView.postMessage(1);");
        s.Page.Origin = new Uri("https://example.com");
        s.Page.Execute("AdvancedWebView.postMessage(2);");
        s.Page.Origin = new Uri("https://example.com.evil.net");
        s.Page.Execute("AdvancedWebView.postMessage(3);");
        await s.Page.SettleAsync(30);

        Assert.Equal(["1"], s.Events.Select(e => e.Message));
    }

    [Fact]
    public async Task Iframes_are_ignored_unless_allowed()
    {
        var s = Create();
        s.Bridge.SetHandler("add", Json(_ => 7));
        s.Page.IsMainFrame = false;
        s.Page.UseReplyChannel = true;

        s.Page.Execute("AdvancedWebView.postMessage('from iframe');");
        await s.Page.SettleAsync(30);
        Assert.Empty(s.Events);

        s.Options.AllowSubframes = true;
        var r = await InvokeAsync(s.Page, "add");

        Assert.True(r.GetProperty("ok").GetBoolean());
        Assert.Equal(7, r.GetProperty("value").GetInt32());
    }

    [Fact]
    public async Task Android_replies_through_the_reply_proxy()
    {
        var s = Create(android: true);
        s.Page.UseReplyChannel = true;
        s.Bridge.SetHandler("add", Json(_ => 3));

        var r = await InvokeAsync(s.Page, "add");

        Assert.Equal(3, r.GetProperty("value").GetInt32());
    }

    [Fact]
    public async Task CallFunction_returns_the_result()
    {
        var s = Create();
        s.Page.Execute("var cart = { items: [1, 2], getTotal: function (tax) { return this.items.length * 10 + tax; } };");

        var call = s.Bridge.CallFunctionAsync("cart.getTotal", "[5]");
        await s.Page.SettleAsync(30);

        Assert.Equal("25", await call);
    }

    [Fact]
    public async Task CallFunction_awaits_promises_and_maps_undefined_to_null()
    {
        var s = Create();
        s.Page.Execute("""
            var app = {
              later: function (x) { return Promise.resolve({ x: x }); },
              nothing: function () { }
            };
            """);

        var later = s.Bridge.CallFunctionAsync("app.later", """["a \"quoted\"\nline"]""");
        var nothing = s.Bridge.CallFunctionAsync("app.nothing", "[]");
        await s.Page.SettleAsync(30);

        Assert.Equal("a \"quoted\"\nline", JsonDocument.Parse((await later)!).RootElement.GetProperty("x").GetString());
        Assert.Null(await nothing);
    }

    [Fact]
    public async Task CallFunction_errors_become_JavaScriptException()
    {
        var s = Create();
        s.Page.Execute("""
            var app = {
              fail: function () { throw new Error("bad input"); },
              reject: function () { return Promise.reject(new Error("rejected")); },
              circular: function () { var o = {}; o.self = o; return o; }
            };
            """);

        var fail = s.Bridge.CallFunctionAsync("app.fail", "[]");
        var reject = s.Bridge.CallFunctionAsync("app.reject", "[]");
        var missing = s.Bridge.CallFunctionAsync("app.missing", "[]");
        var circular = s.Bridge.CallFunctionAsync("app.circular", "[]");
        await s.Page.SettleAsync(30);

        Assert.Equal("bad input", (await Assert.ThrowsAsync<JavaScriptException>(() => fail)).Message);
        Assert.Equal("rejected", (await Assert.ThrowsAsync<JavaScriptException>(() => reject)).Message);
        Assert.Contains("is not a function", (await Assert.ThrowsAsync<JavaScriptException>(() => missing)).Message);
        await Assert.ThrowsAsync<JavaScriptException>(() => circular);
    }

    [Fact]
    public async Task CallFunction_works_on_any_origin()
    {
        var s = Create(new BridgeOptions()); // no remote origin allowed
        s.Page.Execute("var app = { v: function () { return 1; } };");

        var call = s.Bridge.CallFunctionAsync("app.v", "[]");
        await s.Page.SettleAsync(30);

        Assert.Equal("1", await call);
    }

    [Fact]
    public async Task CallFunction_times_out_without_bridge()
    {
        var page = new FakePage(injectBridge: false);
        var bridge = new WebViewBridge(new BridgeOptions { InvokeTimeout = TimeSpan.FromMilliseconds(100) }, page.Enqueue, _ => { });
        page.Bridge = bridge;

        var call = bridge.CallFunctionAsync("app.v", "[]");
        await page.SettleAsync(30);

        await Assert.ThrowsAsync<TimeoutException>(() => call);
    }

    [Fact]
    public async Task Reset_cancels_pending_calls()
    {
        var s = Create(new BridgeOptions { InvokeTimeout = Timeout.InfiniteTimeSpan });
        s.Page.Execute("var app = { never: function () { return new Promise(function () { }); } };");

        var call = s.Bridge.CallFunctionAsync("app.never", "[]");
        await s.Page.SettleAsync(30);
        s.Bridge.Reset();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call);
    }
}
