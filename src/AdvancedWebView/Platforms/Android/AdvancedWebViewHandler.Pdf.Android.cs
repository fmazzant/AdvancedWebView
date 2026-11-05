using Android.OS;
using Android.Print;
using Android.Runtime;
using Android.Webkit;

namespace Mafesoft.Maui.Controls.Handlers
{
    // Android: the WebView's print document adapter. Printing hands it to PrintManager; the PDF export writes it
    // to a file through android.print.AdvancedWebViewPdf (Platforms/Android/Java), without the print UI.
    public partial class AdvancedWebViewHandler
    {
        const double MilsPerInch = 1000;

        static IntPtr pdfWriterClass;
        static IntPtr pdfWriteMethod;

        internal partial async Task<byte[]> ExportPdfAsync(WebPdfOptions options)
        {
            var webView = PlatformView ?? throw new InvalidOperationException("The web view is not ready.");

            var (width, height) = PdfLayout.PageSizeInches(options.PaperSize, options.Orientation);
            var margin = (int)Math.Round(PdfLayout.MarginInches(options) * MilsPerInch);
            var mediaSize = new PrintAttributes.MediaSize("advancedwebview", "AdvancedWebView",
                (int)Math.Round(width * MilsPerInch), (int)Math.Round(height * MilsPerInch));
            var attributes = new PrintAttributes.Builder()
                .SetMediaSize(mediaSize)!
                .SetResolution(new PrintAttributes.Resolution("pdf", "pdf", 600, 600))!
                .SetMinMargins(new PrintAttributes.Margins(margin, margin, margin, margin))!
                .Build()!;

            var file = new Java.IO.File(webView.Context!.CacheDir, $"advancedwebview-{Guid.NewGuid():N}.pdf");
            try
            {
                using (var output = ParcelFileDescriptor.Open(file, ParcelFileMode.ReadWrite | ParcelFileMode.Create | ParcelFileMode.Truncate)!)
                {
                    var done = new PdfWriteCallback();
                    var adapter = webView.CreatePrintDocumentAdapter("AdvancedWebView");
                    WritePdf(adapter, attributes, output, done);
                    await done.Task;
                }

                return await File.ReadAllBytesAsync(file.AbsolutePath);
            }
            finally
            {
                file.Delete();
            }
        }

        static void WritePdf(PrintDocumentAdapter adapter, PrintAttributes attributes, ParcelFileDescriptor output, PdfWriteCallback done)
        {
            if (pdfWriterClass == IntPtr.Zero)
            {
                var local = JNIEnv.FindClass("android/print/AdvancedWebViewPdf");
                pdfWriterClass = JNIEnv.NewGlobalRef(local);
                JNIEnv.DeleteLocalRef(local);
                pdfWriteMethod = JNIEnv.GetStaticMethodID(pdfWriterClass, "write",
                    "(Landroid/print/PrintDocumentAdapter;Landroid/print/PrintAttributes;Landroid/os/ParcelFileDescriptor;Landroid/webkit/ValueCallback;)V");
            }

            JNIEnv.CallStaticVoidMethod(pdfWriterClass, pdfWriteMethod,
                new JValue(adapter), new JValue(attributes), new JValue(output), new JValue(done));
        }

        internal partial Task PrintAsync(string jobName)
        {
            var webView = PlatformView ?? throw new InvalidOperationException("The web view is not ready.");
            var context = Platform.CurrentActivity ?? webView.Context;
            if (context?.GetSystemService(Android.Content.Context.PrintService) is not PrintManager printManager)
                throw new InvalidOperationException("Printing is not available on this device.");

            printManager.Print(jobName, webView.CreatePrintDocumentAdapter(jobName), new PrintAttributes.Builder().Build());
            return Task.CompletedTask;
        }

        /// <summary>Receives null on success, or the error message (android.print.AdvancedWebViewPdf).</summary>
        sealed class PdfWriteCallback : Java.Lang.Object, IValueCallback
        {
            readonly TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public Task Task => completion.Task;

            public void OnReceiveValue(Java.Lang.Object? value)
            {
                if (value is null)
                    completion.TrySetResult();
                else
                    completion.TrySetException(new InvalidOperationException(value.ToString()));
            }
        }
    }
}
