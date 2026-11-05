using Mafesoft.Maui.Controls.Handlers;

namespace Mafesoft.Maui.Controls
{
    // PDF export and printing.
    public partial class AdvancedWebView
    {
        /// <summary>
        /// Exports the page as a PDF, split into pages as when printing it: the page's print CSS (<c>@media print</c>,
        /// <c>@page</c>) applies, and the text stays selectable. Returns the PDF as a stream positioned at the start.
        /// </summary>
        /// <param name="options">Paper size, orientation and margin; null for A4 portrait with 10 mm margins.</param>
        /// <exception cref="InvalidOperationException">The control is not attached to its handler or the page is not ready.</exception>
        public async Task<Stream> ExportPdfAsync(WebPdfOptions? options = null)
        {
            var handler = RequireHandler();
            var pdf = await MainThread.InvokeOnMainThreadAsync(() => handler.ExportPdfAsync(options ?? new WebPdfOptions()));
            return new MemoryStream(pdf, writable: false);
        }

        /// <summary>
        /// Opens the system print dialog for the page. Android: the print service UI. iOS and Mac Catalyst: the
        /// print sheet; the task completes when it closes. Windows: the system print dialog.
        /// </summary>
        /// <param name="jobName">Name of the print job; null for the page title.</param>
        /// <exception cref="InvalidOperationException">The control is not attached to its handler or the page is not ready.</exception>
        public Task PrintAsync(string? jobName = null)
        {
            var handler = RequireHandler();
            var name = string.IsNullOrWhiteSpace(jobName) ? (string.IsNullOrWhiteSpace(Title) ? "Document" : Title!) : jobName;
            return MainThread.InvokeOnMainThreadAsync(() => handler.PrintAsync(name));
        }

        AdvancedWebViewHandler RequireHandler() =>
            Handler as AdvancedWebViewHandler ?? throw new InvalidOperationException(
                "AdvancedWebView is not attached to its handler. Call builder.UseAdvancedWebView() in MauiProgram and wait until the control is loaded.");
    }
}
