using AView = Android.Views.View;
using AWebView = Android.Webkit.WebView;

namespace Mafesoft.Maui.Controls.Handlers
{
    // Android: context menu control (#57). A plain WebView has no menu for links and images: a long press starts the text
    // selection and its action mode. The long-click listener reads the HitTestResult, raises ContextMenuRequested and
    // consumes the long press when the menu is off or the app handles it (text fields keep it).
    public partial class AdvancedWebViewHandler
    {
        void SetUpContextMenu(AWebView webView) => webView.SetOnLongClickListener(new ContextMenuLongClickListener(this));

        static void TearDownContextMenu(AWebView webView) => webView.SetOnLongClickListener(null);

        /// <summary>Whether the long press is consumed (no selection, no menu).</summary>
        bool OnWebViewLongClick(AWebView webView)
        {
            if (Advanced is not { } view)
                return false;

            var hit = webView.GetHitTestResult();
            var type = (int)hit.Type;

            if (!view.ContextMenuEnabled)
                return ContextMenus.ConsumeAndroidLongPress(type);

            if (!view.HasContextMenuHandler)
                return false;

            var (kind, linkUrl, imageUrl, isEditable) = ContextMenus.FromAndroidHitTest(type, hit.Extra);
            return view.OnContextMenuRequested(new ContextMenuRequestedEventArgs(kind, linkUrl, imageUrl, null, null, isEditable));
        }

        sealed class ContextMenuLongClickListener : Java.Lang.Object, AView.IOnLongClickListener
        {
            readonly WeakReference<AdvancedWebViewHandler> handler;

            public ContextMenuLongClickListener(AdvancedWebViewHandler handler) =>
                this.handler = new WeakReference<AdvancedWebViewHandler>(handler);

            public bool OnLongClick(AView? v) =>
                v is AWebView webView && handler.TryGetTarget(out var owner) && owner.OnWebViewLongClick(webView);
        }
    }
}
