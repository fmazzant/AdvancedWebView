using Foundation;
using ObjCRuntime;
using WebKit;

namespace Mafesoft.Maui.Controls.Handlers
{
    // iOS and Mac Catalyst: custom schemes (#55) through WKURLSchemeHandler, registered on the configuration before the
    // WKWebView is created. WebKit does not let apps intercept http and https: WebResourceRequested is raised for the
    // custom schemes only. Every scheme task gets exactly one answer, unless WebKit stops it first.
    public partial class AdvancedWebViewHandler
    {
        const int SchemeChunkSize = 64 * 1024;

        // Scheme tasks still running: WebKit throws when a stopped task is answered.
        readonly HashSet<NativeHandle> activeSchemeTasks = [];

        void SetUpCustomSchemes(WKWebViewConfiguration config, AdvancedWebView view)
        {
            foreach (var scheme in view.GetCustomSchemes())
            {
                try
                {
                    config.SetUrlSchemeHandler(new CustomSchemeHandler(this), scheme);
                }
                catch (Exception ex)
                {
                    // WebKit refuses the schemes it handles itself.
                    Log($"Could not register the custom scheme '{scheme}': {ex.Message}");
                }
            }
        }

        async void StartSchemeTask(IWKUrlSchemeTask task)
        {
            activeSchemeTasks.Add(task.Handle);
            try
            {
                var request = task.Request;
                var url = request.Url?.AbsoluteString ?? string.Empty;
                var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (request.Headers is { } fields)
                {
                    foreach (var (key, value) in fields)
                        headers[key.ToString()] = value?.ToString() ?? string.Empty;
                }

                WebResourceResponse? response = null;
                if (Advanced is { } view)
                {
                    var e = await view.RequestWebResourceAsync(url, request.HttpMethod ?? "GET", headers, WebResourceType.Unknown);
                    response = e?.Response;
                }

                await AnswerSchemeTaskAsync(task, request.Url, response ?? AdvancedWebView.CustomSchemeNotFound());
            }
            catch (Exception ex)
            {
                Log($"Custom scheme request failed: {ex.Message}");
                if (activeSchemeTasks.Contains(task.Handle))
                    task.DidFailWithError(new NSError(NSError.NSUrlErrorDomain, (nint)(long)NSUrlError.Unknown));
            }
            finally
            {
                activeSchemeTasks.Remove(task.Handle);
            }
        }

        // Runs on the main thread: the awaits come back to it.
        async Task AnswerSchemeTaskAsync(IWKUrlSchemeTask task, NSUrl? url, WebResourceResponse response)
        {
            using var content = response.Content;
            if (!activeSchemeTasks.Contains(task.Handle) || url is null)
                return;

            var headers = new NSMutableDictionary();
            foreach (var (name, value) in WebResourceRules.BuildHeaders(response))
                headers[new NSString(name)] = new NSString(value);

            task.DidReceiveResponse(new NSHttpUrlResponse(url, response.StatusCode, "HTTP/1.1", headers));

            if (content is not null)
            {
                var buffer = new byte[SchemeChunkSize];
                int read;
                while ((read = await content.ReadAsync(buffer)) > 0)
                {
                    if (!activeSchemeTasks.Contains(task.Handle))
                        return;
                    task.DidReceiveData(NSData.FromArray(buffer.AsSpan(0, read).ToArray()));
                }
            }

            if (activeSchemeTasks.Contains(task.Handle))
                task.DidFinish();
        }

        void StopSchemeTask(IWKUrlSchemeTask task) => activeSchemeTasks.Remove(task.Handle);

        sealed class CustomSchemeHandler : NSObject, IWKUrlSchemeHandler
        {
            readonly WeakReference<AdvancedWebViewHandler> handler;

            public CustomSchemeHandler(AdvancedWebViewHandler handler) =>
                this.handler = new WeakReference<AdvancedWebViewHandler>(handler);

            public void StartUrlSchemeTask(WKWebView webView, IWKUrlSchemeTask urlSchemeTask)
            {
                if (handler.TryGetTarget(out var owner))
                    owner.StartSchemeTask(urlSchemeTask);
                else
                    urlSchemeTask.DidFailWithError(new NSError(NSError.NSUrlErrorDomain, (nint)(long)NSUrlError.Cancelled));
            }

            public void StopUrlSchemeTask(WKWebView webView, IWKUrlSchemeTask urlSchemeTask)
            {
                if (handler.TryGetTarget(out var owner))
                    owner.StopSchemeTask(urlSchemeTask);
            }
        }
    }
}
