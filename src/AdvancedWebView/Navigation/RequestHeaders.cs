namespace Mafesoft.Maui.Controls
{
    /// <summary>
    /// Headers given to <c>LoadUrl</c> / <c>PostUrlAsync</c> and the only origin they may be sent to: the origin
    /// (scheme, host and port) of the URL they were given with. A redirect to another origin, https to http included,
    /// must not carry them (#60).
    /// </summary>
    internal sealed class RequestHeaderScope
    {
        public RequestHeaderScope(string url, IReadOnlyDictionary<string, string> headers)
        {
            Origin = GetOrigin(url);
            Headers = headers;
        }

        /// <summary><c>scheme://host:port</c> in lower case, or null when the URL is not http(s): then no URL is allowed.</summary>
        public string? Origin { get; }

        public IReadOnlyDictionary<string, string> Headers { get; }

        /// <summary>Whether the headers may be sent to <paramref name="url"/>.</summary>
        public bool IsAllowed(string? url) => Origin is not null && GetOrigin(url) == Origin;

        /// <summary>Whether this scope sent a header with this name and value.</summary>
        public bool Owns(string name, string? value) =>
            Headers.TryGetValue(name, out var own) && string.Equals(own, value, StringComparison.Ordinal);

        /// <summary>The origin of an http(s) URL as <c>scheme://host:port</c> in lower case; null for anything else.</summary>
        public static string? GetOrigin(string? url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") || uri.Host.Length == 0)
                return null;
            return $"{uri.Scheme}://{uri.IdnHost.ToLowerInvariant()}:{uri.Port}";
        }
    }

    /// <summary>
    /// What a web view did with <c>LoadUrl</c> headers: every scope it sent (to recognize those headers on a later
    /// request, such as a redirect, a reload or a back navigation) and the scope of the current page (for <c>Reload()</c>).
    /// </summary>
    internal sealed class RequestHeaderState
    {
        const int MaxScopes = 16;

        readonly List<RequestHeaderScope> scopes = [];
        RequestHeaderScope? pending;
        bool pendingIsPost;
        RequestHeaderScope? current;
        bool otherNavigation;

        /// <summary>A <c>LoadUrl</c> / <c>PostUrlAsync</c> request is about to be loaded.</summary>
        public void BeginRequest(string url, IReadOnlyDictionary<string, string> headers, bool isPost)
        {
            otherNavigation = false;
            pendingIsPost = isPost;
            if (headers.Count == 0)
            {
                pending = null;
                return;
            }

            var scope = new RequestHeaderScope(url, headers);
            scopes.Add(scope);
            if (scopes.Count > MaxScopes)
                scopes.RemoveAt(0);

            pending = scope;
        }

        /// <summary>A link or a script started a navigation (not LoadUrl, reload or back): its redirects are not ours.</summary>
        public void BeginOtherNavigation()
        {
            pending = null;
            otherNavigation = true;
        }

        /// <summary>A new top-level document was committed (after any redirect).</summary>
        public void OnCommitted(string? url)
        {
            // A POST is not sent again by Reload(): the native reload handles it (and asks before resending a form).
            current = pending is { } scope && !pendingIsPost && scope.IsAllowed(url) ? scope : null;
            pending = null;
            otherNavigation = false;
        }

        /// <summary>
        /// The headers to add to a top-level document request for <paramref name="url"/> while the LoadUrl navigation is
        /// in progress (same-origin redirects included); null for any other request. For engines where the app adds the
        /// headers to each request itself (Windows).
        /// </summary>
        public IReadOnlyDictionary<string, string>? GetPendingHeaders(string? url) =>
            pending is { } scope && scope.IsAllowed(url) ? scope.Headers : null;

        /// <summary>
        /// Whether a top-level redirect to <paramref name="url"/> must be loaded again without headers. For engines that
        /// do not expose the headers of the redirected request (Android): it applies to the LoadUrl navigation, and to the
        /// reload or back navigation of a page loaded with headers, not to links.
        /// </summary>
        public bool MustDropOnRedirect(string? url) =>
            !otherNavigation && (pending ?? current) is { } scope && !scope.IsAllowed(url);

        /// <summary>
        /// The names of the headers in <paramref name="requestHeaders"/> that a scope sent and that must not go to
        /// <paramref name="url"/>. A header also given with the same value for the origin of <paramref name="url"/> is kept.
        /// </summary>
        public IReadOnlyList<string> FindLeakedHeaders(string? url, IEnumerable<KeyValuePair<string, string>> requestHeaders)
        {
            List<string>? leaked = null;
            if (scopes.Count == 0)
                return [];

            foreach (var (name, value) in requestHeaders)
            {
                var sentElsewhere = false;
                var allowedHere = false;
                foreach (var scope in scopes)
                {
                    if (!scope.Owns(name, value))
                        continue;
                    if (scope.IsAllowed(url))
                        allowedHere = true;
                    else
                        sentElsewhere = true;
                }

                if (sentElsewhere && !allowedHere)
                    (leaked ??= []).Add(name);
            }

            return leaked ?? (IReadOnlyList<string>)[];
        }

        /// <summary>
        /// The headers <c>Reload()</c> sends again for <paramref name="pageUrl"/>: those of the current page when it was
        /// loaded by <c>LoadUrl</c> and is still on their origin; otherwise null (native reload).
        /// </summary>
        public IReadOnlyDictionary<string, string>? GetReloadHeaders(string? pageUrl) =>
            current is { } scope && scope.IsAllowed(pageUrl) ? scope.Headers : null;
    }
}
