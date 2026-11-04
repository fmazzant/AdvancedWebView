using Android.App;
using Android.Content;
using Android.Webkit;
using AEnvironment = Android.OS.Environment;
using AWebView = Android.Webkit.WebView;

namespace Mafesoft.Maui.Controls.Handlers
{
    // Android: downloads. MAUI sets no DownloadListener, so without one the WebView ignores downloads.
    public partial class AdvancedWebViewHandler
    {
        void SetUpDownloads(AWebView webView) => webView.SetDownloadListener(new AdvancedDownloadListener(this));

        void OnDownloadStart(string url, string? userAgent, string? contentDisposition, string? mimeType, long contentLength)
        {
            if (Advanced is not { } view)
                return;

            // GuessFileName reads Content-Disposition (also the download attribute of blob: links); for blob:/data:
            // without it, it would invent "downloadfile.bin": Downloads.FileName builds a better default.
            var suggested = !string.IsNullOrEmpty(contentDisposition) || ExternalLinkRules.GetScheme(url) is "http" or "https"
                ? URLUtil.GuessFileName(url, contentDisposition, mimeType)
                : null;

            var source = new DownloadSource(view,
                getCookieHeader: u => Task.FromResult(CookieManager.Instance?.GetCookie(u)),
                getUserAgent: () => Task.FromResult(userAgent ?? PlatformView?.Settings.UserAgentString),
                referrer: PlatformView?.Url);

            var e = view.OnDownloadRequested(url, suggested, mimeType, contentLength, source);
            if (view.ShouldUseSystemDownloadManager(e))
                EnqueueSystemDownload(e!, userAgent);
        }

        // DownloadManager saves to the public Downloads folder and shows a notification. It only handles http(s).
        void EnqueueSystemDownload(DownloadRequestedEventArgs e, string? userAgent)
        {
            if (ExternalLinkRules.GetScheme(e.Url) is not ("http" or "https"))
            {
                Log($"The system download manager cannot download {ExternalLinkRules.GetScheme(e.Url)}: URLs. Save it in DownloadRequested with SaveToAsync.");
                return;
            }

            try
            {
                var request = new DownloadManager.Request(Android.Net.Uri.Parse(e.Url));
                if (CookieManager.Instance?.GetCookie(e.Url) is { Length: > 0 } cookies)
                    request.AddRequestHeader("Cookie", cookies);
                if (!string.IsNullOrEmpty(userAgent))
                    request.AddRequestHeader("User-Agent", userAgent);
                if (e.MimeType is { } mimeType)
                    request.SetMimeType(mimeType);
                request.SetTitle(e.SuggestedFileName);
                request.SetNotificationVisibility(DownloadVisibility.VisibleNotifyCompleted);
                request.SetDestinationInExternalPublicDir(AEnvironment.DirectoryDownloads, e.SuggestedFileName);

                (PlatformView?.Context?.GetSystemService(Context.DownloadService) as DownloadManager)?.Enqueue(request);
            }
            catch (Exception ex)
            {
                // Android 9 and earlier need WRITE_EXTERNAL_STORAGE to write to the public Downloads folder.
                Log($"The system download manager could not download {e.Url}: {ex.Message}");
            }
        }

        sealed class AdvancedDownloadListener : Java.Lang.Object, IDownloadListener
        {
            readonly WeakReference<AdvancedWebViewHandler> handler;

            public AdvancedDownloadListener(AdvancedWebViewHandler handler) =>
                this.handler = new WeakReference<AdvancedWebViewHandler>(handler);

            public void OnDownloadStart(string? url, string? userAgent, string? contentDisposition, string? mimetype, long contentLength)
            {
                if (url is not null && handler.TryGetTarget(out var owner))
                    owner.OnDownloadStart(url, userAgent, contentDisposition, mimetype, contentLength);
            }
        }
    }
}
