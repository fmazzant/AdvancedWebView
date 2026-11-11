namespace Mafesoft.Maui.Controls
{
    /// <summary>Why the process that renders the page ended.</summary>
    public enum WebProcessTerminationReason
    {
        /// <summary>The platform does not say (iOS, Mac Catalyst), or the reason is none of the others.</summary>
        Unknown,

        /// <summary>The process crashed.</summary>
        Crashed,

        /// <summary>The system ended it, usually to free memory, or another process killed it.</summary>
        KilledByOs,
    }

    /// <summary>
    /// The process that renders the page ended. The web view has already been recovered: set <see cref="Reload"/> to
    /// choose whether the page is loaded again.
    /// </summary>
    public sealed class WebProcessTerminationEventArgs : EventArgs
    {
        internal WebProcessTerminationEventArgs(WebProcessTerminationReason reason, string? url)
        {
            Reason = reason;
            Url = url;
        }

        /// <summary>Why the process ended.</summary>
        public WebProcessTerminationReason Reason { get; }

        /// <summary>The page shown when the process ended, if known.</summary>
        public string? Url { get; }

        /// <summary>
        /// Whether <see cref="Url"/> is loaded again. Defaults to <see langword="true"/>. Set it to
        /// <see langword="false"/> to leave the web view on an empty page, for example to show your own message first.
        /// </summary>
        public bool Reload { get; set; } = true;
    }

    // Recovery from a crash of the web content process (#53).
    public partial class AdvancedWebView
    {
        /// <summary>
        /// Raised on the main thread when the process that renders the page crashes or is ended by the system (for example
        /// for low memory). Without it Android ends the whole app and iOS leaves a blank page: the control now recovers on
        /// every platform, and by default loads the page again (<see cref="WebProcessTerminationEventArgs.Reload"/>).
        /// </summary>
        /// <remarks>
        /// On Android (8.0 or later) and when the WebView2 browser process ends on Windows, the native web view cannot be
        /// used any more: the control gets a new one, so the history and the state of the page are lost. JavaScript
        /// state is lost on every platform. MAUI's own <see cref="WebView.ProcessTerminated"/> is still raised where MAUI
        /// raises it.
        /// </remarks>
        public event EventHandler<WebProcessTerminationEventArgs>? WebProcessTerminated;

        /// <summary>Raises <see cref="WebProcessTerminated"/>; returns whether the page must be loaded again.</summary>
        internal bool OnWebProcessTerminated(WebProcessTerminationReason reason, string? url)
        {
            SetIsLoading(false);
            var args = new WebProcessTerminationEventArgs(reason, url);
            WebProcessTerminated?.Invoke(this, args);
            return args.Reload;
        }
    }
}
