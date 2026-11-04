namespace Mafesoft.Maui.Controls
{
    /// <summary>Settings of the JavaScript bridge of an <see cref="AdvancedWebView"/>.</summary>
    public sealed class BridgeOptions
    {
        /// <summary>
        /// How long a page call to <c>AdvancedWebView.invoke</c> may run before its Promise is rejected
        /// with code <c>Timeout</c>. Defaults to 30 seconds. Use <see cref="Timeout.InfiniteTimeSpan"/> to disable.
        /// </summary>
        public TimeSpan InvokeTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Whether rejected Promises include the full exception text (<c>error.details</c>).
        /// Defaults to <see langword="false"/>: only <see cref="Exception.Message"/> reaches the page.
        /// Enable it only while debugging.
        /// </summary>
        public bool IncludeExceptionDetails { get; set; }
    }
}
