namespace Mafesoft.Maui.Controls
{
    /// <summary>The HTTP authentication scheme a server asks for.</summary>
    public enum HttpAuthenticationScheme
    {
        /// <summary>Not reported by the platform (Android), or another scheme.</summary>
        Unknown,

        /// <summary>Basic: user name and password sent base64-encoded. Use it only over https.</summary>
        Basic,

        /// <summary>Digest.</summary>
        Digest,

        /// <summary>NTLM (Windows domains).</summary>
        Ntlm,

        /// <summary>Negotiate: Kerberos, or NTLM as a fallback.</summary>
        Negotiate,
    }

    /// <summary>Parsing of HTTP authentication challenges (#54).</summary>
    internal static class HttpAuthenticationChallenge
    {
        /// <summary>The scheme for its name in a <c>WWW-Authenticate</c> header or a platform constant.</summary>
        public static HttpAuthenticationScheme ParseScheme(string? name) => name?.Trim().ToLowerInvariant() switch
        {
            "basic" or "nsurlauthenticationmethodhttpbasic" => HttpAuthenticationScheme.Basic,
            "digest" or "nsurlauthenticationmethodhttpdigest" => HttpAuthenticationScheme.Digest,
            "ntlm" or "nsurlauthenticationmethodntlm" => HttpAuthenticationScheme.Ntlm,
            "negotiate" or "nsurlauthenticationmethodnegotiate" => HttpAuthenticationScheme.Negotiate,
            _ => HttpAuthenticationScheme.Unknown,
        };

        /// <summary>Scheme and realm of a <c>WWW-Authenticate</c> value such as <c>Basic realm="Intranet", charset="UTF-8"</c>.</summary>
        public static (HttpAuthenticationScheme Scheme, string? Realm) Parse(string? challenge)
        {
            if (string.IsNullOrWhiteSpace(challenge))
                return (HttpAuthenticationScheme.Unknown, null);

            var text = challenge.Trim();
            var space = text.IndexOf(' ');
            var scheme = ParseScheme(space < 0 ? text : text[..space]);
            if (space < 0)
                return (scheme, null);

            var parameters = text[(space + 1)..];
            var index = parameters.IndexOf("realm=", StringComparison.OrdinalIgnoreCase);
            // "realm=" must start a parameter, not end another name (such as "xrealm=").
            while (index > 0 && parameters[index - 1] is not (' ' or ','))
                index = parameters.IndexOf("realm=", index + 1, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
                return (scheme, null);

            var value = parameters[(index + "realm=".Length)..];
            if (value.StartsWith('"'))
            {
                var end = value.IndexOf('"', 1);
                return (scheme, end < 0 ? value[1..] : value[1..end]);
            }

            var comma = value.IndexOf(',');
            return (scheme, (comma < 0 ? value : value[..comma]).Trim());
        }
    }

    /// <summary>
    /// How many times credentials were already given for a host and realm since the last page load: the platforms that
    /// do not count failed attempts themselves (Android, Windows) use it for <c>PreviousFailureCount</c>.
    /// </summary>
    internal sealed class HttpAuthenticationAttempts
    {
        readonly Dictionary<string, int> counts = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Returns the attempts made so far for this host and realm, and counts a new one.</summary>
        public int Next(string host, string? realm)
        {
            var key = host + "\n" + realm;
            counts.TryGetValue(key, out var previous);
            counts[key] = previous + 1;
            return previous;
        }

        /// <summary>A page loaded: the credentials were accepted or the user moved on.</summary>
        public void Reset() => counts.Clear();
    }
}
