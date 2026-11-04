using System.Text.Json;

namespace Mafesoft.Maui.Controls
{
    /// <summary>What happens when the page starts a download.</summary>
    public enum DownloadBehavior
    {
        /// <summary>Only <see cref="AdvancedWebView.DownloadRequested"/> is raised: call <c>SaveToAsync</c> to save the file.</summary>
        Event,

        /// <summary>
        /// Android: when the handler does not save it, the system download manager downloads the file to the public
        /// Downloads folder and shows a notification (http and https URLs only). iOS: same as <see cref="Event"/>.
        /// </summary>
        SystemDownloadManager,

        /// <summary>Downloads are ignored and the event is not raised.</summary>
        Ignore,
    }

    /// <summary>The page started a download. Call <see cref="SaveToAsync"/> to save the file.</summary>
    public sealed class DownloadRequestedEventArgs : EventArgs
    {
        readonly DownloadSource source;

        internal DownloadRequestedEventArgs(string url, string suggestedFileName, string? mimeType, long? contentLength, DownloadSource source)
        {
            Url = url;
            SuggestedFileName = suggestedFileName;
            MimeType = mimeType;
            ContentLength = contentLength;
            this.source = source;
        }

        /// <summary>The URL of the file: http(s), <c>blob:</c> or <c>data:</c>.</summary>
        public string Url { get; }

        /// <summary>A safe file name, from <c>Content-Disposition</c>, the URL or the MIME type.</summary>
        public string SuggestedFileName { get; }

        /// <summary>The MIME type reported by the server or the page, when known.</summary>
        public string? MimeType { get; }

        /// <summary>The size in bytes reported by the server, when known.</summary>
        public long? ContentLength { get; }

        /// <summary>
        /// Set to <see langword="true"/> to skip <see cref="AdvancedWebView.DownloadBehavior"/>
        /// (<see cref="DownloadBehavior.SystemDownloadManager"/>) without saving the file.
        /// </summary>
        public bool Cancel { get; set; }

        internal bool Started { get; private set; }

        /// <summary>
        /// Downloads the file to <paramref name="filePath"/>, replacing it, with the web view's cookies (also HttpOnly)
        /// and user agent, so downloads that need the user's session work. <c>blob:</c> and <c>data:</c> URLs are read
        /// from the page. Call it in the event handler, before any other <c>await</c>.
        /// </summary>
        /// <param name="filePath">Where to save the file, for example <c>Path.Combine(FileSystem.CacheDirectory, e.SuggestedFileName)</c>.</param>
        /// <param name="progress">Receives values from 0 to 1 (only 1 at the end when the size is unknown).</param>
        /// <param name="cancellationToken">Cancels the download; the partial file is deleted.</param>
        /// <exception cref="HttpRequestException">The server answered with an error.</exception>
        public Task SaveToAsync(string filePath, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(filePath);
            Started = true;
            return source.SaveAsync(Url, filePath, progress, cancellationToken);
        }
    }

    /// <summary>Platform data a download needs: the page, its cookies and user agent.</summary>
    internal sealed class DownloadSource(AdvancedWebView view, Func<string, Task<string?>> getCookieHeader, Func<Task<string?>> getUserAgent, string? referrer)
    {
        public async Task SaveAsync(string url, string path, IProgress<double>? progress, CancellationToken cancellationToken)
        {
            if (Downloads.ParseDataUrl(url) is { } data)
            {
                await using var stream = new MemoryStream(data.Data, writable: false);
                await Downloads.CopyToFileAsync(stream, path, data.Data.Length, progress, cancellationToken);
                return;
            }

            if (url.StartsWith("blob:", StringComparison.OrdinalIgnoreCase))
            {
                var bytes = await view.ReadBlobAsync(url);
                await using var stream = new MemoryStream(bytes, writable: false);
                await Downloads.CopyToFileAsync(stream, path, bytes.Length, progress, cancellationToken);
                return;
            }

            var cookies = await getCookieHeader(url);
            var userAgent = await getUserAgent();
            await Downloads.DownloadAsync(url, cookies, userAgent, referrer, path, progress, cancellationToken);
        }
    }

    // Downloads started by the page.
    public partial class AdvancedWebView
    {
        /// <summary>Bindable property for <see cref="DownloadBehavior"/>.</summary>
        public static readonly BindableProperty DownloadBehaviorProperty =
            BindableProperty.Create(nameof(DownloadBehavior), typeof(DownloadBehavior), typeof(AdvancedWebView), DownloadBehavior.Event);

        /// <summary>What happens when the page starts a download. Defaults to <see cref="DownloadBehavior.Event"/>.</summary>
        public DownloadBehavior DownloadBehavior
        {
            get => (DownloadBehavior)GetValue(DownloadBehaviorProperty);
            set => SetValue(DownloadBehaviorProperty, value);
        }

        /// <summary>
        /// Raised on the main thread when the page starts a download: a link to a file the web view cannot show,
        /// a <c>Content-Disposition: attachment</c> answer, or a link with the <c>download</c> attribute
        /// (also to <c>blob:</c> and <c>data:</c> URLs). Call <see cref="DownloadRequestedEventArgs.SaveToAsync"/> to save it.
        /// </summary>
        public event EventHandler<DownloadRequestedEventArgs>? DownloadRequested;

        /// <summary>Raises <see cref="DownloadRequested"/>. Returns the event args, or null when downloads are ignored.</summary>
        internal DownloadRequestedEventArgs? OnDownloadRequested(string url, string? suggestedFileName, string? mimeType, long? contentLength, DownloadSource source)
        {
            if (DownloadBehavior == DownloadBehavior.Ignore)
                return null;

            var e = new DownloadRequestedEventArgs(url, Downloads.FileName(suggestedFileName, url, mimeType),
                string.IsNullOrEmpty(mimeType) ? null : mimeType, contentLength is > 0 ? contentLength : null, source);
            DownloadRequested?.Invoke(this, e);
            return e;
        }

        /// <summary>Whether the platform should apply <see cref="DownloadBehavior.SystemDownloadManager"/> after the event.</summary>
        internal bool ShouldUseSystemDownloadManager(DownloadRequestedEventArgs? e) =>
            e is { Cancel: false, Started: false } && DownloadBehavior == DownloadBehavior.SystemDownloadManager;

        /// <summary>Reads a <c>blob:</c> URL of the top-level page through the bridge (page function, base64).</summary>
        internal async Task<byte[]> ReadBlobAsync(string url)
        {
            var json = await InvokeJavaScriptRawAsync("AdvancedWebView.__readBlob", "[" + BridgeProtocol.JsonString(url) + "]");
            using var doc = JsonDocument.Parse(json ?? "null");
            var base64 = doc.RootElement.ValueKind == JsonValueKind.String ? doc.RootElement.GetString() : null;
            return base64 is null ? throw new JavaScriptException($"Could not read {url}.") : Convert.FromBase64String(base64);
        }
    }
}
