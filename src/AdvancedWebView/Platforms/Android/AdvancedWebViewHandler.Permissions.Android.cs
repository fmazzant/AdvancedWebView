using Android.Webkit;

namespace Mafesoft.Maui.Controls.Handlers
{
    // Android: getUserMedia (camera, microphone) and navigator.geolocation. MAUI does not handle them, so the
    // WebView's default denies them silently.
    public partial class AdvancedWebViewHandler
    {
        /// <summary>Returns false for requests AdvancedWebView does not handle (for example protected media), left to the default.</summary>
        bool OnPermissionRequest(PermissionRequest request)
        {
            var resources = request.GetResources() ?? [];
            var wanted = WebPermissions.None;
            if (resources.Contains(PermissionRequest.ResourceVideoCapture))
                wanted |= WebPermissions.Camera;
            if (resources.Contains(PermissionRequest.ResourceAudioCapture))
                wanted |= WebPermissions.Microphone;

            if (wanted == WebPermissions.None || Advanced is not { } view)
                return false;

            Uri.TryCreate(request.Origin?.ToString(), UriKind.Absolute, out var origin);
            AnswerPermissionAsync(view, origin, wanted, granted =>
            {
                var allowed = new List<string>();
                if (granted.HasFlag(WebPermissions.Camera))
                    allowed.Add(PermissionRequest.ResourceVideoCapture);
                if (granted.HasFlag(WebPermissions.Microphone))
                    allowed.Add(PermissionRequest.ResourceAudioCapture);

                if (allowed.Count == 0)
                    request.Deny();
                else
                    request.Grant([.. allowed]);
            });
            return true;
        }

        void OnGeolocationPermissionsShowPrompt(string? origin, GeolocationPermissions.ICallback? callback)
        {
            if (callback is null || Advanced is not { } view)
                return;

            Uri.TryCreate(origin, UriKind.Absolute, out var uri);
            AnswerPermissionAsync(view, uri, WebPermissions.Geolocation,
                granted => callback.Invoke(origin, granted.HasFlag(WebPermissions.Geolocation), retain: false));
        }

        /// <summary>
        /// Raises PermissionRequested, then asks the OS permissions when the decision allows it.
        /// <paramref name="answer"/> always runs once, on the main thread, with what the page may use.
        /// </summary>
        static async void AnswerPermissionAsync(AdvancedWebView view, Uri? origin, WebPermissions wanted, Action<WebPermissions> answer)
        {
            var granted = WebPermissions.None;
            try
            {
                // AskUser and Grant behave the same: Android has no per-site dialog.
                if (await view.RequestPermissionAsync(origin, wanted) != PermissionDecision.Deny)
                    granted = await view.RequestOsPermissionsAsync(wanted);
            }
            finally
            {
                MainThread.BeginInvokeOnMainThread(() => answer(granted));
            }
        }
    }
}
