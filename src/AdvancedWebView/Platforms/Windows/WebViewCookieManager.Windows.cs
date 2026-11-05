using System.Net;
using Microsoft.Web.WebView2.Core;

namespace Mafesoft.Maui.Controls
{
    // Windows: the CookieManager of WebView2's default profile, the one MAUI's web views share. WebView2 exposes it only
    // through a CoreWebView2: the calls use a live web view, and wait for the first one when none is loaded yet.
    // CoreWebView2 must be used on the main thread.
    public sealed partial class WebViewCookieManager
    {
        static readonly List<CoreWebView2> cores = [];
        static TaskCompletionSource<CoreWebView2> firstCore = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Called by the handler when its CoreWebView2 is ready (main thread).</summary>
        internal static void Register(CoreWebView2 core)
        {
            cores.Add(core);
            firstCore.TrySetResult(core);
        }

        /// <summary>Called by the handler when its CoreWebView2 goes away (main thread).</summary>
        internal static void Unregister(CoreWebView2 core)
        {
            cores.Remove(core);
            if (cores.Count == 0 && firstCore.Task.IsCompleted)
                firstCore = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        /// <summary>A live CoreWebView2; waits for the first web view to load when there is none. Main thread only.</summary>
        internal static Task<CoreWebView2> GetCoreAsync() =>
            cores.Count > 0 ? Task.FromResult(cores[^1]) : firstCore.Task;

        static Task<T> OnCoreAsync<T>(Func<CoreWebView2, Task<T>> action) =>
            MainThread.InvokeOnMainThreadAsync(async () => await action(await GetCoreAsync()));

        static Task OnCoreAsync(Func<CoreWebView2, Task> action) =>
            MainThread.InvokeOnMainThreadAsync(async () => await action(await GetCoreAsync()));

        Task<IReadOnlyList<Cookie>> GetCookiesCoreAsync(Uri uri) =>
            OnCoreAsync<IReadOnlyList<Cookie>>(async core =>
                (await core.CookieManager.GetCookiesAsync(uri.AbsoluteUri)).Select(ToCookie).ToList());

        Task SetCookieCoreAsync(Cookie cookie) =>
            OnCoreAsync(core =>
            {
                var manager = core.CookieManager;
                var path = string.IsNullOrEmpty(cookie.Path) ? "/" : cookie.Path;
                var native = manager.CreateCookie(cookie.Name, cookie.Value, cookie.Domain, path);
                native.IsSecure = cookie.Secure;
                native.IsHttpOnly = cookie.HttpOnly;
                if (cookie.Expires != DateTime.MinValue)
                    native.Expires = new DateTimeOffset(cookie.Expires.ToUniversalTime()).ToUnixTimeMilliseconds() / 1000d;
                manager.AddOrUpdateCookie(native);
                return Task.CompletedTask;
            });

        Task DeleteCookiesCoreAsync(Uri uri) =>
            OnCoreAsync(async core =>
            {
                foreach (var cookie in await core.CookieManager.GetCookiesAsync(uri.AbsoluteUri))
                    core.CookieManager.DeleteCookie(cookie);
            });

        Task ClearAllCoreAsync() =>
            OnCoreAsync(core =>
            {
                core.CookieManager.DeleteAllCookies();
                return Task.CompletedTask;
            });

        static Cookie ToCookie(CoreWebView2Cookie c)
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
            // Expires: seconds since 1970; -1 for session cookies.
            if (!c.IsSession && c.Expires > 0)
                cookie.Expires = DateTimeOffset.FromUnixTimeMilliseconds((long)(c.Expires * 1000)).LocalDateTime;
            return cookie;
        }
    }

    public partial class AdvancedWebView
    {
        Task ClearWebsiteDataCoreAsync(WebsiteDataTypes types)
        {
            var kinds = (CoreWebView2BrowsingDataKinds)0;
            if (types.HasFlag(WebsiteDataTypes.Cookies))
                kinds |= CoreWebView2BrowsingDataKinds.Cookies;
            if (types.HasFlag(WebsiteDataTypes.LocalStorage))
                kinds |= CoreWebView2BrowsingDataKinds.LocalStorage;
            // sessionStorage lives only as long as the page session: WebView2 has nothing stored to clear.
            if (types.HasFlag(WebsiteDataTypes.IndexedDb))
                kinds |= CoreWebView2BrowsingDataKinds.IndexedDb;
            if (types.HasFlag(WebsiteDataTypes.Cache))
                kinds |= CoreWebView2BrowsingDataKinds.DiskCache | CoreWebView2BrowsingDataKinds.CacheStorage;
            if (types.HasFlag(WebsiteDataTypes.ServiceWorkers))
                kinds |= CoreWebView2BrowsingDataKinds.ServiceWorkers;

            if (kinds == 0)
                return Task.CompletedTask;

            return MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var core = await WebViewCookieManager.GetCoreAsync();
                await core.Profile.ClearBrowsingDataAsync(kinds);
            });
        }
    }
}
