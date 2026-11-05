using CoreGraphics;
using Foundation;
using UIKit;

namespace Mafesoft.Maui.Controls.Handlers
{
    // iOS and Mac Catalyst: the web view's print formatter, drawn page by page into a PDF context for the export,
    // or handed to UIPrintInteractionController for printing. WKWebView.createPDF would make one long page instead.
    public partial class AdvancedWebViewHandler
    {
        const double PointsPerInch = 72;

        internal partial Task<byte[]> ExportPdfAsync(WebPdfOptions options)
        {
            var webView = PlatformView ?? throw new InvalidOperationException("The web view is not ready.");

            var (width, height) = PdfLayout.PageSizeInches(options.PaperSize, options.Orientation);
            var margin = PdfLayout.MarginInches(options) * PointsPerInch;
            var paper = new CGRect(0, 0, width * PointsPerInch, height * PointsPerInch);

            using var renderer = new PdfPageRenderer(paper, paper.Inset((nfloat)margin, (nfloat)margin));
            renderer.AddPrintFormatter(webView.ViewPrintFormatter, 0);

            var data = new NSMutableData();
            UIGraphics.BeginPDFContext(data, paper, null);
            try
            {
                var pages = renderer.NumberOfPages;
                renderer.PrepareForDrawingPages(new NSRange(0, pages));
                for (nint page = 0; page < pages; page++)
                {
                    UIGraphics.BeginPDFPage();
                    renderer.DrawPage(page, UIGraphics.PDFContextBounds);
                }
            }
            finally
            {
                UIGraphics.EndPDFContext();
            }

            return Task.FromResult(data.ToArray());
        }

        internal partial Task PrintAsync(string jobName)
        {
            var webView = PlatformView ?? throw new InvalidOperationException("The web view is not ready.");

            var info = UIPrintInfo.PrintInfo;
            info.JobName = jobName;
            info.OutputType = UIPrintInfoOutputType.General;

            var controller = UIPrintInteractionController.SharedPrintController;
            controller.PrintInfo = info;
            controller.PrintFormatter = webView.ViewPrintFormatter;

            var closed = new TaskCompletionSource();
            controller.Present(true, (_, _, _) => closed.TrySetResult());
            return closed.Task;
        }

        // UIPrintPageRenderer reads the paper and printable area from these properties, which are read-only otherwise.
        sealed class PdfPageRenderer(CGRect paper, CGRect printable) : UIPrintPageRenderer
        {
            public override CGRect PaperRect => paper;

            public override CGRect PrintableRect => printable;
        }
    }
}
