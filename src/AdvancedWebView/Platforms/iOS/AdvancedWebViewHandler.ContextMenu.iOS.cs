using WebKit;

namespace Mafesoft.Maui.Controls.Handlers
{
    // iOS and Mac Catalyst: context menu control (#57). WKUIDelegate.contextMenuConfigurationForElement is asked for the
    // long-press (or right-click) menu of links and images: answering nil shows no menu. The text selection callout and
    // the page menu of Mac Catalyst are stopped by the display script (-webkit-touch-callout, contextmenu event).
    public partial class AdvancedWebViewHandler
    {
        /// <summary>Whether the menu for <paramref name="elementInfo"/> must not be shown (menu off, or handled by the app).</summary>
        bool SuppressContextMenu(WKContextMenuElementInfo elementInfo)
        {
            if (Advanced is not { } view)
                return false;
            if (!view.ContextMenuEnabled)
                return true;
            if (!view.HasContextMenuHandler)
                return false;

            var linkUrl = elementInfo.LinkUrl?.AbsoluteString;
            var kind = ContextMenus.GetKind(null, linkUrl, null);
            return view.OnContextMenuRequested(new ContextMenuRequestedEventArgs(kind, linkUrl, null, null, null, false));
        }
    }
}
