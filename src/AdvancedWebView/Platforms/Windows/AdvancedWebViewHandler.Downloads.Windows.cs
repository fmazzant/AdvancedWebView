using Microsoft.Web.WebView2.Core;

namespace Mafesoft.Maui.Controls.Handlers
{
    // Windows: downloads. WebView2's own download is cancelled and the file is downloaded with HttpClient
    // (DownloadRequestedEventArgs.SaveToAsync) with the web view's cookies, as on the other platforms.
    // DownloadBehavior.SystemDownloadManager lets WebView2 download it instead (Downloads folder, with its download UI).
    public partial class AdvancedWebViewHandler
    {
        void SetUpDownloads(CoreWebView2 target) => target.DownloadStarting += OnDownloadStarting;

        void TearDownDownloads(CoreWebView2 target) => target.DownloadStarting -= OnDownloadStarting;

        void OnDownloadStarting(CoreWebView2 sender, CoreWebView2DownloadStartingEventArgs args)
        {
            if (Advanced is not { } view)
                return;

            var download = args.DownloadOperation;
            var e = view.OnDownloadRequested(download.Uri, Path.GetFileName(args.ResultFilePath), download.MimeType,
                download.TotalBytesToReceive is { } total ? (long)total : null,
                new DownloadSource(view, GetCookieHeaderAsync, GetUserAgentAsync, sender.Source));

            if (view.ShouldUseSystemDownloadManager(e))
                return; // WebView2 downloads the file itself

            args.Cancel = true;
        }

        Task<string?> GetCookieHeaderAsync(string url) => MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (core is not { } target)
                return null;

            // The cookies WebView2 would send to this URL, HttpOnly ones included.
            var cookies = await target.CookieManager.GetCookiesAsync(url);
            var header = string.Join("; ", cookies.Select(c => c.Name + "=" + c.Value));
            return header.Length == 0 ? null : header;
        });

        Task<string?> GetUserAgentAsync() =>
            MainThread.InvokeOnMainThreadAsync(() => core?.Settings.UserAgent);
    }
}
