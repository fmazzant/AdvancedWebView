using Android.Webkit;

namespace Mafesoft.Maui.Controls.Handlers
{
    // Android: custom JavaScript dialogs (#56) through WebChromeClient.OnJsAlert / OnJsConfirm / OnJsPrompt /
    // OnJsBeforeUnload. Returning true tells the web view that the app answers the JsResult, exactly once.
    public partial class AdvancedWebViewHandler
    {
        /// <summary>Answers the dialog through JavaScriptDialogRequested; false when no handler is attached (platform dialog).</summary>
        bool ShowJavaScriptDialog(JavaScriptDialogKind kind, string? url, string? message, string? defaultValue, JsResult result)
        {
            if (Advanced is not { HasJavaScriptDialogHandler: true } view)
                return false;

            _ = AnswerJavaScriptDialogAsync(view, kind, url, message, defaultValue, result);
            return true;
        }

        async Task AnswerJavaScriptDialogAsync(AdvancedWebView view, JavaScriptDialogKind kind, string? url, string? message, string? defaultValue, JsResult result)
        {
            JavaScriptDialogAnswer answer;
            try
            {
                answer = await view.RequestJavaScriptDialogAsync(kind, message, defaultValue, url);
            }
            catch (Exception ex)
            {
                Log($"JavaScript dialog failed: {ex.Message}");
                answer = JavaScriptDialogAnswer.Cancel(kind);
            }

            if (!answer.Accept)
                result.Cancel();
            else if (result is JsPromptResult prompt)
                prompt.Confirm(answer.Text ?? string.Empty);
            else
                result.Confirm();
        }
    }
}
