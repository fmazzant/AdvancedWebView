namespace Mafesoft.Maui.Controls
{
    /// <summary>What happens when the page opens a new window (<c>target="_blank"</c> or <c>window.open</c>).</summary>
    public enum NewWindowAction
    {
        /// <summary>Load the URL in this web view, replacing the current page.</summary>
        OpenInSameView,

        /// <summary>Open the URL in the system browser (or the app registered for it).</summary>
        OpenInBrowser,

        /// <summary>Do nothing.</summary>
        Cancel,
    }

    /// <summary>The page asked to open a new window. Set <see cref="Action"/> to decide what happens.</summary>
    public sealed class NewWindowRequestedEventArgs : EventArgs
    {
        internal NewWindowRequestedEventArgs(string url, bool isUserGesture, NewWindowAction action)
        {
            Url = url;
            IsUserGesture = isUserGesture;
            Action = action;
        }

        /// <summary>The URL of the new window.</summary>
        public string Url { get; }

        /// <summary>
        /// Whether the request comes from a user action (a tap on a link or a button) rather than from a script running on its own.
        /// </summary>
        /// <remarks>
        /// On iOS, WebKit does not report the gesture for <c>window.open</c>. It is <see langword="true"/> for links and,
        /// when <see cref="AdvancedWebView.JavaScriptCanOpenWindowsAutomatically"/> is <see langword="false"/>, for
        /// <c>window.open</c> too, because WebKit then only allows it during a user action.
        /// Otherwise, <c>window.open</c> is reported as <see langword="false"/>.
        /// </remarks>
        public bool IsUserGesture { get; }

        /// <summary>What to do. Starts as <see cref="AdvancedWebView.NewWindowBehavior"/>.</summary>
        public NewWindowAction Action { get; set; }
    }

    // New windows: target="_blank" links and window.open.
    public partial class AdvancedWebView
    {
        /// <summary>Bindable property for <see cref="NewWindowBehavior"/>.</summary>
        public static readonly BindableProperty NewWindowBehaviorProperty =
            BindableProperty.Create(nameof(NewWindowBehavior), typeof(NewWindowAction), typeof(AdvancedWebView), NewWindowAction.OpenInSameView);

        /// <summary>
        /// What happens when the page opens a new window, unless a <see cref="NewWindowRequested"/> handler changes it.
        /// Defaults to <see cref="NewWindowAction.OpenInSameView"/>.
        /// </summary>
        /// <remarks>
        /// <c>window.open</c> without a user gesture is blocked before this point when
        /// <see cref="JavaScriptCanOpenWindowsAutomatically"/> is <see langword="false"/> (the default).
        /// </remarks>
        public NewWindowAction NewWindowBehavior
        {
            get => (NewWindowAction)GetValue(NewWindowBehaviorProperty);
            set => SetValue(NewWindowBehaviorProperty, value);
        }

        /// <summary>
        /// Raised on the main thread when the page opens a new window with <c>target="_blank"</c> or <c>window.open(url)</c>.
        /// <c>window.open()</c> without a URL is ignored.
        /// </summary>
        public event EventHandler<NewWindowRequestedEventArgs>? NewWindowRequested;

        /// <summary>
        /// Raises <see cref="NewWindowRequested"/> and applies <see cref="NewWindowAction.OpenInBrowser"/>.
        /// Returns <see langword="true"/> when the platform must load <paramref name="url"/> in this view.
        /// </summary>
        internal bool OnNewWindowRequested(string url, bool isUserGesture)
        {
            var e = new NewWindowRequestedEventArgs(url, isUserGesture, NewWindowBehavior);
            NewWindowRequested?.Invoke(this, e);

            switch (e.Action)
            {
                case NewWindowAction.OpenInSameView:
                    return true;

                case NewWindowAction.OpenInBrowser:
                    OpenOutside(url);
                    return false;

                default:
                    return false;
            }
        }

        /// <summary>A URL that a new-window request can load: absolute and not <c>about:blank</c>.</summary>
        internal static bool IsNewWindowUrl(string? url) =>
            !string.IsNullOrEmpty(url) &&
            Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
            !(uri.Scheme == "about" && uri.AbsolutePath == "blank");
    }
}
