using Microsoft.Maui.Platform;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;

namespace Mafesoft.Maui.Controls.Handlers
{
    // Windows: a WebView2 process ended (#53). When the renderer ends, the CoreWebView2 still works and the page is
    // loaded again. When the browser process ends, the WebView2 control cannot be used any more: the control gets a new
    // handler with a new WebView2, in the same place of the layout. Other processes (GPU, utility, iframes) are
    // restarted by WebView2 itself.
    public partial class AdvancedWebViewHandler
    {
        void SetUpProcessTermination(CoreWebView2 target) => target.ProcessFailed += OnProcessFailed;

        void TearDownProcessTermination(CoreWebView2 target) => target.ProcessFailed -= OnProcessFailed;

        void OnProcessFailed(CoreWebView2 sender, CoreWebView2ProcessFailedEventArgs args)
        {
            if (Advanced is not { } view ||
                args.ProcessFailedKind is not (CoreWebView2ProcessFailedKind.RenderProcessExited or CoreWebView2ProcessFailedKind.BrowserProcessExited))
                return;

            var reason = args.Reason switch
            {
                CoreWebView2ProcessFailedReason.Crashed => WebProcessTerminationReason.Crashed,
                CoreWebView2ProcessFailedReason.Terminated or CoreWebView2ProcessFailedReason.OutOfMemory => WebProcessTerminationReason.KilledByOs,
                _ => WebProcessTerminationReason.Unknown,
            };
            var url = view.CurrentUrl;

            if (args.ProcessFailedKind == CoreWebView2ProcessFailedKind.RenderProcessExited)
            {
                if (view.OnWebProcessTerminated(reason, url))
                    sender.Reload();
                else
                    sender.Navigate("about:blank");
                return;
            }

            if (MauiContext is { } context)
                PlatformView.DispatcherQueue.TryEnqueue(() => ReplaceDeadWebView(view, context, reason, url));
        }

        static void ReplaceDeadWebView(AdvancedWebView view, IMauiContext context, WebProcessTerminationReason reason, string? url)
        {
            if (view.Handler is not AdvancedWebViewHandler old)
                return;

            FrameworkElement oldView = old.ContainerView ?? old.PlatformView;
            var parent = oldView.Parent as Panel;
            var index = parent?.Children.IndexOf(oldView) ?? -1;

            view.Handler = null;
            if (parent is not null && index >= 0 && index < parent.Children.Count && ReferenceEquals(parent.Children[index], oldView))
                parent.Children.RemoveAt(index);

            var replacement = view.ToPlatform(context);
            if (parent is not null && replacement.Parent is null)
                parent.Children.Insert(index < 0 ? parent.Children.Count : Math.Min(index, parent.Children.Count), replacement);

            var reload = view.OnWebProcessTerminated(reason, url);
            if (reload && !string.IsNullOrEmpty(url) && view.Handler is AdvancedWebViewHandler fresh)
                fresh.LoadRequest(new WebRequest(url, new Dictionary<string, string>()));
        }
    }
}
