namespace Mafesoft.Maui.Controls
{
    // Read-only state of the loaded page: progress, loading flag, title and URL. Set by the platform handlers.
    public partial class AdvancedWebView
    {
        static readonly BindablePropertyKey ProgressPropertyKey =
            BindableProperty.CreateReadOnly(nameof(Progress), typeof(double), typeof(AdvancedWebView), 0d);

        /// <summary>Bindable property for <see cref="Progress"/>.</summary>
        public static readonly BindableProperty ProgressProperty = ProgressPropertyKey.BindableProperty;

        /// <summary>
        /// Load progress of the current page, from 0 to 1. It reaches 1 when the page has loaded.
        /// </summary>
        public double Progress => (double)GetValue(ProgressProperty);

        static readonly BindablePropertyKey IsLoadingPropertyKey =
            BindableProperty.CreateReadOnly(nameof(IsLoading), typeof(bool), typeof(AdvancedWebView), false);

        /// <summary>Bindable property for <see cref="IsLoading"/>.</summary>
        public static readonly BindableProperty IsLoadingProperty = IsLoadingPropertyKey.BindableProperty;

        /// <summary>Whether a page is loading. Use it, for example, to show a progress bar only while needed.</summary>
        public bool IsLoading => (bool)GetValue(IsLoadingProperty);

        static readonly BindablePropertyKey TitlePropertyKey =
            BindableProperty.CreateReadOnly(nameof(Title), typeof(string), typeof(AdvancedWebView), null);

        /// <summary>Bindable property for <see cref="Title"/>.</summary>
        public static readonly BindableProperty TitleProperty = TitlePropertyKey.BindableProperty;

        /// <summary>
        /// Title of the current page (<c>document.title</c>), or <see langword="null"/> when it has none.
        /// Updates when the page changes its title.
        /// </summary>
        public string? Title => (string?)GetValue(TitleProperty);

        static readonly BindablePropertyKey CurrentUrlPropertyKey =
            BindableProperty.CreateReadOnly(nameof(CurrentUrl), typeof(string), typeof(AdvancedWebView), null);

        /// <summary>Bindable property for <see cref="CurrentUrl"/>.</summary>
        public static readonly BindableProperty CurrentUrlProperty = CurrentUrlPropertyKey.BindableProperty;

        /// <summary>
        /// URL of the current page, or <see langword="null"/> before the first load.
        /// Also updates on single-page-app navigation (<c>history.pushState</c>), which does not raise <see cref="WebView.Navigated"/>.
        /// </summary>
        public string? CurrentUrl => (string?)GetValue(CurrentUrlProperty);

        // Called by the platform handlers on the main thread.

        internal void SetProgress(double progress) => SetValue(ProgressPropertyKey, Math.Clamp(progress, 0d, 1d));

        internal void SetIsLoading(bool isLoading) => SetValue(IsLoadingPropertyKey, isLoading);

        internal void SetTitle(string? title) => SetValue(TitlePropertyKey, string.IsNullOrEmpty(title) ? null : title);

        internal void SetCurrentUrl(string? url) => SetValue(CurrentUrlPropertyKey, string.IsNullOrEmpty(url) ? null : url);

        /// <summary>A main-frame load started (Android).</summary>
        internal void OnLoadStarted(string? url)
        {
            // The platform may report the first progress before the start event: only reset after a finished load.
            if (Progress >= 1d)
                SetProgress(0d);
            SetIsLoading(true);
            SetCurrentUrl(url);
        }

        /// <summary>A main-frame load finished or stopped (Android).</summary>
        internal void OnLoadFinished(string? url)
        {
            SetCurrentUrl(url);
            SetProgress(1d);
            SetIsLoading(false);
        }
    }
}
