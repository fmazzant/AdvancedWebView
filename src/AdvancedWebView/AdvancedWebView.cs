namespace Mafesoft.Maui.Controls
{
    /// <summary>
    /// A <see cref="WebView"/> that exposes additional platform settings.
    /// Register it with <c>builder.UseAdvancedWebView()</c> in <c>MauiProgram</c>.
    /// </summary>
    /// <remarks>
    /// Use the inherited members of <see cref="WebView"/> for the common features:
    /// <see cref="WebView.UserAgent"/>, <see cref="WebView.Cookies"/>, and the
    /// <see cref="WebView.Navigating"/> event (set <c>Cancel</c> to block a URL).
    /// </remarks>
    public partial class AdvancedWebView : WebView
    {
        /// <summary>Bindable property for <see cref="JavaScriptEnabled"/>.</summary>
        public static readonly BindableProperty JavaScriptEnabledProperty =
            BindableProperty.Create(nameof(JavaScriptEnabled), typeof(bool), typeof(AdvancedWebView), true);

        /// <summary>
        /// Whether JavaScript runs in the page. Defaults to <see langword="true"/>.
        /// On iOS the value is read when the native view is created.
        /// </summary>
        public bool JavaScriptEnabled
        {
            get => (bool)GetValue(JavaScriptEnabledProperty);
            set => SetValue(JavaScriptEnabledProperty, value);
        }

        /// <summary>Bindable property for <see cref="JavaScriptCanOpenWindowsAutomatically"/>.</summary>
        public static readonly BindableProperty JavaScriptCanOpenWindowsAutomaticallyProperty =
            BindableProperty.Create(nameof(JavaScriptCanOpenWindowsAutomatically), typeof(bool), typeof(AdvancedWebView), false);

        /// <summary>
        /// Whether JavaScript can open windows without a user gesture (<c>window.open</c>).
        /// Defaults to <see langword="false"/>. On iOS the value is read when the native view is created.
        /// </summary>
        public bool JavaScriptCanOpenWindowsAutomatically
        {
            get => (bool)GetValue(JavaScriptCanOpenWindowsAutomaticallyProperty);
            set => SetValue(JavaScriptCanOpenWindowsAutomaticallyProperty, value);
        }

        /// <summary>Bindable property for <see cref="ScrollBouncesEnabled"/>.</summary>
        public static readonly BindableProperty ScrollBouncesEnabledProperty =
            BindableProperty.Create(nameof(ScrollBouncesEnabled), typeof(bool), typeof(AdvancedWebView), false);

        /// <summary>
        /// Whether the content bounces (iOS) or shows the overscroll glow (Android) at the scroll edges.
        /// Defaults to <see langword="false"/>.
        /// </summary>
        public bool ScrollBouncesEnabled
        {
            get => (bool)GetValue(ScrollBouncesEnabledProperty);
            set => SetValue(ScrollBouncesEnabledProperty, value);
        }

        /// <summary>Bindable property for <see cref="MediaPlaybackRequiresUserGesture"/>.</summary>
        public static readonly BindableProperty MediaPlaybackRequiresUserGestureProperty =
            BindableProperty.Create(nameof(MediaPlaybackRequiresUserGesture), typeof(bool), typeof(AdvancedWebView), false);

        /// <summary>
        /// Whether audio and video need a user gesture before they play.
        /// Defaults to <see langword="false"/> (autoplay allowed). On iOS the value is read when the native view is created.
        /// </summary>
        public bool MediaPlaybackRequiresUserGesture
        {
            get => (bool)GetValue(MediaPlaybackRequiresUserGestureProperty);
            set => SetValue(MediaPlaybackRequiresUserGestureProperty, value);
        }

        /// <summary>Bindable property for <see cref="AllowsInlineMediaPlayback"/>.</summary>
        public static readonly BindableProperty AllowsInlineMediaPlaybackProperty =
            BindableProperty.Create(nameof(AllowsInlineMediaPlayback), typeof(bool), typeof(AdvancedWebView), true);

        /// <summary>
        /// iOS only. Whether HTML5 video plays inline instead of full screen.
        /// Defaults to <see langword="true"/>. Read when the native view is created.
        /// </summary>
        public bool AllowsInlineMediaPlayback
        {
            get => (bool)GetValue(AllowsInlineMediaPlaybackProperty);
            set => SetValue(AllowsInlineMediaPlaybackProperty, value);
        }

        /// <summary>Bindable property for <see cref="AllowMixedContent"/>.</summary>
        public static readonly BindableProperty AllowMixedContentProperty =
            BindableProperty.Create(nameof(AllowMixedContent), typeof(bool), typeof(AdvancedWebView), false);

        /// <summary>
        /// Android only. Whether an HTTPS page may load HTTP resources.
        /// Defaults to <see langword="false"/>.
        /// </summary>
        public bool AllowMixedContent
        {
            get => (bool)GetValue(AllowMixedContentProperty);
            set => SetValue(AllowMixedContentProperty, value);
        }

        /// <summary>Bindable property for <see cref="AllowFileAccess"/>.</summary>
        public static readonly BindableProperty AllowFileAccessProperty =
            BindableProperty.Create(nameof(AllowFileAccess), typeof(bool), typeof(AdvancedWebView), false);

        /// <summary>
        /// Android only. Whether the page may read <c>file://</c> and <c>content://</c> URLs.
        /// Defaults to <see langword="false"/>.
        /// </summary>
        public bool AllowFileAccess
        {
            get => (bool)GetValue(AllowFileAccessProperty);
            set => SetValue(AllowFileAccessProperty, value);
        }

        /// <summary>Bindable property for <see cref="CacheMode"/>.</summary>
        public static readonly BindableProperty CacheModeProperty =
            BindableProperty.Create(nameof(CacheMode), typeof(WebViewCacheMode), typeof(AdvancedWebView), WebViewCacheMode.Default);

        /// <summary>
        /// Android only. How the HTTP cache is used. Defaults to <see cref="WebViewCacheMode.Default"/>.
        /// </summary>
        public WebViewCacheMode CacheMode
        {
            get => (WebViewCacheMode)GetValue(CacheModeProperty);
            set => SetValue(CacheModeProperty, value);
        }

        /// <summary>
        /// Clears the web view's HTTP cache (memory and disk).
        /// Cookies and local storage are kept.
        /// </summary>
        public void ClearCache() => Handler?.Invoke(nameof(ClearCache));
    }

    /// <summary>How an <see cref="AdvancedWebView"/> uses the HTTP cache (Android only).</summary>
    public enum WebViewCacheMode
    {
        /// <summary>Use cached resources when valid, otherwise load from the network.</summary>
        Default,

        /// <summary>Always load from the network.</summary>
        NoCache,

        /// <summary>Use only cached resources; never touch the network.</summary>
        CacheOnly,

        /// <summary>Use cached resources even when expired; fall back to the network.</summary>
        CacheElseNetwork,
    }
}
