using System.Globalization;
using System.Net;
using System.Text;

namespace Mafesoft.Maui.Controls
{
    /// <summary>Cookie helpers shared by the platforms. Platform-independent.</summary>
    internal static class CookieRules
    {
        /// <summary>
        /// Parses a <c>Cookie</c> request header (<c>a=1; b=2</c>), as Android's CookieManager returns it.
        /// Only names and values are known: domain is <paramref name="uri"/>'s host and path is <c>/</c>.
        /// </summary>
        public static IReadOnlyList<Cookie> ParseCookieHeader(string? header, Uri uri)
        {
            var result = new List<Cookie>();
            if (string.IsNullOrWhiteSpace(header))
                return result;

            foreach (var part in header.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var equals = part.IndexOf('=');
                var name = (equals < 0 ? part : part[..equals]).Trim();
                var value = equals < 0 ? string.Empty : part[(equals + 1)..].Trim();
                if (name.Length == 0)
                    continue;

                result.Add(new Cookie
                {
                    Name = name,
                    Value = value,
                    Domain = uri.Host,
                    Path = "/",
                });
            }

            return result;
        }

        /// <summary>A <c>Set-Cookie</c> value for <paramref name="cookie"/>, with every attribute it has.</summary>
        public static string ToSetCookie(Cookie cookie)
        {
            var sb = new StringBuilder();
            sb.Append(cookie.Name).Append('=').Append(cookie.Value);
            if (!string.IsNullOrEmpty(cookie.Domain))
                sb.Append("; Domain=").Append(cookie.Domain);
            sb.Append("; Path=").Append(string.IsNullOrEmpty(cookie.Path) ? "/" : cookie.Path);
            if (cookie.Expires != DateTime.MinValue)
                sb.Append("; Expires=").Append(cookie.Expires.ToUniversalTime().ToString("R", CultureInfo.InvariantCulture));
            if (cookie.Secure)
                sb.Append("; Secure");
            if (cookie.HttpOnly)
                sb.Append("; HttpOnly");
            return sb.ToString();
        }

        /// <summary>A <c>Set-Cookie</c> value that deletes the cookie <paramref name="name"/> for the given domain and path.</summary>
        public static string ToExpiredSetCookie(string name, string? domain, string path) =>
            name + "=; Max-Age=0; Expires=Thu, 01 Jan 1970 00:00:00 GMT; Path=" + path + (string.IsNullOrEmpty(domain) ? string.Empty : "; Domain=" + domain);

        /// <summary>The URL a platform cookie store needs to set <paramref name="cookie"/>: scheme from Secure, host from Domain.</summary>
        public static string UrlFor(Cookie cookie)
        {
            if (string.IsNullOrEmpty(cookie.Domain))
                throw new ArgumentException("The cookie needs a Domain, for example new Cookie(\"session\", token, \"/\", \"example.com\").", nameof(cookie));

            var host = cookie.Domain.TrimStart('.');
            var path = string.IsNullOrEmpty(cookie.Path) ? "/" : cookie.Path;
            return (cookie.Secure ? "https://" : "http://") + host + path;
        }

        /// <summary>
        /// Whether a stored cookie is sent to <paramref name="uri"/> (RFC 6265 domain and path matching, Secure on https only).
        /// </summary>
        public static bool Matches(string domain, string path, bool secure, Uri uri)
        {
            if (secure && uri.Scheme != Uri.UriSchemeHttps)
                return false;

            var host = uri.Host.ToLowerInvariant();
            var cookieDomain = domain.ToLowerInvariant();
            var domainMatch = cookieDomain.StartsWith('.')
                ? host == cookieDomain[1..] || host.EndsWith(cookieDomain, StringComparison.Ordinal)
                : host == cookieDomain || host.EndsWith("." + cookieDomain, StringComparison.Ordinal);
            if (!domainMatch)
                return false;

            var requestPath = string.IsNullOrEmpty(uri.AbsolutePath) ? "/" : uri.AbsolutePath;
            var cookiePath = string.IsNullOrEmpty(path) ? "/" : path;
            return requestPath == cookiePath ||
                   (requestPath.StartsWith(cookiePath, StringComparison.Ordinal) &&
                    (cookiePath.EndsWith('/') || requestPath[cookiePath.Length] == '/'));
        }

        /// <summary>
        /// Domains to try when deleting a cookie whose domain is unknown (Android): the host and each parent domain,
        /// with and without the leading dot, plus no domain at all (host-only cookie).
        /// </summary>
        public static IEnumerable<string?> CandidateDomains(string host)
        {
            yield return null;
            var labels = host.Split('.');
            for (var i = 0; i < labels.Length - 1; i++)
            {
                var domain = string.Join('.', labels[i..]);
                yield return domain;
                yield return "." + domain;
            }
        }
    }
}
