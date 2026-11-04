namespace Mafesoft.Maui.Controls
{
    /// <summary>When a <see cref="UserScript"/> runs.</summary>
    public enum UserScriptInjectionTime
    {
        /// <summary>Before any script of the page runs. The DOM is still empty.</summary>
        DocumentStart,

        /// <summary>After the page has loaded (DOM ready).</summary>
        DocumentEnd,
    }

    /// <summary>
    /// JavaScript injected into every page an <see cref="AdvancedWebView"/> loads. Add it to <see cref="AdvancedWebView.UserScripts"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// webView.UserScripts.Add(new UserScript("document.documentElement.classList.add('in-app');"));
    /// webView.UserScripts.Add(new UserScript { File = "inject.js", InjectionTime = UserScriptInjectionTime.DocumentStart });
    /// </code>
    /// </example>
    public sealed class UserScript
    {
        OriginRuleList allowedOrigins = new();
        string? cachedFile;
        string? cachedFileSource;

        /// <summary>Creates an empty script; set <see cref="Source"/> or <see cref="File"/>.</summary>
        public UserScript()
        {
        }

        /// <summary>Creates a script from source code.</summary>
        public UserScript(string source, UserScriptInjectionTime injectionTime = UserScriptInjectionTime.DocumentEnd, bool mainFrameOnly = true)
        {
            Source = source;
            InjectionTime = injectionTime;
            MainFrameOnly = mainFrameOnly;
        }

        /// <summary>The JavaScript code. Ignored when <see cref="File"/> is set.</summary>
        public string? Source { get; set; }

        /// <summary>
        /// A file packaged with the app as a <c>MauiAsset</c> (for example <c>Resources/Raw/inject.js</c> is <c>"inject.js"</c>).
        /// Takes precedence over <see cref="Source"/>.
        /// </summary>
        public string? File { get; set; }

        /// <summary>When the script runs. Defaults to <see cref="UserScriptInjectionTime.DocumentEnd"/>.</summary>
        public UserScriptInjectionTime InjectionTime { get; set; } = UserScriptInjectionTime.DocumentEnd;

        /// <summary>
        /// Whether the script runs only in the top-level page, not in iframes. Defaults to <see langword="true"/>.
        /// On Android, <see cref="UserScriptInjectionTime.DocumentEnd"/> scripts always run only in the top-level page.
        /// </summary>
        public bool MainFrameOnly { get; set; } = true;

        /// <summary>
        /// Sites where the script runs, with the same rules as <see cref="BridgeOptions.AllowedOrigins"/>
        /// (for example <c>"https://*.example.com"</c>). Empty, the default, means every page.
        /// </summary>
        /// <exception cref="ArgumentException">A rule is not valid.</exception>
        public IList<string> AllowedOrigins
        {
            get => allowedOrigins;
            set => allowedOrigins = new OriginRuleList(value);
        }

        internal IReadOnlyCollection<OriginRule> OriginRules => allowedOrigins.Rules.ToList();

        /// <summary>Returns the code to inject, reading <see cref="File"/> from the app package when set.</summary>
        internal string? ResolveSource()
        {
            if (string.IsNullOrEmpty(File))
                return Source;

            if (cachedFile != File)
            {
                // App package files are local assets: the platform implementations complete synchronously.
                using var stream = FileSystem.OpenAppPackageFileAsync(File).GetAwaiter().GetResult();
                using var reader = new StreamReader(stream);
                cachedFileSource = reader.ReadToEnd();
                cachedFile = File;
            }

            return cachedFileSource;
        }
    }
}
