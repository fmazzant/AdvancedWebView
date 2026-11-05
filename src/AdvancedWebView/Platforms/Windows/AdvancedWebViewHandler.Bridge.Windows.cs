using Microsoft.Web.WebView2.Core;

namespace Mafesoft.Maui.Controls.Handlers
{
    // Windows transport of the JS bridge (chrome.webview.postMessage and web messages back) and of user scripts
    // (AddScriptToExecuteOnDocumentCreatedAsync, which runs in every frame).
    public partial class AdvancedWebViewHandler
    {
        readonly List<string> documentScriptIds = [];
        List<string> pageLoadScripts = [];
        int userScriptsVersion;

        void SetUpBridge(CoreWebView2 target)
        {
            target.WebMessageReceived += OnWebMessageReceived;
            target.FrameCreated += OnFrameCreated;
            UpdateUserScripts();
        }

        void TearDownBridge(CoreWebView2 target)
        {
            target.WebMessageReceived -= OnWebMessageReceived;
            target.FrameCreated -= OnFrameCreated;
            userScriptsVersion++;
            documentScriptIds.Clear();
            pageLoadScripts = [];
        }

        // Top-level page. Replies go back as a web message to the page.
        void OnWebMessageReceived(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
        {
            if (ReadMessage(args.TryGetWebMessageAsString) is not { } text)
                return;

            Advanced?.BridgeCore.OnMessageFromPage(text, ToOrigin(args.Source), isMainFrame: true,
                envelope => MainThread.InvokeOnMainThreadAsync(() => sender.PostWebMessageAsString(envelope)));
        }

        // Iframes post to their own CoreWebView2Frame; replies go back to the same frame.
        void OnFrameCreated(CoreWebView2 sender, CoreWebView2FrameCreatedEventArgs args)
        {
            var frame = args.Frame;
            frame.WebMessageReceived += (f, e) =>
            {
                if (ReadMessage(e.TryGetWebMessageAsString) is not { } text)
                    return;

                Advanced?.BridgeCore.OnMessageFromPage(text, ToOrigin(e.Source), isMainFrame: false,
                    envelope => MainThread.InvokeOnMainThreadAsync(() => f.PostWebMessageAsString(envelope)));
            };
        }

        // The bridge posts strings; anything else (a page calling chrome.webview.postMessage with an object) is not ours.
        static string? ReadMessage(Func<string> read)
        {
            try
            {
                return read();
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        internal partial void UpdateUserScripts()
        {
            if (core is { } target)
                _ = ApplyUserScriptsAsync(target);
        }

        async Task ApplyUserScriptsAsync(CoreWebView2 target)
        {
            var version = ++userScriptsVersion;

            foreach (var id in documentScriptIds)
                target.RemoveScriptToExecuteOnDocumentCreated(id);
            documentScriptIds.Clear();

            // The bridge goes into every frame; the core decides whether iframes may use it (Bridge.AllowSubframes).
            var documentStart = new List<string> { BridgeScript.Source };
            var onLoad = new List<string>();

            if (Advanced is { } view)
            {
                foreach (var script in view.ResolveUserScripts())
                {
                    if (script.InjectionTime == UserScriptInjectionTime.DocumentStart)
                        // Injected into every frame; the guard keeps main-frame-only scripts out of iframes.
                        documentStart.Add(UserScriptCode.Wrap(script.Code, [], script.MainFrameOnly));
                    else
                        // DocumentEnd scripts run when the page has loaded (top-level page only), as on Android.
                        onLoad.Add(script.Code);
                }
            }

            pageLoadScripts = onLoad;

            // One at a time, so the scripts run in order.
            foreach (var code in documentStart)
            {
                try
                {
                    var id = await target.AddScriptToExecuteOnDocumentCreatedAsync(code);
                    if (version == userScriptsVersion && ReferenceEquals(core, target))
                        documentScriptIds.Add(id);
                    else
                        target.RemoveScriptToExecuteOnDocumentCreated(id); // replaced by a newer update meanwhile
                }
                catch (Exception ex)
                {
                    Log($"Could not add a script to the page: {ex.Message}");
                }
            }
        }

        partial void OnPageLoadedCore()
        {
            foreach (var script in pageLoadScripts)
                ExecuteScript(script);
        }
    }
}
