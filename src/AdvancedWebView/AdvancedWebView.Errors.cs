namespace Mafesoft.Maui.Controls
{
    /// <summary>The top-level page failed to load.</summary>
    public sealed class NavigationFailedEventArgs : EventArgs
    {
        internal NavigationFailedEventArgs(string url, WebNavigationError error, int? httpStatusCode, string description, bool showErrorPage)
        {
            Url = url;
            Error = error;
            HttpStatusCode = httpStatusCode;
            Description = description;
            ShowErrorPage = showErrorPage;
        }

        /// <summary>The URL that failed.</summary>
        public string Url { get; }

        /// <summary>Why it failed.</summary>
        public WebNavigationError Error { get; }

        /// <summary>The HTTP status code for <see cref="WebNavigationError.HttpError"/>; otherwise <see langword="null"/>.</summary>
        public int? HttpStatusCode { get; }

        /// <summary>The platform's description of the error (for example <c>net::ERR_NAME_NOT_RESOLVED</c>).</summary>
        public string Description { get; }

        /// <summary>
        /// Whether <see cref="AdvancedWebView.ErrorPageHtml"/> replaces the page. Starts as <see langword="true"/> when
        /// <see cref="AdvancedWebView.ErrorPageHtml"/> is set and the error is not <see cref="WebNavigationError.Cancelled"/>.
        /// Set it to <see langword="false"/> to keep, for example, the server's own 404 page.
        /// </summary>
        public bool ShowErrorPage { get; set; }
    }

    /// <summary>The server's TLS certificate is not valid. Set <see cref="Proceed"/> to load the page anyway.</summary>
    public sealed class ServerCertificateErrorEventArgs : EventArgs
    {
        internal ServerCertificateErrorEventArgs(string url, string description)
        {
            Url = url;
            Description = description;
        }

        /// <summary>The URL whose certificate failed (the page or one of its resources).</summary>
        public string Url { get; }

        /// <summary>What is wrong with the certificate, as reported by the platform.</summary>
        public string Description { get; }

        /// <summary>
        /// Set to <see langword="true"/> to accept the certificate. Defaults to <see langword="false"/>.
        /// Accepting invalid certificates exposes the connection to interception: do it only during development,
        /// for example for a local server with a self-signed certificate.
        /// </summary>
        public bool Proceed { get; set; }
    }

    // Load failures: NavigationFailed, the custom error page and certificate errors.
    public partial class AdvancedWebView
    {
        /// <summary>Bindable property for <see cref="ErrorPageHtml"/>.</summary>
        public static readonly BindableProperty ErrorPageHtmlProperty =
            BindableProperty.Create(nameof(ErrorPageHtml), typeof(string), typeof(AdvancedWebView), null);

        /// <summary>
        /// HTML shown instead of the platform's error page when the top-level page fails to load. <see langword="null"/>,
        /// the default, keeps the platform's page.
        /// Placeholders, HTML-encoded: <c>{{url}}</c>, <c>{{description}}</c>, <c>{{error}}</c>, <c>{{statusCode}}</c>.
        /// A button calling <c>AdvancedWebView.retry()</c> loads the failed URL again (it needs JavaScript and the bridge).
        /// </summary>
        /// <remarks>The page is loaded as local content, so it can use the bridge whatever <see cref="BridgeOptions.AllowedOrigins"/> says.</remarks>
        public string? ErrorPageHtml
        {
            get => (string?)GetValue(ErrorPageHtmlProperty);
            set => SetValue(ErrorPageHtmlProperty, value);
        }

        /// <summary>
        /// Raised on the main thread when the top-level page fails to load: network errors, TLS errors and HTTP 4xx/5xx answers.
        /// Iframes and resources such as images are not reported. <see cref="WebView.Navigated"/> is still raised as usual.
        /// </summary>
        public event EventHandler<NavigationFailedEventArgs>? NavigationFailed;

        /// <summary>
        /// Raised on the main thread when a server's TLS certificate is not valid. Without a handler that sets
        /// <see cref="ServerCertificateErrorEventArgs.Proceed"/>, the connection is rejected and the page fails with
        /// <see cref="WebNavigationError.SslError"/>.
        /// </summary>
        public event EventHandler<ServerCertificateErrorEventArgs>? ServerCertificateError;

        /// <summary>
        /// Raises <see cref="NavigationFailed"/>. Returns the error page to load, or <see langword="null"/> to keep the current page.
        /// </summary>
        internal string? OnNavigationFailed(string url, WebNavigationError error, int? httpStatusCode, string? description)
        {
            var template = ErrorPageHtml;
            var e = new NavigationFailedEventArgs(url, error, httpStatusCode, description ?? string.Empty,
                showErrorPage: template is not null && error != WebNavigationError.Cancelled);
            NavigationFailed?.Invoke(this, e);

            return e.ShowErrorPage && template is not null
                ? ErrorPage.Render(template, url, error, httpStatusCode, e.Description)
                : null;
        }

        /// <summary>Raises <see cref="ServerCertificateError"/>. Returns whether to accept the certificate.</summary>
        internal bool OnServerCertificateError(string url, string description)
        {
            var e = new ServerCertificateErrorEventArgs(url, description);
            ServerCertificateError?.Invoke(this, e);
            return e.Proceed;
        }

        /// <summary>Whether a failure must be reported: not for blank pages, nor for local HTML such as the error page itself.</summary>
        internal static bool IsReportableUrl(string? url) =>
            ExternalLinkRules.GetScheme(url) is "http" or "https" or "file";
    }
}
