using Microsoft.Web.WebView2.Core;

namespace Mafesoft.Maui.Controls.Handlers
{
    // Windows: context menu control (#57). ContextMenuEnabled is WebView2's AreDefaultContextMenusEnabled; with the menu
    // on, CoreWebView2.ContextMenuRequested raises ContextMenuRequested, and Handled stops WebView2's menu.
    public partial class AdvancedWebViewHandler
    {
        void SetUpContextMenu(CoreWebView2 target) => target.ContextMenuRequested += OnContextMenuRequested;

        void TearDownContextMenu(CoreWebView2 target) => target.ContextMenuRequested -= OnContextMenuRequested;

        partial void UpdateContextMenuCore()
        {
            if (core is { } target && Advanced is { } view)
                target.Settings.AreDefaultContextMenusEnabled = view.ContextMenuEnabled;
        }

        void OnContextMenuRequested(CoreWebView2 sender, CoreWebView2ContextMenuRequestedEventArgs args)
        {
            if (Advanced is not { HasContextMenuHandler: true } view)
                return;

            var target = args.ContextMenuTarget;
            var linkUrl = target.HasLinkUri ? target.LinkUri : null;
            var imageUrl = target.Kind == CoreWebView2ContextMenuTargetKind.Image && target.HasSourceUri ? target.SourceUri : null;
            var selectedText = target.HasSelection ? target.SelectionText : null;
            var isMedia = target.Kind is CoreWebView2ContextMenuTargetKind.Audio or CoreWebView2ContextMenuTargetKind.Video;
            var kind = ContextMenus.GetKind(selectedText, linkUrl, imageUrl, isMedia, target.IsEditable);

            // WebView2 reports raw pixels: device-independent units, as in the rest of MAUI.
            var scale = PlatformView?.XamlRoot?.RasterizationScale ?? 1d;
            if (scale <= 0)
                scale = 1d;
            var position = new Point(args.Location.X / scale, args.Location.Y / scale);

            if (view.OnContextMenuRequested(new ContextMenuRequestedEventArgs(kind, linkUrl, imageUrl, selectedText, position, target.IsEditable)))
                args.Handled = true;
        }
    }
}
