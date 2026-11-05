using CoreGraphics;
using Foundation;
using UIKit;
using WebKit;

namespace Mafesoft.Maui.Controls.Handlers
{
    // iOS and Mac Catalyst: the visible part with takeSnapshot; the whole page with createPDF (iOS 14 or later),
    // drawn into an image. takeSnapshot does not render the parts of the page off screen.
    public partial class AdvancedWebViewHandler
    {
        internal partial async Task<WebViewScreenshot> CapturePageAsync(WebCaptureArea area)
        {
            var webView = PlatformView ?? throw new InvalidOperationException("The web view is not ready.");
            if (webView.Bounds.Width <= 0 || webView.Bounds.Height <= 0)
                throw new InvalidOperationException("The web view is not laid out yet: capture it after it is shown.");

            UIImage image;
            if (area == WebCaptureArea.FullPage)
            {
                if (!OperatingSystem.IsIOSVersionAtLeast(14))
                    throw new NotSupportedException("A full-page capture needs iOS 14 or later.");

                // The default configuration (no rect) covers the whole content of the web view.
                var pdf = await webView.CreatePdfAsync(new WKPdfConfiguration());
                image = RenderFirstPage(pdf, webView.Window?.Screen.Scale ?? UIScreen.MainScreen.Scale);
            }
            else
            {
                var config = new WKSnapshotConfiguration { AfterScreenUpdates = true };
                image = await webView.TakeSnapshotAsync(config);
            }

            var width = (int)Math.Round(image.Size.Width * image.CurrentScale);
            var height = (int)Math.Round(image.Size.Height * image.CurrentScale);
            return new WebViewScreenshot(width, height, async (stream, format, quality) =>
            {
                using var data = format == ScreenshotFormat.Jpeg ? image.AsJPEG(quality / 100f) : image.AsPNG();
                if (data is null)
                    throw new InvalidOperationException("The screenshot could not be encoded.");
                using var source = data.AsStream();
                await source.CopyToAsync(stream);
            });
        }

        static UIImage RenderFirstPage(NSData pdf, nfloat scale)
        {
            using var provider = new CGDataProvider(pdf);
            using var document = new CGPDFDocument(provider);
            using var page = document.GetPage(1) ?? throw new InvalidOperationException("The page could not be captured.");
            var box = page.GetBoxRect(CGPDFBox.Media);

            var format = UIGraphicsImageRendererFormat.DefaultFormat;
            format.Scale = scale;
            format.Opaque = true;
            using var renderer = new UIGraphicsImageRenderer(box.Size, format);
            return renderer.CreateImage(context =>
            {
                var cg = context.CGContext;
                cg.SetFillColor(UIColor.White.CGColor);
                cg.FillRect(new CGRect(CGPoint.Empty, box.Size));
                // PDF coordinates start at the bottom left.
                cg.TranslateCTM(0, box.Height);
                cg.ScaleCTM(1, -1);
                cg.DrawPDFPage(page);
            });
        }
    }
}
