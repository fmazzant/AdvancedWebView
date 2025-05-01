using Android.App;
using Android.Graphics;
using Android.Views;
using Android.Webkit;
using Microsoft.Maui.Handlers;
using WebView = Android.Webkit.WebView;

namespace Mafesoft.Maui.Controls.Platforms.Android
{
    public class AdvancedWebViewHandler : WebViewHandler
    {
        protected override WebView CreatePlatformView()
        {
            var maui = VirtualView as AdvancedWebView;

            if (maui == null)
            {
                throw new InvalidOperationException("VirtualView must be of type AdvancedWebView.");
            }

            var webView = new WebView(Context);
            var settings = webView.Settings;

            // Configura WebView avanzata
            settings.JavaScriptEnabled = maui.JavaScriptEnabled;
            settings.DomStorageEnabled = true;
            settings.AllowFileAccess = true;
            settings.AllowContentAccess = true;
            settings.MixedContentMode = MixedContentHandling.AlwaysAllow;
            settings.JavaScriptCanOpenWindowsAutomatically = maui.JavaScriptCanOpenWindowsAutomatically;
            settings.SetSupportMultipleWindows(true);

            webView.SetWebViewClient(new CustomWebViewClient());
            webView.SetWebChromeClient(new CustomWebChromeClient());

            webView.OverScrollMode = maui.ScrollBouncesEnabled
                ? OverScrollMode.Always
                : OverScrollMode.Never;

            return webView;
        }

        protected override void ConnectHandler(WebView platformView)
        {
            base.ConnectHandler(platformView);
            if (VirtualView?.Source is UrlWebViewSource urlSource)
            {
                platformView.LoadUrl(urlSource.Url);
            }
        }
    }

    public class CustomWebViewClient : WebViewClient
    {
        public override void OnPageStarted(WebView view, string url, Bitmap favicon)
        {
            base.OnPageStarted(view, url, favicon);
            Console.WriteLine("Caricamento iniziato: " + url);
        }

        public override void OnPageFinished(WebView view, string url)
        {
            base.OnPageFinished(view, url);
            Console.WriteLine("Caricamento completato: " + url);
        }
    }

    public class CustomWebChromeClient : WebChromeClient
    {
        public override bool OnJsAlert(WebView view, string url, string message, JsResult result)
        {
            AlertDialog.Builder builder = new AlertDialog.Builder(view.Context);
            builder.SetMessage(message)
                   .SetPositiveButton("OK", (sender, args) => result.Confirm())
                   .Create()
                   .Show();
            return true;
        }
    }

}
