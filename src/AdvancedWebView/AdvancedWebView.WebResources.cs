using Mafesoft.Maui.Controls.Handlers;

namespace Mafesoft.Maui.Controls
{
    /// <summary>
    /// The web view is about to load a resource: a page, an iframe, a script, a style sheet, an image, a
    /// <c>fetch()</c> call... Set <see cref="Response"/> to answer it from the app, leave it null to let the web view
    /// load it. For an asynchronous answer (for example a file read), call <see cref="GetDeferral"/> before the first
    /// <c>await</c> and dispose it after.
    /// </summary>
    public sealed class WebResourceRequestEventArgs : EventArgs
    {
        readonly EventDeferral deferral = new();
        Dictionary<string, string>? addedHeaders;

        internal WebResourceRequestEventArgs(string url, string method, IReadOnlyDictionary<string, string> headers, WebResourceType resourceType, bool isCustomScheme)
        {
            Url = url;
            Method = method;
            Headers = headers;
            ResourceType = resourceType;
            IsCustomScheme = isCustomScheme;
        }

        /// <summary>The URL of the request, for example <c>app://localhost/style.css</c>.</summary>
        public string Url { get; }

        /// <summary>The HTTP method, for example <c>GET</c>.</summary>
        public string Method { get; }

        /// <summary>The request headers the platform reports (WebKit and Android do not report all of them, such as cookies).</summary>
        public IReadOnlyDictionary<string, string> Headers { get; }

        /// <summary>
        /// What the request loads. Windows reports every type; Android only tells the top-level page
        /// (<see cref="WebResourceType.Document"/>) from the rest (<see cref="WebResourceType.Unknown"/>); iOS and Mac
        /// Catalyst report <see cref="WebResourceType.Unknown"/>.
        /// </summary>
        public WebResourceType ResourceType { get; }

        /// <summary>
        /// Whether the URL uses one of <see cref="AdvancedWebView.CustomSchemes"/>. Such a request has no other source:
        /// without a <see cref="Response"/> it fails with 404 Not Found.
        /// </summary>
        public bool IsCustomScheme { get; }

        /// <summary>
        /// The answer of the app, or null (the default) to let the web view load the resource itself.
        /// Use <c>new WebResourceResponse(403)</c> to block a request.
        /// </summary>
        public WebResourceResponse? Response { get; set; }

        /// <summary>
        /// Windows only: adds or replaces a header of the request the web view sends (when <see cref="Response"/> is null).
        /// Android and WebKit do not let an app change the headers of the requests they send: there it has no effect.
        /// </summary>
        public void SetRequestHeader(string name, string value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            ArgumentNullException.ThrowIfNull(value);
            (addedHeaders ??= new(StringComparer.OrdinalIgnoreCase))[name] = value;
        }

        /// <summary>The headers given with <see cref="SetRequestHeader"/>.</summary>
        internal IReadOnlyDictionary<string, string>? AddedHeaders => addedHeaders;

        /// <summary>
        /// Delays the answer until the returned object is disposed, so <see cref="Response"/> can be set after an <c>await</c>.
        /// The request waits meanwhile: answer quickly.
        /// </summary>
        /// <example>
        /// <code>
        /// using var deferral = e.GetDeferral();
        /// e.Response = new WebResourceResponse(await FileSystem.OpenAppPackageFileAsync("index.html"), "text/html");
        /// </code>
        /// </example>
        public IDisposable GetDeferral() => deferral.GetDeferral();

        /// <summary>Completes once the event handlers have returned and every deferral is disposed.</summary>
        internal Task WhenAnsweredAsync() => deferral.WhenDoneAsync();
    }

    // Web request interception and custom URL schemes (#55).
    public partial class AdvancedWebView
    {
        NormalizedList customSchemes = new(WebResourceRules.NormalizeCustomScheme);
        EventHandler<WebResourceRequestEventArgs>? webResourceRequested;
        readonly object webResourceRequestedGate = new();

        /// <summary>
        /// URL schemes the app serves itself through <see cref="WebResourceRequested"/>, for example <c>app</c> for
        /// <c>app://localhost/index.html</c>. Empty by default.
        /// </summary>
        /// <remarks>
        /// <para>Set them before the control is shown: iOS, Mac Catalyst and Windows register them when the native view is created.</para>
        /// <para>Pages of a custom scheme are app content: they can use the JavaScript bridge without <c>Bridge.AllowedOrigins</c>,
        /// like local HTML. Use a host, such as <c>app://localhost/</c>, so that relative links and the origin work.</para>
        /// </remarks>
        /// <exception cref="ArgumentException">An entry is not a valid scheme, or is a scheme the web view loads itself (such as <c>https</c> or <c>file</c>).</exception>
        public IList<string> CustomSchemes
        {
            get => customSchemes;
            set => customSchemes = new NormalizedList(WebResourceRules.NormalizeCustomScheme, value);
        }

        /// <summary>
        /// Raised before the web view loads a resource of a <see cref="CustomSchemes"/> URL, and, on Android and Windows,
        /// of every http and https URL. Set <see cref="WebResourceRequestEventArgs.Response"/> to answer from the app.
        /// </summary>
        /// <remarks>
        /// <para><b>Threads:</b> on Android it is raised on a background thread of the web view, and the handler must
        /// not touch the UI (use <c>MainThread</c>); on iOS, Mac Catalyst and Windows it is raised on the main thread.</para>
        /// <para><b>Platforms:</b> WebKit (iOS, Mac Catalyst) only lets the app answer custom schemes: http and https
        /// requests are not reported there.</para>
        /// <para>The page waits for the answer: keep the handler fast, and use <see cref="WebResourceRequestEventArgs.GetDeferral"/>
        /// for asynchronous work.</para>
        /// </remarks>
        public event EventHandler<WebResourceRequestEventArgs>? WebResourceRequested
        {
            add
            {
                lock (webResourceRequestedGate)
                    webResourceRequested += value;
                OnWebResourceRequestedChanged();
            }
            remove
            {
                lock (webResourceRequestedGate)
                    webResourceRequested -= value;
                OnWebResourceRequestedChanged();
            }
        }

        /// <summary>Whether a <see cref="WebResourceRequested"/> handler is attached: without one, only custom schemes are intercepted.</summary>
        internal bool HasWebResourceRequestedHandler => webResourceRequested is not null;

        /// <summary>Whether <paramref name="url"/> uses one of <see cref="CustomSchemes"/>. Safe on any thread.</summary>
        internal bool IsCustomSchemeUrl(string? url) => WebResourceRules.IsCustomSchemeUrl(url, customSchemes.ToArray());

        /// <summary>The custom schemes, copied: read by the platforms when the native view is created.</summary>
        internal string[] GetCustomSchemes() => customSchemes.ToArray();

        void OnWebResourceRequestedChanged()
        {
            if (Dispatcher is { IsDispatchRequired: true } dispatcher)
                dispatcher.Dispatch(() => (Handler as AdvancedWebViewHandler)?.UpdateWebResourceFilters());
            else
                (Handler as AdvancedWebViewHandler)?.UpdateWebResourceFilters();
        }

        /// <summary>
        /// Raises <see cref="WebResourceRequested"/> on the calling thread and waits for its deferrals. Null when the
        /// request is not intercepted (no handler, or a handler that failed).
        /// </summary>
        internal async Task<WebResourceRequestEventArgs?> RequestWebResourceAsync(
            string url, string method, IReadOnlyDictionary<string, string> headers, WebResourceType resourceType)
        {
            var handler = webResourceRequested;
            var isCustomScheme = IsCustomSchemeUrl(url);
            if (handler is null)
                return null;

            var e = new WebResourceRequestEventArgs(url, method, headers, resourceType, isCustomScheme);
            try
            {
                handler(this, e);
            }
            catch (Exception ex)
            {
                LogWarning($"WebResourceRequested handler failed for {url}: {ex.Message}");
                return null;
            }

            await e.WhenAnsweredAsync().ConfigureAwait(false);
            return e;
        }

        /// <summary>The answer to a custom-scheme request without a <see cref="WebResourceRequestEventArgs.Response"/>.</summary>
        internal static WebResourceResponse CustomSchemeNotFound() => new(404);
    }
}
