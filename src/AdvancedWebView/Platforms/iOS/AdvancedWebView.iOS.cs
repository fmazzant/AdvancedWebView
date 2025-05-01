using Foundation;
using Microsoft.Maui.Handlers;
using UIKit;
using WebKit;

namespace Mafesoft.Maui.Controls.Platforms.iOS
{
    public class AdvancedWebViewHandler : WebViewHandler
    {
        protected override WKWebView CreatePlatformView()
        {
            var maui = VirtualView as AdvancedWebView;

            if (maui == null)
            {
                throw new InvalidOperationException("VirtualView must be of type AdvancedWebView.");
            }

            var config = new WKWebViewConfiguration();

            if (UIDevice.CurrentDevice.CheckSystemVersion(11, 0))
            {
                if (UIDevice.CurrentDevice.CheckSystemVersion(14, 0))
                {
                    config.DefaultWebpagePreferences.AllowsContentJavaScript = maui.JavaScriptEnabled;
                }
                else
                {
                    config.Preferences.JavaScriptEnabled = maui.JavaScriptEnabled;
                }
            }

            config.Preferences.JavaScriptCanOpenWindowsAutomatically = maui.JavaScriptCanOpenWindowsAutomatically;
            config.AllowsInlineMediaPlayback = true;
            config.MediaTypesRequiringUserActionForPlayback = WKAudiovisualMediaTypes.None;

            var webView = new WKWebView(maui.Frame, config)
            {
                NavigationDelegate = new CustomWebViewDelegate(),
                UIDelegate = new CustomUIDelegate(),
            };

            webView.ScrollView.Bounces = maui.ScrollBouncesEnabled;

            return webView;
        }

        protected override void ConnectHandler(WKWebView platformView)
        {
            base.ConnectHandler(platformView);
            if (VirtualView?.Source is UrlWebViewSource urlSource)
            {
                platformView.LoadRequest(new NSUrlRequest(new NSUrl(urlSource.Url)));
            }
        }
    }

    public class CustomWebViewDelegate : WKNavigationDelegate
    {
        public override void DidStartProvisionalNavigation(WKWebView webView, WKNavigation navigation)
        {
            Console.WriteLine("Inizio caricamento...");

        }

        public override void DidFinishNavigation(WKWebView webView, WKNavigation navigation)
        {
            Console.WriteLine("Caricamento completato.");
        }
    }

    public class CustomUIDelegate : WKUIDelegate
    {
        public override void RunJavaScriptAlertPanel(WKWebView webView, string message, WKFrameInfo frame, Action completionHandler)
        {
            var alertController = UIAlertController.Create("Messaggio", message, UIAlertControllerStyle.Alert);
            alertController.AddAction(UIAlertAction.Create("OK", UIAlertActionStyle.Default, _ => completionHandler()));

            var vc = UIApplication.SharedApplication.KeyWindow.RootViewController;
            vc?.PresentViewController(alertController, true, null);
        }
    }
}
