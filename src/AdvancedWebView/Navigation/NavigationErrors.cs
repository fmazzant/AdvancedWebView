using System.Net;

namespace Mafesoft.Maui.Controls
{
    /// <summary>Why a page failed to load, the same on every platform.</summary>
    public enum WebNavigationError
    {
        /// <summary>The device is offline (for example, airplane mode).</summary>
        NoInternet,

        /// <summary>The server did not answer in time.</summary>
        Timeout,

        /// <summary>The host name could not be resolved, or the server refused the connection.</summary>
        HostNotFound,

        /// <summary>The secure connection failed, for example because of an invalid certificate.</summary>
        SslError,

        /// <summary>The server answered with an HTTP error status (4xx or 5xx). See <c>HttpStatusCode</c>.</summary>
        HttpError,

        /// <summary>The load was stopped, usually because another navigation replaced it.</summary>
        Cancelled,

        /// <summary>Any other failure. See <c>Description</c>.</summary>
        Unknown,
    }

    /// <summary>Maps the platform error codes to <see cref="WebNavigationError"/>. Platform-independent.</summary>
    internal static class NavigationErrors
    {
        /// <summary>
        /// Android <c>WebResourceError</c>: <c>WebViewClient.ERROR_*</c> code and Chromium description (<c>net::ERR_...</c>).
        /// </summary>
        public static WebNavigationError FromAndroid(int errorCode, string? description)
        {
            // Offline shows up as a host lookup or connect error; only the Chromium description tells it apart.
            if (description is not null &&
                (description.Contains("ERR_INTERNET_DISCONNECTED", StringComparison.Ordinal) ||
                 description.Contains("ERR_NETWORK_CHANGED", StringComparison.Ordinal)))
                return WebNavigationError.NoInternet;

            if (description?.Contains("ERR_ABORTED", StringComparison.Ordinal) == true)
                return WebNavigationError.Cancelled;

            return errorCode switch
            {
                -2 => WebNavigationError.HostNotFound,   // ERROR_HOST_LOOKUP
                -6 => WebNavigationError.HostNotFound,   // ERROR_CONNECT
                -8 => WebNavigationError.Timeout,        // ERROR_TIMEOUT
                -11 => WebNavigationError.SslError,      // ERROR_FAILED_SSL_HANDSHAKE
                _ => WebNavigationError.Unknown,
            };
        }

        /// <summary>iOS <c>NSError</c> of a failed navigation: domain and code.</summary>
        public static WebNavigationError FromNSError(string? domain, long code)
        {
            if (domain == "WebKitErrorDomain")
                return code == 102 ? WebNavigationError.Cancelled : WebNavigationError.Unknown; // 102: frame load interrupted

            if (domain != "NSURLErrorDomain")
                return WebNavigationError.Unknown;

            return code switch
            {
                -999 => WebNavigationError.Cancelled,                      // NSURLErrorCancelled
                -1001 => WebNavigationError.Timeout,                       // NSURLErrorTimedOut
                -1003 or -1004 or -1006 => WebNavigationError.HostNotFound, // cannot find host, cannot connect, DNS lookup failed
                -1005 or -1009 or -1020 => WebNavigationError.NoInternet,  // connection lost, not connected, data not allowed
                >= -1206 and <= -1200 => WebNavigationError.SslError,      // secure connection failed, server certificate errors
                _ => WebNavigationError.Unknown,
            };
        }
    }

    /// <summary>Builds the custom error page from <c>ErrorPageHtml</c>.</summary>
    internal static class ErrorPage
    {
        /// <summary>Global read by <c>AdvancedWebView.retry()</c> in the bridge script.</summary>
        public const string FailedUrlGlobal = "__advancedWebViewFailedUrl";

        /// <summary>
        /// Replaces <c>{{url}}</c>, <c>{{description}}</c>, <c>{{error}}</c> and <c>{{statusCode}}</c> (HTML-encoded) and adds
        /// the script that lets <c>AdvancedWebView.retry()</c> reload <paramref name="url"/>.
        /// </summary>
        public static string Render(string template, string url, WebNavigationError error, int? statusCode, string description)
        {
            var html = template
                .Replace("{{url}}", WebUtility.HtmlEncode(url), StringComparison.Ordinal)
                .Replace("{{description}}", WebUtility.HtmlEncode(description), StringComparison.Ordinal)
                .Replace("{{error}}", error.ToString(), StringComparison.Ordinal)
                .Replace("{{statusCode}}", statusCode?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty, StringComparison.Ordinal);

            // Appended, not prepended: content before <!DOCTYPE> would put the page in quirks mode.
            // JsonString escapes <, > and &, so the URL cannot close the script element.
            return html + "\n<script>window." + FailedUrlGlobal + " = " + BridgeProtocol.JsonString(url) + ";</script>\n";
        }
    }
}
