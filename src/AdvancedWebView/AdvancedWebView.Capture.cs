using Mafesoft.Maui.Controls.Handlers;

namespace Mafesoft.Maui.Controls
{
    /// <summary>What <see cref="AdvancedWebView.CaptureAsync(WebCaptureArea)"/> captures.</summary>
    public enum WebCaptureArea
    {
        /// <summary>The part of the page on screen, at the current scroll position.</summary>
        Viewport,

        /// <summary>
        /// The whole page, from the top, including the parts scrolled out of view.
        /// Android: call <see cref="AdvancedWebView.EnableFullPageCapture"/> first.
        /// </summary>
        FullPage,
    }

    /// <summary>A screenshot of the page: the platform image, encoded as PNG or JPEG on request.</summary>
    internal sealed class WebViewScreenshot(int width, int height, Func<Stream, ScreenshotFormat, int, Task> write) : IScreenshotResult
    {
        public int Width { get; } = width;

        public int Height { get; } = height;

        public async Task<Stream> OpenReadAsync(ScreenshotFormat format = ScreenshotFormat.Png, int quality = 100)
        {
            var stream = new MemoryStream();
            await CopyToAsync(stream, format, quality);
            stream.Position = 0;
            return stream;
        }

        public Task CopyToAsync(Stream destination, ScreenshotFormat format = ScreenshotFormat.Png, int quality = 100)
        {
            ArgumentNullException.ThrowIfNull(destination);
            return write(destination, format, Math.Clamp(quality, 0, 100));
        }
    }

    // Page screenshots.
    public partial class AdvancedWebView
    {
        internal static bool IsFullPageCaptureEnabled { get; private set; }

        /// <summary>
        /// Android: lets <see cref="CaptureAsync(WebCaptureArea)"/> capture the whole page (<see cref="WebCaptureArea.FullPage"/>).
        /// Call it once, before the first web view of the app is created, for example in <c>MauiProgram.CreateMauiApp</c>.
        /// It turns on <c>WebView.enableSlowWholeDocumentDraw</c> for the whole app, which makes drawing slower and uses
        /// more memory. Other platforms capture the whole page without it.
        /// </summary>
        public static void EnableFullPageCapture()
        {
            IsFullPageCaptureEnabled = true;
#if ANDROID
            Android.Webkit.WebView.EnableSlowWholeDocumentDraw();
#endif
        }

        /// <summary>
        /// Captures the part of the page on screen. Unlike the MAUI <c>CaptureAsync</c> extension for views, it reads
        /// the web view's own rendering, so the page is never blank.
        /// </summary>
        /// <exception cref="InvalidOperationException">The control is not attached to its handler or not laid out yet.</exception>
        public Task<IScreenshotResult> CaptureAsync() => CaptureAsync(WebCaptureArea.Viewport);

        /// <summary>
        /// Captures the page: the part on screen (<see cref="WebCaptureArea.Viewport"/>) or the whole page
        /// (<see cref="WebCaptureArea.FullPage"/>). Save it with <see cref="IScreenshotResult.OpenReadAsync"/> or
        /// <see cref="IScreenshotResult.CopyToAsync"/>, as PNG or JPEG. Width and height are in pixels.
        /// </summary>
        /// <exception cref="InvalidOperationException">The control is not attached to its handler or not laid out yet.</exception>
        /// <exception cref="NotSupportedException">
        /// <see cref="WebCaptureArea.FullPage"/> on Android without <see cref="EnableFullPageCapture"/>, or on iOS earlier than 14.
        /// </exception>
        public async Task<IScreenshotResult> CaptureAsync(WebCaptureArea area)
        {
            if (Handler is not AdvancedWebViewHandler handler)
                throw new InvalidOperationException(
                    "AdvancedWebView is not attached to its handler. Call builder.UseAdvancedWebView() in MauiProgram and wait until the control is loaded.");

            return await MainThread.InvokeOnMainThreadAsync(() => handler.CapturePageAsync(area));
        }
    }
}
