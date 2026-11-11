using Mafesoft.Maui.Controls.Handlers;

namespace Mafesoft.Maui.Controls
{
    /// <summary>A page load requested from code, with headers and optionally a POST body.</summary>
    internal sealed record WebRequest(string Url, IReadOnlyDictionary<string, string> Headers, byte[]? Body = null, string? ContentType = null);

    // Loading pages with custom headers or a POST body.
    public partial class AdvancedWebView
    {
        const string FormContentType = "application/x-www-form-urlencoded";

        /// <summary>
        /// Loads <paramref name="url"/> with extra HTTP headers, for example <c>Authorization</c>.
        /// <see cref="WebView.Navigating"/> and <see cref="WebView.Navigated"/> are raised as for a link.
        /// </summary>
        /// <remarks>
        /// The headers are not sent with the page's resources (images, scripts, API calls) or with links the user taps.
        /// They can be sent again on redirects, also to another site, and on <c>Reload()</c> and back navigation.
        /// On Windows, <c>Reload()</c> drops them when this was the first page the web view loaded.
        /// See the README section "Headers and POST".
        /// </remarks>
        /// <exception cref="ArgumentException"><paramref name="url"/> is not an absolute URL.</exception>
        /// <exception cref="InvalidOperationException">The control is not attached to its handler yet.</exception>
        public void LoadUrl(string url, IDictionary<string, string>? headers = null) =>
            Load(new WebRequest(ValidateUrl(url), CopyHeaders(headers)));

        /// <summary>
        /// Loads <paramref name="url"/> with a POST request, for example a <see cref="FormUrlEncodedContent"/>.
        /// <see cref="WebView.Navigating"/> and <see cref="WebView.Navigated"/> are raised as for a link.
        /// </summary>
        /// <remarks>
        /// Android only sends <c>application/x-www-form-urlencoded</c> bodies, without extra headers: that is what its
        /// web view supports. iOS sends any content type and the headers.
        /// Headers can be sent again on redirects, also to another site: see the remarks of <see cref="LoadUrl"/>.
        /// </remarks>
        /// <exception cref="ArgumentException"><paramref name="url"/> is not an absolute URL.</exception>
        /// <exception cref="NotSupportedException">Android: the content is not form-urlencoded, or headers were given.</exception>
        /// <exception cref="InvalidOperationException">The control is not attached to its handler yet.</exception>
        public async Task PostUrlAsync(string url, HttpContent content, IDictionary<string, string>? headers = null)
        {
            ArgumentNullException.ThrowIfNull(content);
            var target = ValidateUrl(url);
            var contentType = content.Headers.ContentType?.ToString();
            var extraHeaders = CopyHeaders(headers);

            if (OperatingSystem.IsAndroid())
            {
                if (!string.Equals(content.Headers.ContentType?.MediaType, FormContentType, StringComparison.OrdinalIgnoreCase))
                    throw new NotSupportedException($"On Android, PostUrlAsync only sends {FormContentType} content (for example FormUrlEncodedContent). This content is '{contentType ?? "without a content type"}'.");
                if (extraHeaders.Count > 0)
                    throw new NotSupportedException("On Android, PostUrlAsync cannot send extra headers: the web view does not support them for POST requests.");
            }

            var body = await content.ReadAsByteArrayAsync().ConfigureAwait(false);
            Load(new WebRequest(target, extraHeaders, body, contentType));
        }

        void Load(WebRequest request)
        {
            if (Handler is not AdvancedWebViewHandler handler)
                throw new InvalidOperationException(
                    "AdvancedWebView is not attached to its handler. Call builder.UseAdvancedWebView() in MauiProgram and wait until the control is loaded (for example in the Loaded event).");

            if (Dispatcher is { IsDispatchRequired: true } dispatcher)
                dispatcher.Dispatch(() => handler.LoadRequest(request));
            else
                handler.LoadRequest(request);
        }

        static string ValidateUrl(string url)
        {
            ArgumentException.ThrowIfNullOrEmpty(url);
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                throw new ArgumentException($"'{url}' is not an absolute URL.", nameof(url));
            return uri.AbsoluteUri;
        }

        static IReadOnlyDictionary<string, string> CopyHeaders(IDictionary<string, string>? headers)
        {
            var copy = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (headers is not null)
            {
                foreach (var (name, value) in headers)
                {
                    ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(headers));
                    copy[name] = value ?? string.Empty;
                }
            }
            return copy;
        }
    }
}
