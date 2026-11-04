using AndroidX.Activity;
using Microsoft.Maui.Platform;

namespace Mafesoft.Maui.Controls.Handlers
{
    // Android: back button and FullscreenChanged while a video (or element) is in fullscreen.
    public partial class AdvancedWebViewHandler
    {
        ExitFullscreenOnBack? exitFullscreenOnBack;

        void OnFullscreenEntered()
        {
            // Registered last, so it runs before MAUI's own back handling (navigation pop, app close).
            if (exitFullscreenOnBack is null && PlatformView?.Context?.GetActivity() is ComponentActivity activity)
            {
                exitFullscreenOnBack = new ExitFullscreenOnBack(this);
                activity.OnBackPressedDispatcher.AddCallback(exitFullscreenOnBack);
            }

            Advanced?.SetFullscreen(true);
        }

        void OnFullscreenExited()
        {
            exitFullscreenOnBack?.Remove();
            exitFullscreenOnBack = null;
            Advanced?.SetFullscreen(false);
        }

        /// <summary>Leaves fullscreen when the view goes away while a video is fullscreen (otherwise it stays on top of the app).</summary>
        void ExitFullscreen()
        {
            if (exitFullscreenOnBack is not null)
                webChromeClient?.OnHideCustomView();
        }

        sealed class ExitFullscreenOnBack : OnBackPressedCallback
        {
            readonly WeakReference<AdvancedWebViewHandler> handler;

            public ExitFullscreenOnBack(AdvancedWebViewHandler handler) : base(enabled: true) =>
                this.handler = new WeakReference<AdvancedWebViewHandler>(handler);

            // Same as the page leaving fullscreen: MAUI restores the activity and notifies the page.
            public override void HandleOnBackPressed()
            {
                if (handler.TryGetTarget(out var owner))
                    owner.ExitFullscreen();
            }
        }
    }
}
