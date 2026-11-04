using System.Net;
using Android.Webkit;
using ACookieManager = Android.Webkit.CookieManager;
using AWebView = Android.Webkit.WebView;

namespace Mafesoft.Maui.Controls
{
    // Android: the process-wide CookieManager. Its callbacks need a Looper thread, so everything runs on the main thread.
    public sealed partial class WebViewCookieManager
    {
        static ACookieManager Store => ACookieManager.Instance ?? throw new InvalidOperationException("Android CookieManager is not available.");

        Task<IReadOnlyList<Cookie>> GetCookiesCoreAsync(Uri uri) =>
            MainThread.InvokeOnMainThreadAsync(() => CookieRules.ParseCookieHeader(Store.GetCookie(uri.AbsoluteUri), uri));

        Task SetCookieCoreAsync(Cookie cookie) =>
            MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var done = new BoolCallback();
                Store.SetCookie(CookieRules.UrlFor(cookie), CookieRules.ToSetCookie(cookie), done);
                await done.Task;
                Store.Flush();
            });

        // The store does not say which domain and path a cookie was set with: expire each name for the host, its parent
        // domains (with and without the leading dot) and the path "/".
        Task DeleteCookiesCoreAsync(Uri uri) =>
            MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var store = Store;
                foreach (var cookie in CookieRules.ParseCookieHeader(store.GetCookie(uri.AbsoluteUri), uri))
                {
                    foreach (var domain in CookieRules.CandidateDomains(uri.Host))
                    {
                        var done = new BoolCallback();
                        store.SetCookie(uri.GetLeftPart(UriPartial.Authority), CookieRules.ToExpiredSetCookie(cookie.Name, domain, "/"), done);
                        await done.Task;
                    }
                }
                store.Flush();
            });

        Task ClearAllCoreAsync() =>
            MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var done = new BoolCallback();
                Store.RemoveAllCookies(done);
                await done.Task;
                Store.Flush();
            });

        /// <summary>A ValueCallback&lt;Boolean&gt; as a Task.</summary>
        internal sealed class BoolCallback : Java.Lang.Object, IValueCallback
        {
            readonly TaskCompletionSource tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public Task Task => tcs.Task;

            public void OnReceiveValue(Java.Lang.Object? value) => tcs.TrySetResult();
        }
    }

    public partial class AdvancedWebView
    {
        // Android cannot clear localStorage, sessionStorage, IndexedDB and service workers separately: WebStorage clears them all.
        Task ClearWebsiteDataCoreAsync(WebsiteDataTypes types) =>
            MainThread.InvokeOnMainThreadAsync(async () =>
            {
                if (types.HasFlag(WebsiteDataTypes.Cookies))
                    await CookieManager.ClearAllAsync();

                if ((types & (WebsiteDataTypes.LocalStorage | WebsiteDataTypes.SessionStorage | WebsiteDataTypes.IndexedDb | WebsiteDataTypes.ServiceWorkers)) != 0)
                    WebStorage.Instance?.DeleteAllData();

                if (types.HasFlag(WebsiteDataTypes.Cache))
                {
                    // ClearCache is an instance method but the cache is shared by every WebView of the app.
                    if (Handler?.PlatformView is AWebView webView)
                    {
                        webView.ClearCache(true);
                    }
                    else
                    {
                        using var temporary = new AWebView(Platform.AppContext);
                        temporary.ClearCache(true);
                        temporary.Destroy();
                    }
                }
            });
    }
}
