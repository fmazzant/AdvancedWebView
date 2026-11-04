using System.Collections.ObjectModel;

namespace Mafesoft.Maui.Controls
{
    /// <summary>Why a link is opened outside the app.</summary>
    public enum ExternalLinkReason
    {
        /// <summary>Its scheme is in <c>ExternalSchemes</c> (for example <c>tel:</c> or <c>mailto:</c>).</summary>
        Scheme,

        /// <summary>It is a web page whose host is not in <c>InternalHosts</c>.</summary>
        Host,
    }

    /// <summary>Decides which URLs leave the web view. Platform-independent.</summary>
    internal static class ExternalLinkRules
    {
        /// <summary>Schemes handed to the system by default.</summary>
        public static readonly string[] DefaultSchemes = ["tel", "mailto", "sms", "geo", "maps", "whatsapp", "intent", "market", "itms-apps"];

        // Schemes the web view loads itself; they cannot be made external.
        static readonly string[] WebViewSchemes = ["http", "https", "about", "data", "blob", "javascript", "file"];

        /// <summary>
        /// Returns why <paramref name="url"/> must open outside the app, or <see langword="null"/> when the web view loads it.
        /// </summary>
        /// <param name="url">The URL being navigated to.</param>
        /// <param name="externalSchemes">Normalized schemes (lower case, no colon).</param>
        /// <param name="internalHosts">Normalized host rules (<c>example.com</c> or <c>*.example.com</c>); empty means every host.</param>
        /// <param name="isMainFrame">Host rules apply to the top-level page only, so iframes from other hosts still load.</param>
        public static ExternalLinkReason? Classify(string? url, IReadOnlyCollection<string> externalSchemes, IReadOnlyCollection<string> internalHosts, bool isMainFrame)
        {
            var scheme = GetScheme(url);
            if (scheme is null)
                return null;

            if (scheme is "http" or "https")
            {
                if (!isMainFrame || internalHosts.Count == 0 || !Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Host.Length == 0)
                    return null;

                var host = uri.IdnHost.ToLowerInvariant();
                return internalHosts.Any(rule => HostMatches(rule, host)) ? null : ExternalLinkReason.Host;
            }

            return externalSchemes.Contains(scheme) ? ExternalLinkReason.Scheme : null;
        }

        /// <summary>Lower-case scheme of <paramref name="url"/>, or <see langword="null"/> when it has none.</summary>
        public static string? GetScheme(string? url)
        {
            if (string.IsNullOrEmpty(url))
                return null;

            var colon = url.IndexOf(':');
            if (colon <= 0)
                return null;

            var scheme = url[..colon];
            if (!char.IsAsciiLetter(scheme[0]) || !scheme.All(c => char.IsAsciiLetterOrDigit(c) || c is '+' or '-' or '.'))
                return null;

            return scheme.ToLowerInvariant();
        }

        /// <summary><c>example.com</c> matches that host only; <c>*.example.com</c> matches its subdomains, not <c>example.com</c>.</summary>
        public static bool HostMatches(string rule, string host) =>
            rule.StartsWith("*.", StringComparison.Ordinal)
                ? host.EndsWith(rule[1..], StringComparison.Ordinal)
                : host == rule;

        public static string NormalizeScheme(string scheme)
        {
            ArgumentNullException.ThrowIfNull(scheme);
            var s = scheme.Trim().TrimEnd(':').TrimEnd('/').TrimEnd(':').ToLowerInvariant();
            if (GetScheme(s + ":") != s)
                throw new ArgumentException($"'{scheme}' is not a valid URL scheme. Use a name such as \"tel\" or \"mailto\".", nameof(scheme));
            if (WebViewSchemes.Contains(s))
                throw new ArgumentException($"'{s}' URLs are loaded by the web view and cannot be external. To open web sites outside the app, use InternalHosts.", nameof(scheme));
            return s;
        }

        public static string NormalizeHost(string host)
        {
            ArgumentNullException.ThrowIfNull(host);
            var h = host.Trim().ToLowerInvariant();
            var wildcard = h.StartsWith("*.", StringComparison.Ordinal);
            var name = wildcard ? h[2..] : h;

            if (name.Length == 0 || name.Contains('*') || Uri.CheckHostName(name) == UriHostNameType.Unknown)
                throw new ArgumentException($"'{host}' is not a valid host rule. Use \"example.com\" or \"*.example.com\", without scheme or path.", nameof(host));

            // Same normalization as Uri.IdnHost, so rules with Unicode names match real URLs.
            var idn = Uri.TryCreate("http://" + name, UriKind.Absolute, out var uri) ? uri.IdnHost.ToLowerInvariant() : name;
            return wildcard ? "*." + idn : idn;
        }
    }

    /// <summary>A list that normalizes and validates its entries when they are added.</summary>
    internal sealed class NormalizedList : Collection<string>, IReadOnlyCollection<string>
    {
        readonly Func<string, string> normalize;

        public NormalizedList(Func<string, string> normalize, IEnumerable<string>? items = null)
        {
            this.normalize = normalize;
            if (items is not null)
            {
                foreach (var item in items)
                    Add(item);
            }
        }

        protected override void InsertItem(int index, string item)
        {
            var value = normalize(item);
            if (!Contains(value))
                base.InsertItem(index, value);
        }

        protected override void SetItem(int index, string item) => base.SetItem(index, normalize(item));
    }
}
