using Microsoft.Web.WebView2.Core;
using WWebView2 = Microsoft.UI.Xaml.Controls.WebView2;

namespace Mafesoft.Maui.Controls.Handlers
{
    // Windows: custom schemes and WebResourceRequested (#55).
    // Custom schemes must be registered in the options of the WebView2 environment, before CoreWebView2 is created: with
    // CustomSchemes set, the handler creates CoreWebView2 itself, and MAUI's Source mapping (which would create it with
    // the default environment) waits for it. WebResourceRequested is asked of WebView2 with filters: the custom schemes
    // always, every URL while a handler is attached.
    public partial class AdvancedWebViewHandler
    {
        // Shared by the views with the same schemes: one browser process per set of environment options.
        static readonly Dictionary<string, Task<CoreWebView2Environment>> customSchemeEnvironments = [];

        Task? customSchemeInitialization;
        string[] registeredSchemes = [];
        bool allRequestsFiltered;

        /// <summary>Starts CoreWebView2 with the custom schemes registered. Called from CreatePlatformView.</summary>
        void StartCustomSchemes(WWebView2 platformView)
        {
            if (Advanced?.GetCustomSchemes() is not { Length: > 0 } schemes)
                return;

            registeredSchemes = schemes;
            customSchemeInitialization = InitializeWithCustomSchemesAsync(platformView, schemes);
        }

        async Task InitializeWithCustomSchemesAsync(WWebView2 platformView, string[] schemes)
        {
            try
            {
                var environment = await GetCustomSchemeEnvironmentAsync(schemes);
                await platformView.EnsureCoreWebView2Async(environment);
            }
            catch (Exception ex)
            {
                // For example another WebView2 of the app already runs with other environment options.
                Log($"WebView2 could not start with the custom schemes {string.Join(", ", schemes)}: {ex.Message}. " +
                    "Give every AdvancedWebView of the app the same CustomSchemes.");
            }
        }

        static Task<CoreWebView2Environment> GetCustomSchemeEnvironmentAsync(string[] schemes)
        {
            var key = string.Join(",", schemes.Order(StringComparer.Ordinal));
            if (!customSchemeEnvironments.TryGetValue(key, out var environment) || environment.IsFaulted)
                customSchemeEnvironments[key] = environment = CreateCustomSchemeEnvironmentAsync(schemes);
            return environment;
        }

        static async Task<CoreWebView2Environment> CreateCustomSchemeEnvironmentAsync(string[] schemes)
        {
            var options = new CoreWebView2EnvironmentOptions();

            // Same language as WinUI's default environment (as MAUI's HybridWebView does), so that the user data folder
            // and browser process can be shared.
            var languages = Windows.Globalization.ApplicationLanguages.Languages;
            if (languages.Count > 0)
                options.Language = languages[0];

            var registrations = new List<CoreWebView2CustomSchemeRegistration>();
            foreach (var scheme in schemes)
            {
                // app://localhost/...: a host, so relative URLs and the origin work as with https; secure, like https.
                registrations.Add(new CoreWebView2CustomSchemeRegistration(scheme)
                {
                    TreatAsSecure = 1, // a BOOL in this WebView2 projection
                    HasAuthorityComponent = true,
                });
            }
            options.CustomSchemeRegistrations = registrations;

            return await CoreWebView2Environment.CreateWithOptionsAsync(null, null, options);
        }

        /// <summary>Waits until CoreWebView2 exists, creating it with the custom schemes when they are set.</summary>
        async Task EnsureCoreAsync()
        {
            if (customSchemeInitialization is { } initialization)
                await initialization;
            if (core is null && PlatformView is { } platformView)
                await platformView.EnsureCoreWebView2Async();
        }

        // MAUI's Source mapping creates CoreWebView2 with the default environment: with custom schemes, wait for ours.
        static void MapSource(AdvancedWebViewHandler handler, AdvancedWebView view)
        {
            if (handler.customSchemeInitialization is { IsCompleted: false } initialization)
                _ = MapSourceAfterAsync(handler, view, initialization);
            else
                Microsoft.Maui.Handlers.WebViewHandler.MapSource(handler, view);
        }

        static async Task MapSourceAfterAsync(AdvancedWebViewHandler handler, AdvancedWebView view, Task initialization)
        {
            await initialization;
            if (ReferenceEquals(handler.VirtualView, view))
                Microsoft.Maui.Handlers.WebViewHandler.MapSource(handler, view);
        }

        void SetUpWebResources(CoreWebView2 target)
        {
            target.WebResourceRequested += OnWebResourceRequested;
            foreach (var scheme in registeredSchemes)
                target.AddWebResourceRequestedFilter(scheme + ":*", CoreWebView2WebResourceContext.All);
            allRequestsFiltered = false;
            UpdateWebResourceFiltersCore();
        }

        void TearDownWebResources(CoreWebView2 target)
        {
            target.WebResourceRequested -= OnWebResourceRequested;
            foreach (var scheme in registeredSchemes)
                target.RemoveWebResourceRequestedFilter(scheme + ":*", CoreWebView2WebResourceContext.All);
            if (allRequestsFiltered)
                target.RemoveWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
            allRequestsFiltered = false;
        }

        // Every request is reported only while a handler is attached: the filter slows down every load.
        partial void UpdateWebResourceFiltersCore()
        {
            if (core is not { } target)
                return;

            var wanted = Advanced?.HasWebResourceRequestedHandler == true;
            if (wanted == allRequestsFiltered)
                return;

            if (wanted)
                target.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
            else
                target.RemoveWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
            allRequestsFiltered = wanted;
        }

        async void OnWebResourceRequested(CoreWebView2 sender, CoreWebView2WebResourceRequestedEventArgs args)
        {
            if (Advanced is not { } view)
                return;

            var request = args.Request;
            var url = request.Uri;
            var isCustomScheme = WebResourceRules.IsCustomSchemeUrl(url, registeredSchemes);
            if (!isCustomScheme && !view.HasWebResourceRequestedHandler)
                return;

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in request.Headers)
                headers[header.Key] = header.Value;

            var deferral = args.GetDeferral();
            try
            {
                var e = await view.RequestWebResourceAsync(url, request.Method, headers, ToResourceType(args.ResourceContext));
                if (e?.AddedHeaders is { } added && e.Response is null)
                {
                    foreach (var (name, value) in added)
                        request.Headers.SetHeader(name, value);
                }

                var response = e?.Response ?? (isCustomScheme ? AdvancedWebView.CustomSchemeNotFound() : null);
                if (response is not null)
                    args.Response = await CreateResponseAsync(sender.Environment, response);
            }
            catch (Exception ex)
            {
                Log($"WebResourceRequested failed for {url}: {ex.Message}");
            }
            finally
            {
                deferral.Complete();
            }
        }

        static async Task<CoreWebView2WebResourceResponse> CreateResponseAsync(CoreWebView2Environment environment, WebResourceResponse response)
        {
            var headers = string.Join("\r\n", WebResourceRules.BuildHeaders(response).Select(h => $"{h.Key}: {h.Value}"));

            Windows.Storage.Streams.IRandomAccessStream? content = null;
            if (response.Content is { } stream)
            {
                // WebView2 reads the content later, on its own: copy it into memory.
                var memory = new MemoryStream();
                await using (stream)
                    await stream.CopyToAsync(memory);
                memory.Position = 0;
                content = memory.AsRandomAccessStream();
            }

            return environment.CreateWebResourceResponse(content, response.StatusCode, response.ReasonPhrase, headers);
        }

        static WebResourceType ToResourceType(CoreWebView2WebResourceContext context) => context switch
        {
            CoreWebView2WebResourceContext.Document => WebResourceType.Document,
            CoreWebView2WebResourceContext.Stylesheet => WebResourceType.Stylesheet,
            CoreWebView2WebResourceContext.Script => WebResourceType.Script,
            CoreWebView2WebResourceContext.Image => WebResourceType.Image,
            CoreWebView2WebResourceContext.Media => WebResourceType.Media,
            CoreWebView2WebResourceContext.Font => WebResourceType.Font,
            CoreWebView2WebResourceContext.XmlHttpRequest or CoreWebView2WebResourceContext.Fetch => WebResourceType.Fetch,
            CoreWebView2WebResourceContext.All => WebResourceType.Unknown,
            _ => WebResourceType.Other,
        };

        /// <summary>The origin of a page of a custom scheme (app://localhost); null for other URLs.</summary>
        Uri? ToCustomSchemeOrigin(string? url) => WebResourceRules.GetCustomSchemeOrigin(url, registeredSchemes);
    }
}
