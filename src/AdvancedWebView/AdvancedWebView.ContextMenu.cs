namespace Mafesoft.Maui.Controls
{
    /// <summary>
    /// The user long-pressed (or right-clicked) the page. Set <see cref="Handled"/> to <see langword="true"/> to stop the
    /// platform menu, for example to show the app's own actions.
    /// </summary>
    public sealed class ContextMenuRequestedEventArgs : EventArgs
    {
        internal ContextMenuRequestedEventArgs(ContextMenuKind kind, string? linkUrl, string? imageUrl, string? selectedText, Point? position, bool isEditable)
        {
            Kind = kind;
            LinkUrl = linkUrl;
            ImageUrl = imageUrl;
            SelectedText = selectedText;
            Position = position;
            IsEditable = isEditable;
        }

        /// <summary>What was pressed: a link, an image, text or the page.</summary>
        public ContextMenuKind Kind { get; }

        /// <summary>The URL of the link, where the platform reports it (Android, iOS, Mac Catalyst, Windows).</summary>
        public string? LinkUrl { get; }

        /// <summary>The URL of the image, where the platform reports it (Android and Windows; WebKit does not).</summary>
        public string? ImageUrl { get; }

        /// <summary>The selected text (Windows only).</summary>
        public string? SelectedText { get; }

        /// <summary>
        /// Where the menu was asked for, in device-independent units relative to the web view (Windows only; Android and
        /// WebKit do not report it).
        /// </summary>
        public Point? Position { get; }

        /// <summary>Whether the target is a text field.</summary>
        public bool IsEditable { get; }

        /// <summary>Set to <see langword="true"/> to stop the platform menu.</summary>
        public bool Handled { get; set; }
    }

    // Context menu (long press / right click) control (#57).
    public partial class AdvancedWebView
    {
        /// <summary>Bindable property for <see cref="ContextMenuEnabled"/>.</summary>
        public static readonly BindableProperty ContextMenuEnabledProperty =
            BindableProperty.Create(nameof(ContextMenuEnabled), typeof(bool), typeof(AdvancedWebView), true);

        /// <summary>
        /// Whether the page shows the platform's context menu: the long-press menu of links and images and, on Android,
        /// the text selection that a long press starts (text fields keep it). Defaults to <see langword="true"/>.
        /// When off, <see cref="ContextMenuRequested"/> is not raised either.
        /// </summary>
        public bool ContextMenuEnabled
        {
            get => (bool)GetValue(ContextMenuEnabledProperty);
            set => SetValue(ContextMenuEnabledProperty, value);
        }

        /// <summary>
        /// Raised on the main thread before the platform shows the context menu, while <see cref="ContextMenuEnabled"/>
        /// is on. Set <see cref="ContextMenuRequestedEventArgs.Handled"/> to show the app's own menu instead.
        /// </summary>
        /// <remarks>
        /// Android raises it on a long press, iOS and Mac Catalyst on a long press (or right click) of a link or an image,
        /// Windows on a right click or a long touch.
        /// </remarks>
        public event EventHandler<ContextMenuRequestedEventArgs>? ContextMenuRequested;

        /// <summary>Whether a handler is attached.</summary>
        internal bool HasContextMenuHandler => ContextMenuRequested is not null;

        /// <summary>Raises <see cref="ContextMenuRequested"/>; returns whether the platform menu must be stopped.</summary>
        internal bool OnContextMenuRequested(ContextMenuRequestedEventArgs e)
        {
            try
            {
                ContextMenuRequested?.Invoke(this, e);
                return e.Handled;
            }
            catch (Exception ex)
            {
                LogWarning($"ContextMenuRequested handler failed, the platform menu is shown: {ex.Message}");
                return false;
            }
        }
    }
}
