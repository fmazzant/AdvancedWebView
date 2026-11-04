using Foundation;
using WebKit;

namespace Mafesoft.Maui.Controls.Handlers
{
    // iOS: page state (progress, loading, title, URL) observed on the WKWebView with KVO.
    public partial class AdvancedWebViewHandler
    {
        readonly List<IDisposable> pageStateObservers = [];

        /// <inheritdoc/>
        protected override void ConnectHandler(WKWebView platformView)
        {
            base.ConnectHandler(platformView);

            // KVO notifications for these WKWebView properties arrive on the main thread.
            Observe(platformView, "estimatedProgress", (view, web) => view.SetProgress(web.EstimatedProgress));
            Observe(platformView, "loading", (view, web) => view.SetIsLoading(web.IsLoading));
            Observe(platformView, "title", (view, web) => view.SetTitle(web.Title));
            // Also changes on history.pushState / replaceState.
            Observe(platformView, "URL", (view, web) => view.SetCurrentUrl(web.Url?.AbsoluteString));

            // Element fullscreen (iOS 16+). The native video player's fullscreen is not reported by WebKit.
            if (OperatingSystem.IsIOSVersionAtLeast(16))
                Observe(platformView, "fullscreenState", (view, web) =>
                {
                    // Repeated inside the callback: the analyzer does not carry the check above into the lambda.
                    if (OperatingSystem.IsIOSVersionAtLeast(16))
                        view.SetFullscreen(web.FullscreenState == WKFullscreenState.InFullscreen);
                });
        }

        void Observe(WKWebView platformView, string keyPath, Action<AdvancedWebView, WKWebView> update)
        {
            var webView = new WeakReference<WKWebView>(platformView);
            pageStateObservers.Add(platformView.AddObserver(keyPath, NSKeyValueObservingOptions.Initial | NSKeyValueObservingOptions.New, _ =>
            {
                if (Advanced is { } view && webView.TryGetTarget(out var web))
                    update(view, web);
            }));
        }

        void StopObservingPageState()
        {
            foreach (var observer in pageStateObservers)
                observer.Dispose();
            pageStateObservers.Clear();
        }
    }
}
