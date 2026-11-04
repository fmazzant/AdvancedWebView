using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

namespace Mafesoft.Maui.Controls
{
    /// <summary>
    /// An allowed-origin rule: <c>*</c> (any origin), <c>https://example.com</c>, <c>https://example.com:8443</c>
    /// or <c>https://*.example.com</c> (subdomains only, not <c>example.com</c> itself).
    /// </summary>
    internal sealed class OriginRule
    {
        public static readonly OriginRule Any = new(null, null, false, -1);

        OriginRule(string? scheme, string? host, bool subdomains, int port)
        {
            Scheme = scheme;
            Host = host;
            Subdomains = subdomains;
            Port = port;
        }

        /// <summary>Lower-case scheme, or null for <see cref="Any"/>.</summary>
        public string? Scheme { get; }

        /// <summary>Lower-case host without the <c>*.</c> prefix, or null for <see cref="Any"/>.</summary>
        public string? Host { get; }

        /// <summary>Whether the rule matches subdomains of <see cref="Host"/> (<c>*.</c> prefix).</summary>
        public bool Subdomains { get; }

        /// <summary>Port, the scheme's default when the rule has none, or -1 for <see cref="Any"/>.</summary>
        public int Port { get; }

        public bool IsAny => Scheme is null;

        public static bool TryParse(string? text, [NotNullWhen(true)] out OriginRule? rule)
        {
            rule = null;
            text = text?.Trim();
            if (string.IsNullOrEmpty(text))
                return false;

            if (text == "*")
            {
                rule = Any;
                return true;
            }

            var schemeEnd = text.IndexOf("://", StringComparison.Ordinal);
            if (schemeEnd <= 0)
                return false;

            var scheme = text[..schemeEnd].ToLowerInvariant();
            var rest = text[(schemeEnd + 3)..].TrimEnd('/');
            if (rest.Length == 0 || rest.Contains('/') || rest.Contains('?') || rest.Contains('#') || rest.Contains('@'))
                return false;

            var subdomains = rest.StartsWith("*.", StringComparison.Ordinal);
            if (subdomains)
                rest = rest[2..];
            if (rest.Contains('*'))
                return false;

            // Parse "host[:port]" through Uri, so IDN and IPv6 hosts are normalized the same way as real origins.
            if (!Uri.TryCreate(scheme + "://" + rest, UriKind.Absolute, out var uri) || uri.AbsolutePath != "/" || string.IsNullOrEmpty(uri.Host))
                return false;

            rule = new OriginRule(scheme, uri.IdnHost.ToLowerInvariant(), subdomains, uri.Port);
            return true;
        }

        public static OriginRule Parse(string text) =>
            TryParse(text, out var rule)
                ? rule
                : throw new ArgumentException(
                    $"'{text}' is not a valid origin rule. Use \"*\", \"https://example.com\", \"https://example.com:8443\" or \"https://*.example.com\".",
                    nameof(text));

        public bool Matches(Uri? origin)
        {
            if (IsAny)
                return true;
            if (origin is null || !origin.IsAbsoluteUri)
                return false;
            if (!string.Equals(origin.Scheme, Scheme, StringComparison.OrdinalIgnoreCase) || origin.Port != Port)
                return false;

            var host = origin.IdnHost.ToLowerInvariant();
            return Subdomains
                ? host.EndsWith("." + Host, StringComparison.Ordinal)
                : host == Host;
        }

        /// <summary>Content packaged with the app or set as HTML: no origin, or a file:// origin.</summary>
        public static bool IsLocal(Uri? origin) =>
            origin is null || string.Equals(origin.Scheme, Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A list of origin rules that rejects invalid entries when they are added.</summary>
    internal sealed class OriginRuleList : Collection<string>
    {
        public OriginRuleList()
        {
        }

        public OriginRuleList(IEnumerable<string>? rules)
        {
            if (rules is not null)
            {
                foreach (var rule in rules)
                    Add(rule);
            }
        }

        public IEnumerable<OriginRule> Rules => this.Select(OriginRule.Parse);

        public bool Matches(Uri? origin) => this.Any(r => OriginRule.Parse(r).Matches(origin));

        protected override void InsertItem(int index, string item)
        {
            OriginRule.Parse(item);
            base.InsertItem(index, item.Trim());
        }

        protected override void SetItem(int index, string item)
        {
            OriginRule.Parse(item);
            base.SetItem(index, item.Trim());
        }
    }
}
