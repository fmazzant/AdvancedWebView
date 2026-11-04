namespace Mafesoft.Maui.Controls
{
    /// <summary>Settings of the JavaScript bridge of an <see cref="AdvancedWebView"/>.</summary>
    public sealed class BridgeOptions
    {
        OriginRuleList allowedOrigins = new();

        /// <summary>
        /// Remote origins whose pages may use the bridge (messages and handlers).
        /// Rules: <c>"https://example.com"</c>, <c>"https://example.com:8443"</c>, <c>"https://*.example.com"</c>
        /// (subdomains only) or <c>"*"</c> (any site; not recommended).
        /// </summary>
        /// <remarks>
        /// Local content (an <see cref="HtmlWebViewSource"/> or files packaged with the app) can always use the bridge.
        /// With an empty list, which is the default, remote pages cannot use it: their messages are dropped
        /// and their <c>invoke</c> calls are rejected with <c>OriginNotAllowed</c>.
        /// </remarks>
        /// <exception cref="ArgumentException">A rule is not valid.</exception>
        public IList<string> AllowedOrigins
        {
            get => allowedOrigins;
            set => allowedOrigins = new OriginRuleList(value);
        }

        /// <summary>
        /// Whether iframes (with an allowed origin) may use the bridge. Defaults to <see langword="false"/>:
        /// only the top-level page can.
        /// </summary>
        public bool AllowSubframes { get; set; }

        /// <summary>
        /// How long a page call to <c>AdvancedWebView.invoke</c>, or a C# call to <c>InvokeJavaScriptAsync</c>,
        /// may run before it fails with a timeout. Defaults to 30 seconds. Use <see cref="Timeout.InfiniteTimeSpan"/> to disable.
        /// </summary>
        public TimeSpan InvokeTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Whether rejected Promises include the full exception text (<c>error.details</c>).
        /// Defaults to <see langword="false"/>: only <see cref="Exception.Message"/> reaches the page.
        /// Enable it only while debugging.
        /// </summary>
        public bool IncludeExceptionDetails { get; set; }

        internal bool IsOriginAllowed(Uri? origin) => OriginRule.IsLocal(origin) || allowedOrigins.Matches(origin);
    }
}
