using System.Net;
using Microsoft.Web.WebView2.Core;

namespace Mafesoft.Maui.Controls.Handlers
{
    // Windows: page state, new windows, external links, load failures, certificate errors, LoadUrl with headers and POST.
    // MAUI handles NavigationStarting first (it raises Navigating and may cancel); these handlers run after it.
    public partial class AdvancedWebViewHandler
    {
        // Navigations cancelled in NavigationStarting: their "operation canceled" completion is not a page failure.
        readonly HashSet<ulong> cancelledNavigations = [];
        readonly Dictionary<ulong, string> navigationUrls = [];

        void SetUpNavigation(CoreWebView2 target)
        {
            target.NavigationStarting += OnNavigationStarting;
            target.FrameNavigationStarting += OnFrameNavigationStarting;
            target.ContentLoading += OnContentLoading;
            target.DOMContentLoaded += OnDOMContentLoaded;
            target.NavigationCompleted += OnNavigationCompleted;
            target.SourceChanged += OnSourceChanged;
            target.DocumentTitleChanged += OnDocumentTitleChanged;
            target.NewWindowRequested += OnNewWindowRequested;
            target.LaunchingExternalUriScheme += OnLaunchingExternalUriScheme;
            target.ServerCertificateErrorDetected += OnServerCertificateErrorDetected;
        }

        void TearDownNavigation(CoreWebView2 target)
        {
            target.NavigationStarting -= OnNavigationStarting;
            target.FrameNavigationStarting -= OnFrameNavigationStarting;
            target.ContentLoading -= OnContentLoading;
            target.DOMContentLoaded -= OnDOMContentLoaded;
            target.NavigationCompleted -= OnNavigationCompleted;
            target.SourceChanged -= OnSourceChanged;
            target.DocumentTitleChanged -= OnDocumentTitleChanged;
            target.NewWindowRequested -= OnNewWindowRequested;
            target.LaunchingExternalUriScheme -= OnLaunchingExternalUriScheme;
            target.ServerCertificateErrorDetected -= OnServerCertificateErrorDetected;
            cancelledNavigations.Clear();
            navigationUrls.Clear();
        }

        void OnNavigationStarting(CoreWebView2 sender, CoreWebView2NavigationStartingEventArgs args)
        {
            if (Advanced is not { } view)
                return;

            // A navigation MAUI allowed may still leave the app (tel:, mailto:, hosts outside InternalHosts).
            if (!args.Cancel && !IsMauiLocalUrl(args.Uri) && view.TryOpenExternalLink(args.Uri, isMainFrame: true))
                args.Cancel = true;

            if (args.Cancel)
            {
                cancelledNavigations.Add(args.NavigationId);
                return;
            }

            navigationUrls[args.NavigationId] = args.Uri;
            RemoveLeakedHeaders(args);
            view.OnLoadStarted(args.Uri);
            // WebView2 reports no load progress: Progress moves in steps (start, content, DOM ready, done).
            view.SetProgress(0.1);
        }

        void OnFrameNavigationStarting(CoreWebView2 sender, CoreWebView2NavigationStartingEventArgs args)
        {
            if (!args.Cancel && Advanced is { } view && view.TryOpenExternalLink(args.Uri, isMainFrame: false))
                args.Cancel = true;
        }

        void OnContentLoading(CoreWebView2 sender, CoreWebView2ContentLoadingEventArgs args)
        {
            // The new document is committed, after any redirect.
            RequestHeaders.OnCommitted(sender.Source);
            Advanced?.SetProgress(Math.Max(Advanced.Progress, 0.5));
        }

        /// <summary>
        /// #60: WebView2 sends the LoadUrl headers again on redirects (to other origins too), reloads and back navigations.
        /// NavigationStarting is raised for each of them, and its headers can be changed: the ones that must not go to
        /// this origin are removed.
        /// </summary>
        void RemoveLeakedHeaders(CoreWebView2NavigationStartingEventArgs args)
        {
            var headers = args.RequestHeaders;
            foreach (var name in RequestHeaders.FindLeakedHeaders(args.Uri, headers))
                headers.RemoveHeader(name);
        }

        void OnDOMContentLoaded(CoreWebView2 sender, CoreWebView2DOMContentLoadedEventArgs args)
        {
            ApplyTextZoom();
            Advanced?.SetProgress(Math.Max(Advanced.Progress, 0.8));
        }

        void OnSourceChanged(CoreWebView2 sender, CoreWebView2SourceChangedEventArgs args) =>
            Advanced?.SetCurrentUrl(sender.Source);

        void OnDocumentTitleChanged(CoreWebView2 sender, object args) =>
            Advanced?.SetTitle(sender.DocumentTitle);

        void OnNavigationCompleted(CoreWebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            var id = args.NavigationId;
            navigationUrls.Remove(id, out var url);
            if (cancelledNavigations.Remove(id) || Advanced is not { } view)
                return;

            view.OnLoadFinished(sender.Source);
            url ??= sender.Source;

            // An HTTP error page from the server can still be "successful": the status code tells.
            string? html = null;
            if (args.HttpStatusCode >= 400)
                html = ErrorPageFor(url, WebNavigationError.HttpError, args.HttpStatusCode, ReasonPhrase(args.HttpStatusCode));
            else if (!args.IsSuccess)
                html = ErrorPageFor(url, NavigationErrors.FromWebView2((int)args.WebErrorStatus), null, Describe(args.WebErrorStatus));

            if (html is not null)
                sender.NavigateToString(html);
        }

        /// <summary>Raises NavigationFailed for the top-level page. Returns the error page to show, or null.</summary>
        string? ErrorPageFor(string? url, WebNavigationError error, int? httpStatusCode, string description)
        {
            if (Advanced is not { } view || !AdvancedWebView.IsReportableUrl(url) || IsMauiLocalUrl(url))
                return null;
            return view.OnNavigationFailed(url!, error, httpStatusCode, description);
        }

        static string ReasonPhrase(int statusCode)
        {
            using var response = new System.Net.Http.HttpResponseMessage((HttpStatusCode)statusCode);
            return response.ReasonPhrase ?? $"HTTP {statusCode}";
        }

        // "HostNameNotResolved" -> "Host name not resolved".
        static string Describe(CoreWebView2WebErrorStatus status)
        {
            var name = status.ToString();
            var text = new System.Text.StringBuilder(name.Length + 8);
            foreach (var c in name)
            {
                if (char.IsUpper(c) && text.Length > 0)
                    text.Append(' ').Append(char.ToLowerInvariant(c));
                else
                    text.Append(c);
            }
            return text.ToString();
        }

        // target="_blank" links and window.open. Handled = true: WebView2 never opens its own popup window.
        void OnNewWindowRequested(CoreWebView2 sender, CoreWebView2NewWindowRequestedEventArgs args)
        {
            args.Handled = true;
            if (Advanced is not { } view || !AdvancedWebView.IsNewWindowUrl(args.Uri))
                return;

            // Popup blocking, as WebKit and Android do with JavaScriptCanOpenWindowsAutomatically off.
            if (!args.IsUserInitiated && !view.JavaScriptCanOpenWindowsAutomatically)
                return;

            // Navigate goes through NavigationStarting, where MAUI raises Navigating.
            if (view.OnNewWindowRequested(args.Uri, args.IsUserInitiated))
                sender.Navigate(args.Uri);
        }

        // A scheme no web page handles (mailto:, tel:, ms-settings:...): WebView2 would ask the user before opening it.
        void OnLaunchingExternalUriScheme(CoreWebView2 sender, CoreWebView2LaunchingExternalUriSchemeEventArgs args)
        {
            if (Advanced is { } view && view.TryOpenExternalLink(args.Uri, isMainFrame: true))
                args.Cancel = true;
        }

        // Invalid server certificates. Rejected unless the app sets Proceed: the load then fails with SslError.
        void OnServerCertificateErrorDetected(CoreWebView2 sender, CoreWebView2ServerCertificateErrorDetectedEventArgs args)
        {
            if (Advanced is not { } view || !Uri.TryCreate(args.RequestUri, UriKind.Absolute, out var uri))
                return;

            var url = uri.GetLeftPart(UriPartial.Authority) + "/";
            args.Action = view.OnServerCertificateError(url, Describe(args.ErrorStatus))
                ? CoreWebView2ServerCertificateErrorAction.AlwaysAllow
                : CoreWebView2ServerCertificateErrorAction.Cancel;
        }

        internal partial string? GetCurrentPageUrl() => core?.Source;

        // LoadUrl / PostUrlAsync. NavigateWithWebResourceRequest goes through NavigationStarting, as a link.
        internal partial void LoadRequest(WebRequest request) => _ = LoadRequestAsync(request);

        async Task LoadRequestAsync(WebRequest request)
        {
            try
            {
                if (core is null)
                    await PlatformView.EnsureCoreWebView2Async();
                if (core is not { } target)
                    return;

                var headers = new List<string>();
                foreach (var (name, value) in request.Headers)
                    headers.Add($"{name}: {value}");

                Windows.Storage.Streams.IRandomAccessStream? content = null;
                if (request.Body is { } body)
                {
                    if (request.ContentType is { } contentType)
                        headers.Add($"Content-Type: {contentType}");
                    var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
                    using (var writer = new Windows.Storage.Streams.DataWriter(stream))
                    {
                        writer.WriteBytes(body);
                        await writer.StoreAsync();
                        writer.DetachStream();
                    }
                    stream.Seek(0);
                    content = stream;
                }

                var nativeRequest = target.Environment.CreateWebResourceRequest(
                    request.Url, request.Body is null ? "GET" : "POST", content, string.Join("\r\n", headers));
                target.NavigateWithWebResourceRequest(nativeRequest);
            }
            catch (Exception ex)
            {
                Log($"Could not load {request.Url}: {ex.Message}");
            }
        }
    }
}
