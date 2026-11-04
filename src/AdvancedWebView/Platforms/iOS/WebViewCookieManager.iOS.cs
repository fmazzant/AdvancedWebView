using System.Net;
using Foundation;
using WebKit;

namespace Mafesoft.Maui.Controls
{
    // iOS: WKHTTPCookieStore of the default data store, the one MAUI's web views use. It must be used on the main thread.
    public sealed partial class WebViewCookieManager
    {
        static WKHttpCookieStore Store => WKWebsiteDataStore.DefaultDataStore.HttpCookieStore;

        Task<IReadOnlyList<Cookie>> GetCookiesCoreAsync(Uri uri) =>
            MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var cookies = await Store.GetAllCookiesAsync();
                return (IReadOnlyList<Cookie>)cookies
                    .Where(c => CookieRules.Matches(c.Domain, c.Path, c.IsSecure, uri))
                    .Select(ToCookie)
                    .ToList();
            });

        Task SetCookieCoreAsync(Cookie cookie) =>
            MainThread.InvokeOnMainThreadAsync(() => Store.SetCookieAsync(new NSHttpCookie(cookie)));

        Task DeleteCookiesCoreAsync(Uri uri) =>
            MainThread.InvokeOnMainThreadAsync(async () =>
            {
                foreach (var cookie in await Store.GetAllCookiesAsync())
                {
                    if (CookieRules.Matches(cookie.Domain, cookie.Path, cookie.IsSecure, uri))
                        await Store.DeleteCookieAsync(cookie);
                }
            });

        Task ClearAllCoreAsync() =>
            MainThread.InvokeOnMainThreadAsync(() =>
                WKWebsiteDataStore.DefaultDataStore.RemoveDataOfTypesAsync(new NSSet<NSString>(WKWebsiteDataType.Cookies), NSDate.DistantPast));

        static Cookie ToCookie(NSHttpCookie c)
        {
            var cookie = new Cookie
            {
                Name = c.Name,
                Value = c.Value,
                Domain = c.Domain,
                Path = c.Path,
                Secure = c.IsSecure,
                HttpOnly = c.IsHttpOnly,
            };
            if (c.ExpiresDate is { } expires)
                cookie.Expires = ((DateTime)expires).ToLocalTime();
            return cookie;
        }
    }

    public partial class AdvancedWebView
    {
        Task ClearWebsiteDataCoreAsync(WebsiteDataTypes types)
        {
            var dataTypes = new List<NSString>();
            if (types.HasFlag(WebsiteDataTypes.Cookies))
                dataTypes.Add(WKWebsiteDataType.Cookies);
            if (types.HasFlag(WebsiteDataTypes.LocalStorage))
                dataTypes.Add(WKWebsiteDataType.LocalStorage);
            if (types.HasFlag(WebsiteDataTypes.SessionStorage))
                dataTypes.Add(WKWebsiteDataType.SessionStorage);
            if (types.HasFlag(WebsiteDataTypes.IndexedDb))
                dataTypes.Add(WKWebsiteDataType.IndexedDBDatabases);
            if (types.HasFlag(WebsiteDataTypes.Cache))
            {
                dataTypes.Add(WKWebsiteDataType.DiskCache);
                dataTypes.Add(WKWebsiteDataType.MemoryCache);
                dataTypes.Add(WKWebsiteDataType.FetchCache);
            }
            if (types.HasFlag(WebsiteDataTypes.ServiceWorkers))
                dataTypes.Add(WKWebsiteDataType.ServiceWorkerRegistrations);

            if (dataTypes.Count == 0)
                return Task.CompletedTask;

            return MainThread.InvokeOnMainThreadAsync(() =>
                WKWebsiteDataStore.DefaultDataStore.RemoveDataOfTypesAsync(new NSSet<NSString>(dataTypes.ToArray()), NSDate.DistantPast));
        }
    }
}
