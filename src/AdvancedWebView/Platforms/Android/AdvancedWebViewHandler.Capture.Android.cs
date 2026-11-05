using Android.Graphics;

namespace Mafesoft.Maui.Controls.Handlers
{
    // Android: CaptureAsync draws the WebView into a bitmap.
    public partial class AdvancedWebViewHandler
    {
        internal partial Task<WebViewScreenshot> CapturePageAsync(WebCaptureArea area)
        {
            var webView = PlatformView ?? throw new InvalidOperationException("The web view is not ready.");
            if (webView.Width == 0 || webView.Height == 0)
                throw new InvalidOperationException("The web view is not laid out yet: capture it after it is shown.");

            Bitmap bitmap;
            if (area == WebCaptureArea.FullPage)
            {
                // Without enableSlowWholeDocumentDraw the WebView only draws the tiles on screen: the rest would be blank.
                if (!AdvancedWebView.IsFullPageCaptureEnabled)
                    throw new NotSupportedException(
                        "On Android, a full-page capture needs AdvancedWebView.EnableFullPageCapture(), called before the first web view is created (for example in MauiProgram).");

#pragma warning disable CA1422, CS0618 // Scale is the only public way to turn ContentHeight (CSS pixels) into view pixels.
                var contentHeight = (int)Math.Ceiling(webView.ContentHeight * webView.Scale);
#pragma warning restore CA1422, CS0618

                // Drawn without the parent's scroll translation: the page from the top.
                bitmap = Bitmap.CreateBitmap(webView.Width, Math.Max(contentHeight, webView.Height), Bitmap.Config.Argb8888!)!;
                webView.Draw(new Canvas(bitmap));
            }
            else
            {
                // The view draws its content in page coordinates: move to the visible part, as the parent does.
                bitmap = Bitmap.CreateBitmap(webView.Width, webView.Height, Bitmap.Config.Argb8888!)!;
                var canvas = new Canvas(bitmap);
                canvas.Translate(-webView.ScrollX, -webView.ScrollY);
                webView.Draw(canvas);
            }

            return Task.FromResult(new WebViewScreenshot(bitmap.Width, bitmap.Height, (stream, format, quality) =>
                bitmap.CompressAsync(format == ScreenshotFormat.Jpeg ? Bitmap.CompressFormat.Jpeg! : Bitmap.CompressFormat.Png!, quality, stream)));
        }
    }
}
