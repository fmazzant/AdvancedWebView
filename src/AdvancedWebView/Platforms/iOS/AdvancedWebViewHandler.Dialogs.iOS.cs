using WebKit;

namespace Mafesoft.Maui.Controls.Handlers
{
    // iOS and Mac Catalyst: custom JavaScript dialogs (#56) through the WKUIDelegate panels. WebKit requires the completion
    // handler to be called exactly once: the answer goes through a guard that drops any second call. WebKit has no
    // public beforeunload panel, so BeforeUnload is not raised here.
    public partial class AdvancedWebViewHandler
    {
        /// <summary>Answers the panel through JavaScriptDialogRequested; false when no handler is attached (MAUI's alert).</summary>
        bool ShowJavaScriptDialog(JavaScriptDialogKind kind, string message, string? defaultText, WKFrameInfo frame, Action<JavaScriptDialogAnswer> complete)
        {
            if (Advanced is not { HasJavaScriptDialogHandler: true } view)
                return false;

            var answered = 0;
            void CompleteOnce(JavaScriptDialogAnswer answer)
            {
                if (Interlocked.Exchange(ref answered, 1) == 0)
                    complete(answer);
            }

            _ = AnswerJavaScriptDialogAsync(view, kind, message, defaultText, frame, CompleteOnce);
            return true;
        }

        async Task AnswerJavaScriptDialogAsync(AdvancedWebView view, JavaScriptDialogKind kind, string message, string? defaultText, WKFrameInfo frame, Action<JavaScriptDialogAnswer> complete)
        {
            try
            {
                var url = frame.Request?.Url?.AbsoluteString ?? frame.WebView?.Url?.AbsoluteString;
                complete(await view.RequestJavaScriptDialogAsync(kind, message, defaultText, url));
            }
            catch (Exception ex)
            {
                Log($"JavaScript dialog failed: {ex.Message}");
                complete(JavaScriptDialogAnswer.Cancel(kind));
            }
        }
    }
}
