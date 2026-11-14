namespace Mafesoft.Maui.Controls
{
    /// <summary>What the user long-pressed or right-clicked in the page.</summary>
    public enum ContextMenuKind
    {
        /// <summary>The page itself: no link, image or selected text.</summary>
        Page,

        /// <summary>A link (<c>LinkUrl</c>), possibly around an image (<c>ImageUrl</c>).</summary>
        Link,

        /// <summary>An image (<c>ImageUrl</c>).</summary>
        Image,

        /// <summary>Selected text (<c>SelectedText</c>) or a text field.</summary>
        Text,

        /// <summary>Audio or video (Windows only).</summary>
        Media,
    }

    /// <summary>Context menu targets (#57). Platform-independent.</summary>
    internal static class ContextMenus
    {
        // android.webkit.WebView.HitTestResult types.
        const int AndroidUnknown = 0;
        const int AndroidAnchor = 1;
        const int AndroidPhone = 2;
        const int AndroidGeo = 3;
        const int AndroidEmail = 4;
        const int AndroidImage = 5;
        const int AndroidImageAnchor = 6;
        const int AndroidSrcAnchor = 7;
        const int AndroidSrcImageAnchor = 8;
        const int AndroidEditText = 9;

        /// <summary>
        /// The kind for what the platform reports: selected text first (the user is working with it), then a link,
        /// an image, media, and otherwise the page.
        /// </summary>
        public static ContextMenuKind GetKind(string? selectedText, string? linkUrl, string? imageUrl, bool isMedia = false, bool isEditable = false)
        {
            if (!string.IsNullOrEmpty(selectedText) || isEditable)
                return ContextMenuKind.Text;
            if (!string.IsNullOrEmpty(linkUrl))
                return ContextMenuKind.Link;
            if (!string.IsNullOrEmpty(imageUrl))
                return ContextMenuKind.Image;
            return isMedia ? ContextMenuKind.Media : ContextMenuKind.Page;
        }

        /// <summary>
        /// Kind, link and image of an Android <c>WebView.HitTestResult</c> (type and extra). Phone numbers, e-mail
        /// addresses and places are reported as <c>tel:</c>, <c>mailto:</c> and <c>geo:</c> links. For an image inside a
        /// link, Android reports only the image.
        /// </summary>
        public static (ContextMenuKind Kind, string? LinkUrl, string? ImageUrl, bool IsEditable) FromAndroidHitTest(int type, string? extra)
        {
            var value = string.IsNullOrEmpty(extra) ? null : extra;
            return type switch
            {
                AndroidAnchor or AndroidSrcAnchor when value is not null => (ContextMenuKind.Link, value, null, false),
                AndroidPhone when value is not null => (ContextMenuKind.Link, "tel:" + value, null, false),
                AndroidEmail when value is not null => (ContextMenuKind.Link, "mailto:" + value, null, false),
                AndroidGeo when value is not null => (ContextMenuKind.Link, "geo:0,0?q=" + Uri.EscapeDataString(value), null, false),
                AndroidImage or AndroidImageAnchor or AndroidSrcImageAnchor when value is not null => (ContextMenuKind.Image, null, value, false),
                AndroidEditText => (ContextMenuKind.Text, null, null, true),
                _ => (ContextMenuKind.Page, null, null, false),
            };
        }

        /// <summary>
        /// Android, with the menu turned off: whether the long press is consumed (no text selection, no action mode).
        /// Text fields keep it, so that paste and the cursor still work.
        /// </summary>
        public static bool ConsumeAndroidLongPress(int type) => type != AndroidEditText;
    }
}
