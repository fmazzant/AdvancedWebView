using Microsoft.Web.WebView2.Core;

namespace Mafesoft.Maui.Controls.Handlers
{
    // Windows: camera, microphone and location requested by the page (getUserMedia, navigator.geolocation).
    public partial class AdvancedWebViewHandler
    {
        void SetUpPermissions(CoreWebView2 target) => target.PermissionRequested += OnPermissionRequested;

        void TearDownPermissions(CoreWebView2 target) => target.PermissionRequested -= OnPermissionRequested;

        async void OnPermissionRequested(CoreWebView2 sender, CoreWebView2PermissionRequestedEventArgs args)
        {
            var wanted = args.PermissionKind switch
            {
                CoreWebView2PermissionKind.Camera => WebPermissions.Camera,
                CoreWebView2PermissionKind.Microphone => WebPermissions.Microphone,
                CoreWebView2PermissionKind.Geolocation => WebPermissions.Geolocation,
                _ => WebPermissions.None,
            };
            if (wanted == WebPermissions.None || Advanced is not { } view)
                return; // other permissions (notifications, clipboard...) keep WebView2's default

            var deferral = args.GetDeferral();
            try
            {
                // AskUser keeps WebView2's own dialog for the site. Windows privacy settings still apply to Grant.
                args.State = await view.RequestPermissionAsync(ToOrigin(args.Uri), wanted) switch
                {
                    PermissionDecision.Grant => CoreWebView2PermissionState.Allow,
                    PermissionDecision.Deny => CoreWebView2PermissionState.Deny,
                    _ => CoreWebView2PermissionState.Default,
                };
            }
            catch (Exception ex)
            {
                Log($"Permission request failed: {ex.Message}");
            }
            finally
            {
                deferral.Complete();
            }
        }
    }
}
