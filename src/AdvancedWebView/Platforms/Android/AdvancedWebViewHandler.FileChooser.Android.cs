using Android.Webkit;
using AUri = Android.Net.Uri;

namespace Mafesoft.Maui.Controls.Handlers
{
    // Android: <input type="file">. MAUI's chrome client already opens a picker for a single file; it cannot return
    // several files (it reads only the first URI of the result) and ignores the capture attribute.
    // AdvancedWebView raises FileChooserRequested and covers those two cases with MAUI FilePicker and MediaPicker.
    public partial class AdvancedWebViewHandler
    {
        /// <summary>Returns <see langword="false"/> when MAUI's own picker should handle the request.</summary>
        bool ShowFileChooser(IValueCallback callback, WebChromeClient.FileChooserParams parameters)
        {
            if (Advanced is not { } view)
                return false;

            var accept = AdvancedWebView.SplitAcceptTypes(parameters.GetAcceptTypes());
            var multiple = parameters.Mode == ChromeFileChooserMode.OpenMultiple;
            var capture = parameters.IsCaptureEnabled;

            // The callback must be completed exactly once, also on cancel: otherwise the page cannot open a picker again.
            void Complete(string[]? files) =>
                MainThread.BeginInvokeOnMainThread(() => callback.OnReceiveValue(ToUris(files)));

            var e = new FileChooserRequestedEventArgs(accept, multiple, capture, Complete);
            view.OnFileChooserRequested(e);
            if (e.Handled)
                return true;

            if (!multiple && !capture)
                return false;

            PickFiles(e, accept, multiple, capture);
            return true;
        }

        async void PickFiles(FileChooserRequestedEventArgs request, string[] accept, bool multiple, bool capture)
        {
            string[]? files = null;
            try
            {
                if (capture && MediaPicker.Default.IsCaptureSupported)
                {
                    // capture="..." with accept="video/*" records a video; anything else takes a photo.
                    var video = accept.Any(t => t.StartsWith("video/", StringComparison.Ordinal)) &&
                                !accept.Any(t => t.StartsWith("image/", StringComparison.Ordinal));
                    var media = video ? await MediaPicker.Default.CaptureVideoAsync() : await MediaPicker.Default.CapturePhotoAsync();
                    files = media is null ? null : [media.FullPath];
                }
                else
                {
                    var options = new PickOptions { FileTypes = ToFileTypes(accept) };
                    if (multiple)
                        files = (await FilePicker.Default.PickMultipleAsync(options))?.OfType<FileResult>().Select(f => f.FullPath).ToArray();
                    else if (await FilePicker.Default.PickAsync(options) is { } file)
                        files = [file.FullPath];
                }
            }
            catch (Exception ex)
            {
                Log($"File picker failed: {ex.Message}. For camera capture, the app needs the CAMERA permission.");
            }
            finally
            {
                request.SetResult(files);
            }
        }

        /// <summary>MIME types for FilePicker: <c>image/*</c> as is, <c>.pdf</c> through Android's MIME map. Null means any file.</summary>
        static FilePickerFileType? ToFileTypes(string[] accept)
        {
            var mimeTypes = accept
                .Select(a => a.StartsWith('.') ? MimeTypeMap.Singleton?.GetMimeTypeFromExtension(a[1..]) : a.Contains('/') ? a : null)
                .OfType<string>()
                .Distinct()
                .ToArray();

            // An extension Android does not know would filter out everything: show all files instead.
            if (mimeTypes.Length == 0 || mimeTypes.Length < accept.Length)
                return null;

            return new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>> { [DevicePlatform.Android] = mimeTypes });
        }

        static AUri[]? ToUris(string[]? files) =>
            files?.Select(f => f.StartsWith("content://", StringComparison.OrdinalIgnoreCase) || f.StartsWith("file://", StringComparison.OrdinalIgnoreCase)
                    ? AUri.Parse(f)!
                    : AUri.FromFile(new Java.IO.File(f))!)
                .ToArray();
    }
}
