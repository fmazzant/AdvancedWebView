namespace Mafesoft.Maui.Controls
{
    /// <summary>A link is about to open outside the app. Set <see cref="Cancel"/> to handle it yourself.</summary>
    public sealed class ExternalLinkOpeningEventArgs : EventArgs
    {
        internal ExternalLinkOpeningEventArgs(string url, ExternalLinkReason reason)
        {
            Url = url;
            Reason = reason;
        }

        /// <summary>The URL of the link.</summary>
        public string Url { get; }

        /// <summary>Whether it leaves the app because of its scheme or because of its host.</summary>
        public ExternalLinkReason Reason { get; }

        /// <summary>
        /// Set to <see langword="true"/> to stop the system from opening the link. The web view does not load it either.
        /// </summary>
        public bool Cancel { get; set; }
    }

    // Links that open outside the app: tel:, mailto: and other app schemes, and web sites outside InternalHosts.
    public partial class AdvancedWebView
    {
        NormalizedList externalSchemes = new(ExternalLinkRules.NormalizeScheme, ExternalLinkRules.DefaultSchemes);
        NormalizedList internalHosts = new(ExternalLinkRules.NormalizeHost);

        /// <summary>
        /// URL schemes handed to the system instead of the web view, which cannot load them.
        /// Defaults to <c>tel</c>, <c>mailto</c>, <c>sms</c>, <c>geo</c>, <c>maps</c>, <c>whatsapp</c>, <c>intent</c>
        /// (Android intent URLs), <c>market</c> and <c>itms-apps</c>.
        /// </summary>
        /// <exception cref="ArgumentException">An entry is not a valid scheme, or is a scheme the web view loads itself (such as <c>https</c>).</exception>
        public IList<string> ExternalSchemes
        {
            get => externalSchemes;
            set => externalSchemes = new NormalizedList(ExternalLinkRules.NormalizeScheme, value);
        }

        /// <summary>
        /// Hosts whose pages stay in the web view: <c>"example.com"</c> (that host only) or <c>"*.example.com"</c> (its subdomains).
        /// Links to other hosts open in the system browser. Empty, the default, keeps every site inside.
        /// Only the top-level page is checked: iframes from other hosts still load.
        /// </summary>
        /// <exception cref="ArgumentException">An entry is not a valid host rule.</exception>
        public IList<string> InternalHosts
        {
            get => internalHosts;
            set => internalHosts = new NormalizedList(ExternalLinkRules.NormalizeHost, value);
        }

        /// <summary>
        /// Raised on the main thread before a link opens outside the app (see <see cref="ExternalSchemes"/> and <see cref="InternalHosts"/>).
        /// <see cref="WebView.Navigating"/> is raised first: a URL cancelled there never gets here.
        /// </summary>
        public event EventHandler<ExternalLinkOpeningEventArgs>? ExternalLinkOpening;

        /// <summary>
        /// Opens <paramref name="url"/> outside the app when the rules say so. Returns <see langword="true"/> when the
        /// web view must not load it (opened outside, or cancelled by the app).
        /// </summary>
        /// <param name="url">The URL being navigated to.</param>
        /// <param name="isMainFrame">Whether the navigation is in the top-level page.</param>
        /// <param name="open">Platform opener for URLs the shared one cannot handle (Android intent URLs); null for <see cref="Launcher"/>.</param>
        internal bool TryOpenExternalLink(string? url, bool isMainFrame, Action<string>? open = null)
        {
            if (url is null || ExternalLinkRules.Classify(url, externalSchemes, internalHosts, isMainFrame) is not { } reason)
                return false;

            var e = new ExternalLinkOpeningEventArgs(url, reason);
            ExternalLinkOpening?.Invoke(this, e);

            if (!e.Cancel)
                (open ?? OpenOutside)(url);
            return true;
        }

        /// <summary>Opens a URL in the system browser or in the app registered for its scheme.</summary>
        internal async void OpenOutside(string url)
        {
            // OpenAsync, not TryOpenAsync: on Android 11+ CanOpenAsync needs <queries> entries in the app manifest
            // and returns false without them, even for https and tel.
            try
            {
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || !await Launcher.Default.OpenAsync(uri))
                    LogWarning($"No app can open {url}.");
            }
            catch (Exception ex)
            {
                LogWarning($"Could not open {url} outside the app: {ex.Message}");
            }
        }
    }
}
