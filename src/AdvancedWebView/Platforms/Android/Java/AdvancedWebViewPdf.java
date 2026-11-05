package android.print;

import android.os.CancellationSignal;
import android.os.ParcelFileDescriptor;
import android.webkit.ValueCallback;

/**
 * Writes a WebView's print document to a file, without the print UI (AdvancedWebView.ExportPdfAsync).
 * It lives in android.print because the constructors of LayoutResultCallback and WriteResultCallback are
 * package-private: code outside this package cannot receive the results of PrintDocumentAdapter.
 */
public final class AdvancedWebViewPdf {
    private AdvancedWebViewPdf() {
    }

    /** Lays out and writes every page. {@code done} receives null on success, or the error message. */
    public static void write(final PrintDocumentAdapter adapter, final PrintAttributes attributes,
            final ParcelFileDescriptor output, final ValueCallback<String> done) {
        final CancellationSignal cancellation = new CancellationSignal();
        adapter.onStart();
        adapter.onLayout(null, attributes, cancellation, new PrintDocumentAdapter.LayoutResultCallback() {
            @Override
            public void onLayoutFinished(PrintDocumentInfo info, boolean changed) {
                adapter.onWrite(new PageRange[] { PageRange.ALL_PAGES }, output, cancellation,
                        new PrintDocumentAdapter.WriteResultCallback() {
                            @Override
                            public void onWriteFinished(PageRange[] pages) {
                                finish(adapter, done, null);
                            }

                            @Override
                            public void onWriteFailed(CharSequence error) {
                                finish(adapter, done, error == null ? "The PDF could not be written." : error.toString());
                            }

                            @Override
                            public void onWriteCancelled() {
                                finish(adapter, done, "The PDF export was cancelled.");
                            }
                        });
            }

            @Override
            public void onLayoutFailed(CharSequence error) {
                finish(adapter, done, error == null ? "The page could not be laid out." : error.toString());
            }

            @Override
            public void onLayoutCancelled() {
                finish(adapter, done, "The PDF export was cancelled.");
            }
        }, null);
    }

    private static void finish(PrintDocumentAdapter adapter, ValueCallback<String> done, String error) {
        adapter.onFinish();
        done.onReceiveValue(error);
    }
}
