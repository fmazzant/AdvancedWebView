namespace Mafesoft.Maui.Controls
{
    /// <summary>What a web request loads, where the platform reports it.</summary>
    public enum WebResourceType
    {
        /// <summary>Not reported by the platform (Android sub-resources, iOS and Mac Catalyst), or another type.</summary>
        Unknown,

        /// <summary>A page or an iframe.</summary>
        Document,

        /// <summary>A style sheet.</summary>
        Stylesheet,

        /// <summary>A script.</summary>
        Script,

        /// <summary>An image.</summary>
        Image,

        /// <summary>Audio or video.</summary>
        Media,

        /// <summary>A web font.</summary>
        Font,

        /// <summary>A <c>fetch()</c> or <c>XMLHttpRequest</c> call.</summary>
        Fetch,

        /// <summary>Anything else the platform reports (web sockets, manifests, pings...).</summary>
        Other,
    }

    /// <summary>
    /// The answer of the app to a web request (<see cref="WebResourceRequestEventArgs.Response"/>): content, content
    /// type, status code and headers. The web view reads and disposes <see cref="Content"/>.
    /// </summary>
    public sealed class WebResourceResponse
    {
        /// <summary>A response with content, for example a file of the app package.</summary>
        /// <param name="content">The body. The web view reads it to the end and disposes it.</param>
        /// <param name="contentType">The MIME type, optionally with a charset, for example <c>text/html; charset=utf-8</c>.
        /// See <see cref="GetContentType(string)"/>.</param>
        /// <param name="statusCode">The HTTP status code. Defaults to 200.</param>
        /// <param name="reasonPhrase">The status text; the standard one for <paramref name="statusCode"/> when null.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="statusCode"/> is not between 100 and 599, or is a redirect (3xx).</exception>
        public WebResourceResponse(Stream content, string contentType, int statusCode = 200, string? reasonPhrase = null)
            : this(statusCode, reasonPhrase)
        {
            ArgumentNullException.ThrowIfNull(content);
            ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
            Content = content;
            ContentType = contentType.Trim();
        }

        /// <summary>
        /// A response without content, for example <c>new WebResourceResponse(403)</c> to block a request or
        /// <c>new WebResourceResponse(404)</c> for a missing file.
        /// </summary>
        /// <param name="statusCode">The HTTP status code.</param>
        /// <param name="reasonPhrase">The status text; the standard one for <paramref name="statusCode"/> when null.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="statusCode"/> is not between 100 and 599, or is a redirect (3xx).</exception>
        public WebResourceResponse(int statusCode, string? reasonPhrase = null)
        {
            // Android rejects redirects (3xx) in an intercepted response; the other platforms would not follow them either.
            if (statusCode is < 100 or > 599 or (>= 300 and < 400))
                throw new ArgumentOutOfRangeException(nameof(statusCode), statusCode, "The status code must be between 100 and 599, and not a redirect (3xx).");

            StatusCode = statusCode;
            ReasonPhrase = string.IsNullOrWhiteSpace(reasonPhrase) ? WebResourceRules.GetReasonPhrase(statusCode) : reasonPhrase.Trim();
        }

        /// <summary>The body, or null for a response without content.</summary>
        public Stream? Content { get; }

        /// <summary>The MIME type with its optional charset, for example <c>text/html; charset=utf-8</c>.</summary>
        public string ContentType { get; } = "text/plain";

        /// <summary>The HTTP status code.</summary>
        public int StatusCode { get; }

        /// <summary>The status text, for example <c>OK</c> or <c>Not Found</c>.</summary>
        public string ReasonPhrase { get; }

        /// <summary>Extra response headers, for example <c>Cache-Control</c>. <c>Content-Type</c> comes from <see cref="ContentType"/>.</summary>
        public IDictionary<string, string> Headers { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// The MIME type of a file from its extension, for example <c>text/html</c> for <c>index.html</c> or
        /// <c>app://localhost/index.html</c>; <c>application/octet-stream</c> when unknown.
        /// </summary>
        public static string GetContentType(string pathOrUrl) => WebResourceRules.GetContentType(pathOrUrl);
    }

    /// <summary>Custom URL schemes and web request interception (#55). Platform-independent.</summary>
    internal static class WebResourceRules
    {
        // Loaded by the engines themselves: WebKit refuses a handler for them, WebView2 cannot register them.
        static readonly string[] ReservedSchemes =
            ["http", "https", "file", "about", "data", "blob", "javascript", "ws", "wss", "ftp", "content", "edge", "chrome", "webkit", "intent", "ms-appx", "ms-appx-web", "ms-appdata"];

        static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            [".html"] = "text/html",
            [".htm"] = "text/html",
            [".css"] = "text/css",
            [".js"] = "text/javascript",
            [".mjs"] = "text/javascript",
            [".json"] = "application/json",
            [".map"] = "application/json",
            [".xml"] = "application/xml",
            [".txt"] = "text/plain",
            [".svg"] = "image/svg+xml",
            [".png"] = "image/png",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".gif"] = "image/gif",
            [".webp"] = "image/webp",
            [".ico"] = "image/x-icon",
            [".bmp"] = "image/bmp",
            [".avif"] = "image/avif",
            [".woff"] = "font/woff",
            [".woff2"] = "font/woff2",
            [".ttf"] = "font/ttf",
            [".otf"] = "font/otf",
            [".mp3"] = "audio/mpeg",
            [".wav"] = "audio/wav",
            [".ogg"] = "audio/ogg",
            [".mp4"] = "video/mp4",
            [".webm"] = "video/webm",
            [".pdf"] = "application/pdf",
            [".wasm"] = "application/wasm",
            [".zip"] = "application/zip",
        };

        /// <summary>Lower-case scheme without colon; throws for invalid names and schemes the engines load themselves.</summary>
        public static string NormalizeCustomScheme(string scheme)
        {
            ArgumentNullException.ThrowIfNull(scheme);
            var s = scheme.Trim().TrimEnd('/').TrimEnd(':').ToLowerInvariant();
            if (ExternalLinkRules.GetScheme(s + ":") != s)
                throw new ArgumentException($"'{scheme}' is not a valid URL scheme. Use a name such as \"app\".", nameof(scheme));
            if (ReservedSchemes.Contains(s))
                throw new ArgumentException($"'{s}' is loaded by the web view itself and cannot be a custom scheme. To intercept http and https requests, use WebResourceRequested.", nameof(scheme));
            return s;
        }

        /// <summary>The custom scheme of <paramref name="url"/> when it is in <paramref name="schemes"/>; otherwise null.</summary>
        public static string? GetCustomScheme(string? url, IEnumerable<string> schemes)
        {
            var scheme = ExternalLinkRules.GetScheme(url);
            return scheme is not null && schemes.Contains(scheme) ? scheme : null;
        }

        /// <summary>Whether <paramref name="url"/> uses one of <paramref name="schemes"/>.</summary>
        public static bool IsCustomSchemeUrl(string? url, IEnumerable<string> schemes) => GetCustomScheme(url, schemes) is not null;

        /// <summary>
        /// The origin of a page served from a custom scheme, for example <c>app://localhost</c> for
        /// <c>app://localhost/index.html</c>; null when <paramref name="url"/> does not use one of <paramref name="schemes"/>
        /// or has no host.
        /// </summary>
        public static Uri? GetCustomSchemeOrigin(string? url, IEnumerable<string> schemes)
        {
            if (GetCustomScheme(url, schemes) is null || !Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Host.Length == 0)
                return null;

            return new Uri(uri.GetLeftPart(UriPartial.Authority).ToLowerInvariant());
        }

        /// <summary>The MIME type for the extension of a path or URL (query and fragment ignored).</summary>
        public static string GetContentType(string pathOrUrl)
        {
            ArgumentNullException.ThrowIfNull(pathOrUrl);
            var path = pathOrUrl;
            var end = path.IndexOfAny(['?', '#']);
            if (end >= 0)
                path = path[..end];

            var slash = path.LastIndexOfAny(['/', '\\']);
            var name = slash >= 0 ? path[(slash + 1)..] : path;
            var dot = name.LastIndexOf('.');
            return dot >= 0 && ContentTypes.TryGetValue(name[dot..], out var type) ? type : "application/octet-stream";
        }

        /// <summary>Splits <c>text/html; charset=utf-8</c> into the MIME type and the charset (null when absent).</summary>
        public static (string MimeType, string? Charset) SplitContentType(string contentType)
        {
            var parts = contentType.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return ("application/octet-stream", null);

            string? charset = null;
            foreach (var part in parts.Skip(1))
            {
                if (part.StartsWith("charset=", StringComparison.OrdinalIgnoreCase))
                    charset = part["charset=".Length..].Trim('"', ' ');
            }

            return (parts[0], string.IsNullOrEmpty(charset) ? null : charset);
        }

        /// <summary>The standard reason phrase of an HTTP status code.</summary>
        public static string GetReasonPhrase(int statusCode) => statusCode switch
        {
            200 => "OK",
            201 => "Created",
            202 => "Accepted",
            204 => "No Content",
            206 => "Partial Content",
            400 => "Bad Request",
            401 => "Unauthorized",
            403 => "Forbidden",
            404 => "Not Found",
            405 => "Method Not Allowed",
            408 => "Request Timeout",
            410 => "Gone",
            415 => "Unsupported Media Type",
            429 => "Too Many Requests",
            500 => "Internal Server Error",
            501 => "Not Implemented",
            502 => "Bad Gateway",
            503 => "Service Unavailable",
            504 => "Gateway Timeout",
            >= 100 and < 200 => "Informational",
            >= 200 and < 300 => "OK",
            >= 400 and < 500 => "Client Error",
            _ => "Server Error",
        };

        /// <summary>
        /// Status line, headers and content type of a response, as the platforms need them: the headers of the
        /// response plus <c>Content-Type</c>.
        /// </summary>
        public static Dictionary<string, string> BuildHeaders(WebResourceResponse response)
        {
            var headers = new Dictionary<string, string>(response.Headers, StringComparer.OrdinalIgnoreCase);
            if (response.Content is not null)
                headers["Content-Type"] = response.ContentType;
            return headers;
        }
    }
}
