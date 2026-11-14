using Mafesoft.Maui.Controls.Handlers;

namespace Mafesoft.Maui.Controls
{
    /// <summary>
    /// The page opens <c>alert()</c>, <c>confirm()</c>, <c>prompt()</c> or a "leave this page?" dialog. Show your own UI
    /// and set <see cref="Result"/> (and <see cref="Text"/> for a prompt), or leave them as they are to dismiss the dialog.
    /// For an asynchronous answer (for example <c>DisplayAlertAsync</c>), call <see cref="GetDeferral"/> before the first
    /// <c>await</c> and dispose it after: the page waits until then.
    /// </summary>
    public sealed class JavaScriptDialogRequestedEventArgs : EventArgs
    {
        readonly EventDeferral deferral = new();

        internal JavaScriptDialogRequestedEventArgs(JavaScriptDialogKind kind, string message, string? defaultText, string? url)
        {
            Kind = kind;
            Message = message;
            DefaultText = defaultText;
            Text = defaultText;
            Url = url;
            Origin = JavaScriptDialogs.GetOrigin(url);
        }

        /// <summary>Alert, confirm, prompt or "leave this page?".</summary>
        public JavaScriptDialogKind Kind { get; }

        /// <summary>The message the page shows.</summary>
        public string Message { get; }

        /// <summary>The default text of a prompt (<c>prompt(message, defaultText)</c>); null for the other dialogs.</summary>
        public string? DefaultText { get; }

        /// <summary>The URL of the page (or iframe) that opened the dialog, where the platform reports it.</summary>
        public string? Url { get; }

        /// <summary>The origin of <see cref="Url"/>, for example <c>https://example.com</c>; null for local content.</summary>
        public Uri? Origin { get; }

        /// <summary>
        /// <see langword="true"/> for OK (confirm: <c>true</c>; prompt: <see cref="Text"/>; "leave this page?": leave),
        /// <see langword="false"/> (the default) for Cancel. Ignored for an alert.
        /// </summary>
        public bool Result { get; set; }

        /// <summary>The text a prompt returns when <see cref="Result"/> is <see langword="true"/>. Starts as <see cref="DefaultText"/>.</summary>
        public string? Text { get; set; }

        /// <summary>Delays the answer until the returned object is disposed, so <see cref="Result"/> can be set after an <c>await</c>.</summary>
        /// <example>
        /// <code>
        /// using var deferral = e.GetDeferral();
        /// e.Result = await DisplayAlertAsync("Page", e.Message, "OK", "Cancel");
        /// </code>
        /// </example>
        public IDisposable GetDeferral() => deferral.GetDeferral();

        /// <summary>Completes once the event handlers have returned and every deferral is disposed.</summary>
        internal Task WhenAnsweredAsync() => deferral.WhenDoneAsync();
    }

    // Custom JavaScript dialogs: alert, confirm, prompt, beforeunload (#56).
    public partial class AdvancedWebView
    {
        EventHandler<JavaScriptDialogRequestedEventArgs>? javaScriptDialogRequested;
        readonly object javaScriptDialogGate = new();

        /// <summary>
        /// Raised on the main thread when the page opens <c>alert()</c>, <c>confirm()</c>, <c>prompt()</c> or, on Android
        /// and Windows, the "leave this page?" dialog of <c>beforeunload</c>. With a handler attached, the platform dialog
        /// is not shown: the handler answers (or dismisses it by doing nothing). Without a handler, the platform dialogs stay.
        /// </summary>
        public event EventHandler<JavaScriptDialogRequestedEventArgs>? JavaScriptDialogRequested
        {
            add
            {
                lock (javaScriptDialogGate)
                    javaScriptDialogRequested += value;
                OnJavaScriptDialogRequestedChanged();
            }
            remove
            {
                lock (javaScriptDialogGate)
                    javaScriptDialogRequested -= value;
                OnJavaScriptDialogRequestedChanged();
            }
        }

        /// <summary>Whether a handler is attached: without one, the platform dialogs are shown.</summary>
        internal bool HasJavaScriptDialogHandler => javaScriptDialogRequested is not null;

        void OnJavaScriptDialogRequestedChanged()
        {
            if (Dispatcher is { IsDispatchRequired: true } dispatcher)
                dispatcher.Dispatch(() => (Handler as AdvancedWebViewHandler)?.UpdateJavaScriptDialogs());
            else
                (Handler as AdvancedWebViewHandler)?.UpdateJavaScriptDialogs();
        }

        /// <summary>
        /// Raises <see cref="JavaScriptDialogRequested"/> and waits for its deferrals. A handler that throws cancels the
        /// dialog. Continues on the calling (main) thread: the platforms answer the page from there.
        /// </summary>
        internal async Task<JavaScriptDialogAnswer> RequestJavaScriptDialogAsync(JavaScriptDialogKind kind, string? message, string? defaultText, string? url)
        {
            var e = new JavaScriptDialogRequestedEventArgs(kind, message ?? string.Empty, kind == JavaScriptDialogKind.Prompt ? defaultText ?? string.Empty : null, url);
            try
            {
                javaScriptDialogRequested?.Invoke(this, e);
            }
            catch (Exception ex)
            {
                LogWarning($"JavaScriptDialogRequested handler failed, the dialog is cancelled: {ex.Message}");
                return JavaScriptDialogAnswer.Cancel(kind);
            }

            await e.WhenAnsweredAsync();
            return JavaScriptDialogs.Resolve(kind, e.Result, e.Text);
        }
    }
}
