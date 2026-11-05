using Microsoft.UI.Windowing;
using Microsoft.Web.WebView2.Core;

namespace Mafesoft.Maui.Controls.Handlers
{
    // Windows: element fullscreen (video players, element.requestFullscreen). WebView2 only fills its own bounds:
    // the app window goes fullscreen too, and FullscreenChanged lets the app hide the rest of the page.
    public partial class AdvancedWebViewHandler
    {
        AppWindow? fullscreenWindow;
        AppWindowPresenterKind previousPresenter;

        void SetUpFullscreen(CoreWebView2 target) => target.ContainsFullScreenElementChanged += OnContainsFullScreenElementChanged;

        void TearDownFullscreen(CoreWebView2 target)
        {
            target.ContainsFullScreenElementChanged -= OnContainsFullScreenElementChanged;
            SetWindowFullscreen(false);
            Advanced?.SetFullscreen(false);
        }

        void OnContainsFullScreenElementChanged(CoreWebView2 sender, object args)
        {
            if (Advanced is not { } view)
                return;

            var isFullscreen = sender.ContainsFullScreenElement;
            if (isFullscreen && !view.FullscreenEnabled)
            {
                // WebView2 cannot refuse the request: leave fullscreen right away.
                ExecuteScript("if (document.fullscreenElement) { document.exitFullscreen(); }");
                return;
            }

            SetWindowFullscreen(isFullscreen);
            view.SetFullscreen(isFullscreen);
        }

        void SetWindowFullscreen(bool isFullscreen)
        {
            if (isFullscreen)
            {
                if (fullscreenWindow is not null || (Advanced?.Window?.Handler?.PlatformView as Microsoft.UI.Xaml.Window)?.AppWindow is not { } window)
                    return;

                fullscreenWindow = window;
                previousPresenter = window.Presenter.Kind;
                if (previousPresenter != AppWindowPresenterKind.FullScreen)
                    window.SetPresenter(AppWindowPresenterKind.FullScreen);
            }
            else if (fullscreenWindow is { } window)
            {
                fullscreenWindow = null;
                if (previousPresenter != AppWindowPresenterKind.FullScreen)
                    window.SetPresenter(previousPresenter);
            }
        }
    }
}
