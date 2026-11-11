using Foundation;
using WebKit;

namespace Mafesoft.Maui.Controls.Handlers
{
    // iOS / Mac Catalyst: the web content process ended (#53), for example killed by the system for memory. WebKit leaves
    // a blank page; the WKWebView still works and starts a new process on the next load. WebKit does not say why.
    public partial class AdvancedWebViewHandler
    {
        void OnWebContentProcessTerminated(WKWebView webView)
        {
            if (Advanced is not { } view)
                return;

            var url = view.CurrentUrl ?? webView.Url?.AbsoluteString;
            if (!view.OnWebProcessTerminated(WebProcessTerminationReason.Unknown, url))
                return;

            if (webView.Url is not null)
                webView.Reload();
            else if (!string.IsNullOrEmpty(url) && NSUrl.FromString(url) is { } nsUrl)
                webView.LoadRequest(new NSUrlRequest(nsUrl));
        }

        // Other parts: AdvancedWebViewHandler.NewWindow.iOS.cs and AdvancedWebViewHandler.Errors.iOS.cs.
        sealed partial class AdvancedNavigationDelegate
        {
            // MAUI exports this selector on MauiWKWebView, not on its navigation delegate, which this class replaces.
            [Export("webViewWebContentProcessDidTerminate:")]
            public void ContentProcessDidTerminate(WKWebView webView)
            {
                if (handler.TryGetTarget(out var owner))
                    owner.OnWebContentProcessTerminated(webView);
            }
        }
    }
}
