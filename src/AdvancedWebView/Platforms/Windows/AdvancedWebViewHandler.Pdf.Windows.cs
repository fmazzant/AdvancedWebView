using Microsoft.Web.WebView2.Core;

namespace Mafesoft.Maui.Controls.Handlers
{
    // Windows: PDF export with PrintToPdfStreamAsync, printing with the system print dialog.
    public partial class AdvancedWebViewHandler
    {
        internal partial async Task<byte[]> ExportPdfAsync(WebPdfOptions options)
        {
            if (core is not { } target)
                throw new InvalidOperationException("The web view is not ready: export the page after it is loaded.");

            var (width, height) = PdfLayout.PageSizeInches(options.PaperSize, options.Orientation);
            var margin = PdfLayout.MarginInches(options);

            var settings = target.Environment.CreatePrintSettings();
            // The page size already has the orientation applied.
            settings.Orientation = CoreWebView2PrintOrientation.Portrait;
            settings.PageWidth = width;
            settings.PageHeight = height;
            settings.MarginTop = settings.MarginBottom = settings.MarginLeft = settings.MarginRight = margin;
            settings.ShouldPrintHeaderAndFooter = false;

            using var stream = await target.PrintToPdfStreamAsync(settings);
            using var input = stream.AsStreamForRead();
            using var output = new MemoryStream();
            await input.CopyToAsync(output);
            return output.ToArray();
        }

        internal partial Task PrintAsync(string jobName)
        {
            if (core is not { } target)
                throw new InvalidOperationException("The web view is not ready: print the page after it is loaded.");

            // WebView2 names the job after the page title.
            target.ShowPrintUI(CoreWebView2PrintDialogKind.System);
            return Task.CompletedTask;
        }
    }
}
