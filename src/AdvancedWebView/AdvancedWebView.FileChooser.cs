namespace Mafesoft.Maui.Controls
{
    /// <summary>
    /// The page opened a file picker (<c>&lt;input type="file"&gt;</c>). Set <see cref="Handled"/> to show your own picker,
    /// then call <see cref="SetResult"/>.
    /// </summary>
    public sealed class FileChooserRequestedEventArgs : EventArgs
    {
        readonly Action<string[]?> complete;
        int completed;

        internal FileChooserRequestedEventArgs(IReadOnlyList<string> acceptTypes, bool allowsMultiple, bool isCaptureEnabled, Action<string[]?> complete)
        {
            AcceptTypes = acceptTypes;
            AllowsMultiple = allowsMultiple;
            IsCaptureEnabled = isCaptureEnabled;
            this.complete = complete;
        }

        /// <summary>The <c>accept</c> attribute, split: MIME types (<c>image/*</c>) and extensions (<c>.pdf</c>). Empty means any file.</summary>
        public IReadOnlyList<string> AcceptTypes { get; }

        /// <summary>Whether the input has the <c>multiple</c> attribute.</summary>
        public bool AllowsMultiple { get; }

        /// <summary>Whether the input has the <c>capture</c> attribute: the page asks for the camera (or microphone).</summary>
        public bool IsCaptureEnabled { get; }

        /// <summary>
        /// Set to <see langword="true"/>, before any <c>await</c> in the handler, to show your own picker.
        /// You must then call <see cref="SetResult"/>, also when the user cancels: until then the page cannot open another picker.
        /// </summary>
        public bool Handled { get; set; }

        /// <summary>
        /// Hands the chosen files to the page: local file paths (for example <c>FileResult.FullPath</c>) or
        /// <c>content://</c> URIs. <see langword="null"/> or empty means cancelled. Only the first call counts; any thread.
        /// </summary>
        public void SetResult(IEnumerable<string>? files)
        {
            if (Interlocked.Exchange(ref completed, 1) == 1)
                return;

            var list = files?.Where(f => !string.IsNullOrEmpty(f)).ToArray();
            complete(list is { Length: > 0 } ? list : null);
        }
    }

    // <input type="file">.
    public partial class AdvancedWebView
    {
        /// <summary>
        /// Android only. Raised on the main thread when the page opens a file picker, to show your own picker
        /// (set <see cref="FileChooserRequestedEventArgs.Handled"/>). Without a handler AdvancedWebView shows the system picker,
        /// with multiple selection, <c>accept</c> filters and camera capture.
        /// On iOS the system picker of WKWebView is used and this event is not raised.
        /// </summary>
        public event EventHandler<FileChooserRequestedEventArgs>? FileChooserRequested;

        internal void OnFileChooserRequested(FileChooserRequestedEventArgs e) => FileChooserRequested?.Invoke(this, e);

        /// <summary>Splits the <c>accept</c> values (<c>"image/*,.pdf"</c>) into trimmed, lower-case entries.</summary>
        internal static string[] SplitAcceptTypes(IEnumerable<string?>? accept) =>
            accept is null
                ? []
                : accept
                    .SelectMany(a => (a ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    .Select(a => a.ToLowerInvariant())
                    .Distinct()
                    .ToArray();
    }
}
