namespace Mafesoft.Maui.Controls
{
    /// <summary>The color scheme pages see in <c>prefers-color-scheme</c> (iOS).</summary>
    public enum WebViewColorScheme
    {
        /// <summary>Follow the app's theme.</summary>
        Auto,

        /// <summary>Always light.</summary>
        Light,

        /// <summary>Always dark.</summary>
        Dark,
    }

    // Display options: zoom, text size, transparency, dark mode, text selection, link previews.
    public partial class AdvancedWebView
    {
        /// <summary>Bindable property for <see cref="ZoomEnabled"/>.</summary>
        public static readonly BindableProperty ZoomEnabledProperty =
            BindableProperty.Create(nameof(ZoomEnabled), typeof(bool), typeof(AdvancedWebView), true);

        /// <summary>
        /// Whether the user can pinch to zoom. Defaults to <see langword="true"/>. Android hides the on-screen zoom buttons.
        /// On iOS, turning it off adds <c>user-scalable=no</c> to the page's viewport; turning it back on applies from the next page load.
        /// </summary>
        public bool ZoomEnabled
        {
            get => (bool)GetValue(ZoomEnabledProperty);
            set => SetValue(ZoomEnabledProperty, value);
        }

        /// <summary>Bindable property for <see cref="TextZoom"/>.</summary>
        public static readonly BindableProperty TextZoomProperty =
            BindableProperty.Create(nameof(TextZoom), typeof(int), typeof(AdvancedWebView), 100,
                validateValue: (_, value) => value is int zoom && zoom is >= 10 and <= 500);

        /// <summary>
        /// Text size, in percent. Defaults to 100. Use it to follow the system font size.
        /// Android scales the text; iOS scales the whole page (<c>pageZoom</c>, iOS 14 or later).
        /// </summary>
        public int TextZoom
        {
            get => (int)GetValue(TextZoomProperty);
            set => SetValue(TextZoomProperty, value);
        }

        /// <summary>Bindable property for <see cref="IsTransparent"/>.</summary>
        public static readonly BindableProperty IsTransparentProperty =
            BindableProperty.Create(nameof(IsTransparent), typeof(bool), typeof(AdvancedWebView), false);

        /// <summary>
        /// Whether the web view's own background is transparent, so the app shows through where the page has no background.
        /// Defaults to <see langword="false"/>.
        /// </summary>
        public bool IsTransparent
        {
            get => (bool)GetValue(IsTransparentProperty);
            set => SetValue(IsTransparentProperty, value);
        }

        /// <summary>Bindable property for <see cref="PreferredColorScheme"/>.</summary>
        public static readonly BindableProperty PreferredColorSchemeProperty =
            BindableProperty.Create(nameof(PreferredColorScheme), typeof(WebViewColorScheme), typeof(AdvancedWebView), WebViewColorScheme.Auto);

        /// <summary>
        /// iOS only. The color scheme pages see (<c>prefers-color-scheme</c>, form controls). Defaults to <see cref="WebViewColorScheme.Auto"/>.
        /// On Android the web view always follows the app's theme: set <c>Application.Current.UserAppTheme</c> instead.
        /// </summary>
        public WebViewColorScheme PreferredColorScheme
        {
            get => (WebViewColorScheme)GetValue(PreferredColorSchemeProperty);
            set => SetValue(PreferredColorSchemeProperty, value);
        }

        /// <summary>Bindable property for <see cref="AlgorithmicDarkening"/>.</summary>
        public static readonly BindableProperty AlgorithmicDarkeningProperty =
            BindableProperty.Create(nameof(AlgorithmicDarkening), typeof(bool), typeof(AdvancedWebView), false);

        /// <summary>
        /// Android only. Whether pages without a dark theme are darkened automatically while the app's theme is dark.
        /// Defaults to <see langword="false"/>. Needs an Android System WebView that supports it.
        /// </summary>
        public bool AlgorithmicDarkening
        {
            get => (bool)GetValue(AlgorithmicDarkeningProperty);
            set => SetValue(AlgorithmicDarkeningProperty, value);
        }

        /// <summary>Bindable property for <see cref="TextSelectionEnabled"/>.</summary>
        public static readonly BindableProperty TextSelectionEnabledProperty =
            BindableProperty.Create(nameof(TextSelectionEnabled), typeof(bool), typeof(AdvancedWebView), true);

        /// <summary>
        /// Whether the user can select text in the page (form fields always stay editable). Defaults to <see langword="true"/>.
        /// On iOS, turning it off also removes the long-press menu on links and images.
        /// </summary>
        public bool TextSelectionEnabled
        {
            get => (bool)GetValue(TextSelectionEnabledProperty);
            set => SetValue(TextSelectionEnabledProperty, value);
        }

        /// <summary>Bindable property for <see cref="LinkPreviewEnabled"/>.</summary>
        public static readonly BindableProperty LinkPreviewEnabledProperty =
            BindableProperty.Create(nameof(LinkPreviewEnabled), typeof(bool), typeof(AdvancedWebView), true);

        /// <summary>iOS only. Whether a long press on a link shows its preview. Defaults to <see langword="true"/>.</summary>
        public bool LinkPreviewEnabled
        {
            get => (bool)GetValue(LinkPreviewEnabledProperty);
            set => SetValue(LinkPreviewEnabledProperty, value);
        }

        /// <summary>The script that applies <see cref="TextSelectionEnabled"/> and, on iOS, <see cref="ZoomEnabled"/>; empty when nothing to do.</summary>
        internal string DisplayScript() =>
            UserScriptCode.Display(TextSelectionEnabled, disableZoom: OperatingSystem.IsIOS() && !ZoomEnabled);
    }
}
