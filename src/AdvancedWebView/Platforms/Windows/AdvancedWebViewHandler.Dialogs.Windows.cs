using Microsoft.Web.WebView2.Core;

namespace Mafesoft.Maui.Controls.Handlers
{
    // Windows: custom JavaScript dialogs (#56). With a JavaScriptDialogRequested handler, WebView2's own dialogs are turned
    // off (AreDefaultScriptDialogsEnabled) and ScriptDialogOpening is answered with a deferral; without one, they stay.
    public partial class AdvancedWebViewHandler
    {
        void SetUpDialogs(CoreWebView2 target)
        {
            target.ScriptDialogOpening += OnScriptDialogOpening;
            UpdateJavaScriptDialogsCore();
        }

        void TearDownDialogs(CoreWebView2 target) => target.ScriptDialogOpening -= OnScriptDialogOpening;

        partial void UpdateJavaScriptDialogsCore()
        {
            if (core is { } target)
                target.Settings.AreDefaultScriptDialogsEnabled = Advanced?.HasJavaScriptDialogHandler != true;
        }

        async void OnScriptDialogOpening(CoreWebView2 sender, CoreWebView2ScriptDialogOpeningEventArgs args)
        {
            // Raised only with the default dialogs off. A handler removed meanwhile: dismiss, as the page cannot get WebView2's dialog now.
            var kind = ToDialogKind(args.Kind);
            var deferral = args.GetDeferral();
            try
            {
                var answer = Advanced is { } view
                    ? await view.RequestJavaScriptDialogAsync(kind, args.Message, args.DefaultText, args.Uri)
                    : JavaScriptDialogAnswer.Cancel(kind);

                if (answer.Accept)
                {
                    if (kind == JavaScriptDialogKind.Prompt)
                        args.ResultText = answer.Text ?? string.Empty;
                    args.Accept();
                }
            }
            catch (Exception ex)
            {
                Log($"JavaScript dialog failed: {ex.Message}");
            }
            finally
            {
                deferral.Complete();
            }
        }

        static JavaScriptDialogKind ToDialogKind(CoreWebView2ScriptDialogKind kind) => kind switch
        {
            CoreWebView2ScriptDialogKind.Confirm => JavaScriptDialogKind.Confirm,
            CoreWebView2ScriptDialogKind.Prompt => JavaScriptDialogKind.Prompt,
            CoreWebView2ScriptDialogKind.Beforeunload => JavaScriptDialogKind.BeforeUnload,
            _ => JavaScriptDialogKind.Alert,
        };
    }
}
