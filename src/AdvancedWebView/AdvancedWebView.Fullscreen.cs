namespace Mafesoft.Maui.Controls
{
    /// <summary>The page entered or left fullscreen.</summary>
    public sealed class FullscreenChangedEventArgs : EventArgs
    {
        internal FullscreenChangedEventArgs(bool isFullscreen) => IsFullscreen = isFullscreen;

        /// <summary>Whether the page is now in fullscreen.</summary>
        public bool IsFullscreen { get; }
    }

    // Fullscreen video and the HTML fullscreen API (element.requestFullscreen).
    public partial class AdvancedWebView
    {
        /// <summary>Bindable property for <see cref="FullscreenEnabled"/>.</summary>
        public static readonly BindableProperty FullscreenEnabledProperty =
            BindableProperty.Create(nameof(FullscreenEnabled), typeof(bool), typeof(AdvancedWebView), true);

        /// <summary>
        /// Whether the page can go fullscreen, for example with the fullscreen button of a video player.
        /// Defaults to <see langword="true"/>. On iOS the value is read when the native view is created.
        /// </summary>
        public bool FullscreenEnabled
        {
            get => (bool)GetValue(FullscreenEnabledProperty);
            set => SetValue(FullscreenEnabledProperty, value);
        }

        static readonly BindablePropertyKey IsFullscreenPropertyKey =
            BindableProperty.CreateReadOnly(nameof(IsFullscreen), typeof(bool), typeof(AdvancedWebView), false);

        /// <summary>Bindable property for <see cref="IsFullscreen"/>.</summary>
        public static readonly BindableProperty IsFullscreenProperty = IsFullscreenPropertyKey.BindableProperty;

        /// <summary>Whether the page is in fullscreen.</summary>
        public bool IsFullscreen => (bool)GetValue(IsFullscreenProperty);

        /// <summary>
        /// Raised on the main thread when the page enters or leaves fullscreen. Use it, for example, to hide the app's
        /// navigation bar: <c>Shell.SetNavBarIsVisible(page, !e.IsFullscreen)</c>.
        /// </summary>
        public event EventHandler<FullscreenChangedEventArgs>? FullscreenChanged;

        /// <summary>Called by the platform handlers on the main thread.</summary>
        internal void SetFullscreen(bool isFullscreen)
        {
            if (IsFullscreen == isFullscreen)
                return;

            SetValue(IsFullscreenPropertyKey, isFullscreen);
            FullscreenChanged?.Invoke(this, new FullscreenChangedEventArgs(isFullscreen));
        }
    }
}
