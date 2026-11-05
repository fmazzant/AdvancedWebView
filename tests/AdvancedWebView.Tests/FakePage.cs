using System.Collections.Concurrent;
using Jint;
using Jint.Native;

namespace Mafesoft.Maui.Controls.Tests;

/// <summary>
/// A page (one frame) running the real bridge script in Jint. What the page posts goes to <see cref="WebViewBridge"/>;
/// what C# sends back is queued and executed by <see cref="SettleAsync"/> on the test thread (Jint is not thread-safe).
/// </summary>
/// <summary>The native transport the fake page exposes to the bridge script.</summary>
internal enum FakeTransport
{
    Ios,
    Android,
    Windows,
}

internal sealed class FakePage
{
    readonly Engine engine = new(o => o.Strict(false));
    readonly ConcurrentQueue<string> inbox = new();
    readonly FakeTransport transport;

    public FakePage(FakeTransport transport = FakeTransport.Ios, bool injectBridge = true)
    {
        this.transport = transport;
        engine.SetValue("__post", new Action<string>(text => Posted.Add(text)));
        engine.Execute("""
            var window = globalThis;
            window.top = window;
            window.location = { protocol: "https:", hostname: "example.com", port: "" };
            window.setTimeout = function (f) { };
            """);

        engine.Execute(transport switch
        {
            FakeTransport.Android => "window.__advancedWebViewNative = { postMessage: function (t) { __post(t); } };",
            // WebView2: window.chrome.webview is an event target for the host's web messages.
            FakeTransport.Windows => """
                var __webMessageListeners = [];
                window.chrome = { webview: {
                  postMessage: function (t) { __post(t); },
                  addEventListener: function (type, l) { if (type === "message") { __webMessageListeners.push(l); } }
                } };
                """,
            _ => "window.webkit = { messageHandlers: { advancedWebView: { postMessage: function (t) { __post(t); } } } };",
        });

        if (injectBridge)
            engine.Execute(BridgeScript.Source);
    }

    /// <summary>Strings the page posted that the bridge has not consumed yet.</summary>
    public List<string> Posted { get; } = [];

    public WebViewBridge? Bridge { get; set; }

    public Uri? Origin { get; set; } = new("https://example.com");

    public bool IsMainFrame { get; set; } = true;

    /// <summary>Use the frame's own reply channel (Android replyProxy / iOS frame evaluate) instead of the top-level page.</summary>
    public bool UseReplyChannel { get; set; }

    /// <summary>Queues a script C# wants to run in this page.</summary>
    public Task Enqueue(string script)
    {
        inbox.Enqueue(script);
        return Task.CompletedTask;
    }

    public void Execute(string js)
    {
        engine.Execute(js);
        Pump();
    }

    public JsValue Evaluate(string js) => engine.Evaluate(js);

    public T Get<T>(string js) => (T)engine.Evaluate(js).ToObject()!;

    /// <summary>Delivers posted messages to C#, runs C# replies in the page, and processes Promise jobs until nothing is left.</summary>
    public async Task SettleAsync(int milliseconds = 200)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(milliseconds);
        do
        {
            Pump();
            await Task.Delay(10);
        }
        while (DateTime.UtcNow < deadline);
        Pump();
    }

    void Pump()
    {
        for (var round = 0; round < 50; round++)
        {
            var worked = false;

            while (Posted.Count > 0)
            {
                var text = Posted[0];
                Posted.RemoveAt(0);
                Func<string, Task>? reply = UseReplyChannel ? ReplyToThisFrame : null;
                Bridge?.OnMessageFromPage(text, Origin, IsMainFrame, reply);
                worked = true;
            }

            while (inbox.TryDequeue(out var script))
            {
                engine.Execute(script);
                worked = true;
            }

            engine.Advanced.ProcessTasks();

            if (!worked && Posted.Count == 0 && inbox.IsEmpty)
                return;
        }
    }

    Task ReplyToThisFrame(string envelope) =>
        Enqueue(transport switch
        {
            FakeTransport.Android => "window.__advancedWebViewNative.onmessage({ data: " + BridgeProtocol.JsonString(envelope) + " });",
            FakeTransport.Windows => "__webMessageListeners.forEach(function (l) { l({ data: " + BridgeProtocol.JsonString(envelope) + " }); });",
            _ => BridgeProtocol.ReceiveScript(envelope),
        });
}
