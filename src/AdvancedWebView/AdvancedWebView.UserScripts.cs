using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Mafesoft.Maui.Controls
{
    /// <summary>A user script ready for a platform: final code, injection time and frame scope.</summary>
    internal sealed record ResolvedUserScript(string Code, UserScriptInjectionTime InjectionTime, bool MainFrameOnly);

    // Calling page functions from C#, and scripts/styles injected into every page.
    public partial class AdvancedWebView
    {
        /// <summary>
        /// JavaScript injected into every page. Changes apply from the next page load.
        /// </summary>
        public ObservableCollection<UserScript> UserScripts { get; } = new();

        /// <summary>
        /// CSS added to every page as a <c>&lt;style&gt;</c> element, for example
        /// <c>".cookie-banner { display: none !important; }"</c>. Changes apply from the next page load.
        /// </summary>
        public ObservableCollection<string> UserStyleSheets { get; } = new();

        /// <summary>The user scripts and the style sheets, with origin guards applied, in the order they run.</summary>
        internal IReadOnlyList<ResolvedUserScript> ResolveUserScripts()
        {
            var result = new List<ResolvedUserScript>();

            var styles = UserScriptCode.StyleSheets(UserStyleSheets);
            if (styles.Length > 0)
                result.Add(new ResolvedUserScript(styles, UserScriptInjectionTime.DocumentStart, MainFrameOnly: true));

            foreach (var script in UserScripts)
            {
                var source = script.ResolveSource();
                if (string.IsNullOrWhiteSpace(source))
                    continue;

                var code = UserScriptCode.Wrap(source, script.OriginRules, mainFrameOnly: false);
                result.Add(new ResolvedUserScript(code, script.InjectionTime, script.MainFrameOnly));
            }

            return result;
        }

        #region Calling page functions

        /// <summary>
        /// Calls a JavaScript function of the page by name (for example <c>"app.showToast"</c>) with the given arguments,
        /// serialized as JSON. If the function returns a Promise, it is awaited.
        /// </summary>
        /// <exception cref="JavaScriptException">The function does not exist, threw, or rejected its Promise.</exception>
        /// <exception cref="TimeoutException">The page did not answer within <see cref="BridgeOptions.InvokeTimeout"/>.</exception>
        [RequiresUnreferencedCode(WebViewBridge.ReflectionWarning)]
        [RequiresDynamicCode(WebViewBridge.ReflectionWarning)]
        public Task InvokeJavaScriptAsync(string functionName, params object?[] arguments) =>
            InvokeJavaScriptRawAsync(functionName, SerializeArguments(arguments));

        /// <summary>
        /// Calls a JavaScript function of the page and converts its result (awaited if it is a Promise) to <typeparamref name="T"/>.
        /// <c>null</c> and <c>undefined</c> become <see langword="default"/>.
        /// </summary>
        /// <exception cref="JavaScriptException">The function does not exist, threw, or rejected its Promise.</exception>
        /// <exception cref="TimeoutException">The page did not answer within <see cref="BridgeOptions.InvokeTimeout"/>.</exception>
        [RequiresUnreferencedCode(WebViewBridge.ReflectionWarning)]
        [RequiresDynamicCode(WebViewBridge.ReflectionWarning)]
        public async Task<T?> InvokeJavaScriptAsync<T>(string functionName, params object?[] arguments)
        {
            var json = await InvokeJavaScriptRawAsync(functionName, SerializeArguments(arguments));
            return json is null ? default : JsonSerializer.Deserialize<T>(json, WebViewBridge.JsonOptions);
        }

        /// <summary>
        /// Calls a JavaScript function of the page and converts its result with source-generated metadata (trimming and AOT safe).
        /// </summary>
        /// <param name="functionName">Name of the function, for example <c>"app.getUser"</c>.</param>
        /// <param name="resultTypeInfo">Metadata of the result type.</param>
        /// <param name="argumentsJson">The arguments as a JSON array, for example <c>[42, "x"]</c>; null for none.</param>
        public async Task<T?> InvokeJavaScriptAsync<T>(string functionName, JsonTypeInfo<T> resultTypeInfo, string? argumentsJson = null)
        {
            ArgumentNullException.ThrowIfNull(resultTypeInfo);
            var json = await InvokeJavaScriptRawAsync(functionName, argumentsJson);
            return json is null ? default : JsonSerializer.Deserialize(json, resultTypeInfo);
        }

        /// <summary>
        /// Calls a JavaScript function of the page with arguments given as a JSON array and returns the JSON of the result
        /// (null for <c>null</c> or <c>undefined</c>).
        /// </summary>
        /// <exception cref="ArgumentException"><paramref name="argumentsJson"/> is not a JSON array.</exception>
        public Task<string?> InvokeJavaScriptRawAsync(string functionName, string? argumentsJson = null)
        {
            ArgumentException.ThrowIfNullOrEmpty(functionName);
            var arguments = string.IsNullOrWhiteSpace(argumentsJson) ? "[]" : BridgeProtocol.NormalizeArray(argumentsJson);
            return BridgeCore.CallFunctionAsync(functionName, arguments);
        }

        [RequiresUnreferencedCode(WebViewBridge.ReflectionWarning)]
        [RequiresDynamicCode(WebViewBridge.ReflectionWarning)]
        static string SerializeArguments(object?[]? arguments) =>
            JsonSerializer.Serialize(arguments ?? [], WebViewBridge.JsonOptions);

        #endregion
    }
}
