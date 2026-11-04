using System.Net;

namespace Mafesoft.Maui.Controls
{
    /// <summary>Kinds of data a web view stores, for <see cref="AdvancedWebView.ClearWebsiteDataAsync"/>.</summary>
    [Flags]
    public enum WebsiteDataTypes
    {
        /// <summary>Nothing.</summary>
        None = 0,

        /// <summary>Cookies.</summary>
        Cookies = 1,

        /// <summary><c>localStorage</c>. Android clears it together with <see cref="SessionStorage"/> and <see cref="IndexedDb"/>.</summary>
        LocalStorage = 2,

        /// <summary><c>sessionStorage</c>. Android clears it together with <see cref="LocalStorage"/> and <see cref="IndexedDb"/>.</summary>
        SessionStorage = 4,

        /// <summary>IndexedDB databases. Android clears them together with <see cref="LocalStorage"/> and <see cref="SessionStorage"/>.</summary>
        IndexedDb = 8,

        /// <summary>The HTTP cache (memory and disk).</summary>
        Cache = 16,

        /// <summary>Service worker registrations (iOS). Android clears them with the web storage.</summary>
        ServiceWorkers = 32,

        /// <summary>Everything above.</summary>
        All = Cookies | LocalStorage | SessionStorage | IndexedDb | Cache | ServiceWorkers,
    }

    /// <summary>
    /// Reads and changes the cookies of the web view: Android's <c>CookieManager</c>, iOS's <c>WKHTTPCookieStore</c>
    /// of the default data store. Shared by every <see cref="AdvancedWebView"/> of the app. Works before the control is loaded.
    /// </summary>
    /// <remarks>
    /// Android returns only names and values: the cookies it returns have the URL's host as domain, <c>/</c> as path,
    /// and no expiry, Secure or HttpOnly. iOS returns every attribute, HttpOnly cookies included.
    /// </remarks>
    public sealed partial class WebViewCookieManager
    {
        internal static WebViewCookieManager Instance { get; } = new();

        WebViewCookieManager()
        {
        }

        /// <summary>The cookies the web view sends to <paramref name="uri"/>.</summary>
        public Task<IReadOnlyList<Cookie>> GetCookiesAsync(Uri uri)
        {
            ArgumentNullException.ThrowIfNull(uri);
            return GetCookiesCoreAsync(uri);
        }

        /// <summary>Adds or replaces a cookie. It needs a <see cref="Cookie.Domain"/>, for example <c>new Cookie("session", token, "/", "example.com")</c>.</summary>
        /// <exception cref="ArgumentException">The cookie has no domain.</exception>
        public Task SetCookieAsync(Cookie cookie)
        {
            ArgumentNullException.ThrowIfNull(cookie);
            _ = CookieRules.UrlFor(cookie); // validates the domain
            return SetCookieCoreAsync(cookie);
        }

        /// <summary>
        /// Deletes the cookies the web view sends to <paramref name="uri"/>.
        /// On Android, where path and domain of stored cookies are unknown, cookies with a path other than <c>/</c> may remain.
        /// </summary>
        public Task DeleteCookiesAsync(Uri uri)
        {
            ArgumentNullException.ThrowIfNull(uri);
            return DeleteCookiesCoreAsync(uri);
        }

        /// <summary>Deletes every cookie of the web view.</summary>
        public Task ClearAllAsync() => ClearAllCoreAsync();

        /// <summary>
        /// Copies the cookies the web view sends to <paramref name="uri"/> into <paramref name="container"/>, for example to call
        /// an API with <see cref="HttpClient"/> using the session the user opened in the page.
        /// </summary>
        public async Task CopyToAsync(CookieContainer container, Uri uri)
        {
            ArgumentNullException.ThrowIfNull(container);
            foreach (var cookie in await GetCookiesAsync(uri))
            {
                try
                {
                    container.Add(uri, cookie);
                }
                catch (CookieException)
                {
                    // A value .NET does not accept (for example with a comma): skipped.
                }
            }
        }
    }

    // Cookies and website data.
    public partial class AdvancedWebView
    {
        /// <summary>The cookies of the web view. Shared by every <see cref="AdvancedWebView"/> of the app.</summary>
        /// <remarks>
        /// MAUI's <see cref="WebView.Cookies"/> container is copied into the web view on navigation: when you set it,
        /// its cookies can replace the ones set here.
        /// </remarks>
        public WebViewCookieManager CookieManager => WebViewCookieManager.Instance;

        /// <summary>
        /// Deletes stored website data of every site: cookies, web storage, IndexedDB, cache. Shared by every web view of the app.
        /// </summary>
        public Task ClearWebsiteDataAsync(WebsiteDataTypes types) => ClearWebsiteDataCoreAsync(types);
    }
}
