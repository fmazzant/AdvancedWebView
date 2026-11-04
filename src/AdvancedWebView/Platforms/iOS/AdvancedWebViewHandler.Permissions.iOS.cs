using WebKit;

namespace Mafesoft.Maui.Controls.Handlers
{
    // iOS: getUserMedia (camera, microphone). The location has no WebKit hook: WebKit asks the user itself.
    public partial class AdvancedWebViewHandler
    {
        async void RequestMediaCapturePermission(WKSecurityOrigin origin, WKMediaCaptureType type, Action<WKPermissionDecision> decisionHandler)
        {
            var decision = WKPermissionDecision.Prompt; // WebKit's default: ask the user for this site
            try
            {
                if (Advanced is { } view)
                {
                    var wanted = type switch
                    {
                        WKMediaCaptureType.Camera => WebPermissions.Camera,
                        WKMediaCaptureType.Microphone => WebPermissions.Microphone,
                        _ => WebPermissions.Camera | WebPermissions.Microphone,
                    };

                    // Grant skips WebKit's dialog; iOS still shows its own camera/microphone permission dialog the first time.
                    decision = await view.RequestPermissionAsync(ToUri(origin), wanted) switch
                    {
                        PermissionDecision.Grant => WKPermissionDecision.Grant,
                        PermissionDecision.Deny => WKPermissionDecision.Deny,
                        _ => WKPermissionDecision.Prompt,
                    };
                }
            }
            finally
            {
                MainThread.BeginInvokeOnMainThread(() => decisionHandler(decision));
            }
        }

        /// <summary>A WebKit security origin as a Uri; null for local content (no host). Also used by the bridge.</summary>
        static Uri? ToUri(WKSecurityOrigin? origin)
        {
            if (origin is null || string.IsNullOrEmpty(origin.Protocol) || string.IsNullOrEmpty(origin.Host))
                return null;

            var builder = new UriBuilder(origin.Protocol, origin.Host);
            if (origin.Port > 0)
                builder.Port = (int)origin.Port;
            return builder.Uri;
        }
    }
}
