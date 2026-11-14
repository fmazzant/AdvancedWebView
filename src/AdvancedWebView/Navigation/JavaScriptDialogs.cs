namespace Mafesoft.Maui.Controls
{
    /// <summary>The JavaScript dialog a page opens.</summary>
    public enum JavaScriptDialogKind
    {
        /// <summary><c>alert(message)</c>: the page only waits until it is closed.</summary>
        Alert,

        /// <summary><c>confirm(message)</c>: the page gets <see langword="true"/> (OK) or <see langword="false"/> (Cancel).</summary>
        Confirm,

        /// <summary><c>prompt(message, defaultText)</c>: the page gets the text (OK) or <c>null</c> (Cancel).</summary>
        Prompt,

        /// <summary>
        /// The "leave this page?" dialog of a <c>beforeunload</c> handler: <see langword="true"/> leaves the page.
        /// Android and Windows only: WebKit does not let apps show it.
        /// </summary>
        BeforeUnload,
    }

    /// <summary>What the page gets back from a dialog: whether it was accepted (OK) and, for a prompt, the text.</summary>
    internal readonly record struct JavaScriptDialogAnswer(bool Accept, string? Text)
    {
        /// <summary>The answer of a dialog closed with Cancel (an alert is simply closed).</summary>
        public static JavaScriptDialogAnswer Cancel(JavaScriptDialogKind kind) => JavaScriptDialogs.Resolve(kind, false, null);
    }

    /// <summary>Custom JavaScript dialogs (#56). Platform-independent.</summary>
    internal static class JavaScriptDialogs
    {
        /// <summary>
        /// The answer for the page from the handler's <c>Result</c> and <c>Text</c>: an alert is always closed normally;
        /// a confirm or a "leave page" dialog follows <paramref name="result"/>; an accepted prompt returns
        /// <paramref name="text"/> (an empty string when null), a cancelled one returns null.
        /// </summary>
        public static JavaScriptDialogAnswer Resolve(JavaScriptDialogKind kind, bool result, string? text) => kind switch
        {
            JavaScriptDialogKind.Alert => new(true, null),
            JavaScriptDialogKind.Prompt => result ? new(true, text ?? string.Empty) : new(false, null),
            _ => new(result, null),
        };

        /// <summary>The origin (scheme, host and port) of the page URL, or null for local content and invalid URLs.</summary>
        public static Uri? GetOrigin(string? url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Host.Length == 0 || uri.IsFile)
                return null;

            return new Uri(uri.GetLeftPart(UriPartial.Authority));
        }
    }
}
