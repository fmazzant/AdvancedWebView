using Android.OS;
using Android.Views;
using Android.Webkit;
using Microsoft.Maui.Platform;
using AView = Android.Views.View;
using AWebView = Android.Webkit.WebView;

namespace Mafesoft.Maui.Controls.Handlers
{
    // Android (8.0+): the renderer process crashed or was killed (#53). The WebView cannot be used any more, and when
    // OnRenderProcessGone returns false Android ends the whole app. The control gets a new handler with a new WebView,
    // in the same place of the layout.
    public partial class AdvancedWebViewHandler
    {
        [System.Runtime.Versioning.SupportedOSPlatform("android26.0")]
        void OnRenderProcessGone(AWebView? deadView, RenderProcessGoneDetail? detail)
        {
            if (Advanced is not { } view || MauiContext is not { } context || !ReferenceEquals(deadView, PlatformView))
                return;

            var reason = detail is null
                ? WebProcessTerminationReason.Unknown
                : detail.DidCrash() ? WebProcessTerminationReason.Crashed : WebProcessTerminationReason.KilledByOs;
            var url = view.CurrentUrl;

            // Posted: the dead WebView is removed and destroyed outside its own callback.
            new Handler(Looper.MainLooper!).Post(() => ReplaceDeadWebView(view, context, reason, url));
        }

        static void ReplaceDeadWebView(AdvancedWebView view, IMauiContext context, WebProcessTerminationReason reason, string? url)
        {
            if (view.Handler is not AdvancedWebViewHandler old)
                return;

            AView oldView = old.ContainerView ?? old.PlatformView;
            var parent = oldView.Parent as ViewGroup;
            var index = parent?.IndexOfChild(oldView) ?? -1;
            var layout = oldView.LayoutParameters;

            // MAUI's DisconnectHandler removes the dead WebView from its parent and destroys it.
            view.Handler = null;
            var replacement = view.ToPlatform(context);
            if (parent is not null && replacement.Parent is null)
            {
                index = index < 0 ? parent.ChildCount : Math.Min(index, parent.ChildCount);
                if (layout is not null)
                    parent.AddView(replacement, index, layout);
                else
                    parent.AddView(replacement, index);
            }

            var reload = view.OnWebProcessTerminated(reason, url);
            if (view.Handler is not AdvancedWebViewHandler fresh)
                return;

            if (reload && !string.IsNullOrEmpty(url))
                fresh.LoadRequest(new WebRequest(url, new Dictionary<string, string>()));
            else
                fresh.PlatformView.LoadUrl("about:blank");
        }
    }
}
