using System.Globalization;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace Mafesoft.Maui.Controls.Handlers
{
    // Windows: the visible part with CapturePreviewAsync; the whole page with the DevTools protocol
    // (Page.captureScreenshot beyond the viewport). Both give a PNG; JPEG is encoded from it on request.
    public partial class AdvancedWebViewHandler
    {
        internal partial async Task<WebViewScreenshot> CapturePageAsync(WebCaptureArea area)
        {
            if (core is not { } target)
                throw new InvalidOperationException("The web view is not ready: capture it after it is shown.");

            var png = area == WebCaptureArea.FullPage
                ? await CaptureFullPageAsync(target)
                : await CaptureViewportAsync(target);

            var decoder = await BitmapDecoder.CreateAsync(png.AsBuffer().AsStream().AsRandomAccessStream());
            return new WebViewScreenshot((int)decoder.PixelWidth, (int)decoder.PixelHeight,
                (stream, format, quality) => format == ScreenshotFormat.Jpeg
                    ? EncodeJpegAsync(png, quality, stream)
                    : stream.WriteAsync(png).AsTask());
        }

        static async Task<byte[]> CaptureViewportAsync(CoreWebView2 target)
        {
            using var stream = new InMemoryRandomAccessStream();
            await target.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, stream);
            var bytes = new byte[stream.Size];
            stream.Seek(0);
            await stream.ReadAsync(bytes.AsBuffer(), (uint)bytes.Length, InputStreamOptions.None);
            return bytes;
        }

        static async Task<byte[]> CaptureFullPageAsync(CoreWebView2 target)
        {
            // The page size in CSS pixels; the screenshot comes back in device pixels.
            using var metrics = JsonDocument.Parse(await target.CallDevToolsProtocolMethodAsync("Page.getLayoutMetrics", "{}"));
            var size = metrics.RootElement.GetProperty("cssContentSize");
            var width = Math.Ceiling(size.GetProperty("width").GetDouble());
            var height = Math.Ceiling(size.GetProperty("height").GetDouble());

            var parameters = string.Create(CultureInfo.InvariantCulture,
                $$$"""{"format":"png","captureBeyondViewport":true,"clip":{"x":0,"y":0,"width":{{{width}}},"height":{{{height}}},"scale":1}}""");
            using var result = JsonDocument.Parse(await target.CallDevToolsProtocolMethodAsync("Page.captureScreenshot", parameters));
            return Convert.FromBase64String(result.RootElement.GetProperty("data").GetString()!);
        }

        static async Task EncodeJpegAsync(byte[] png, int quality, Stream destination)
        {
            var decoder = await BitmapDecoder.CreateAsync(png.AsBuffer().AsStream().AsRandomAccessStream());
            var pixels = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore);

            using var output = new InMemoryRandomAccessStream();
            var options = new BitmapPropertySet { ["ImageQuality"] = new BitmapTypedValue(quality / 100f, Windows.Foundation.PropertyType.Single) };
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, output, options);
            encoder.SetSoftwareBitmap(pixels);
            await encoder.FlushAsync();

            output.Seek(0);
            await output.AsStreamForRead().CopyToAsync(destination);
        }
    }
}
