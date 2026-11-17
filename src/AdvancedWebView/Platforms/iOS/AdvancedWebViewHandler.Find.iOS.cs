using Foundation;

namespace Mafesoft.Maui.Controls.Handlers
{
    // iOS / Mac Catalyst: find in page (#27). WKWebView's own find (findString:withConfiguration:) moves the selection
    // to one match but reports no count, and UIFindInteraction is a system find bar: the search runs in the page instead
    // (FindInPage.Engine), which counts, highlights and scrolls to the active match.
    public partial class AdvancedWebViewHandler
    {
        internal partial async Task<int> FindCoreAsync(string text, bool caseSensitive)
        {
            var (index, count) = await RunFindScriptAsync(FindInPage.StartScript(text, caseSensitive));
            Advanced?.OnFindResultsUpdated(index, count);
            return count;
        }

        internal async partial void FindNextCore(bool forward)
        {
            var (index, count) = await RunFindScriptAsync(FindInPage.NextScript(forward));
            if (count > 0)
                Advanced?.OnFindResultsUpdated(index, count);
        }

        internal async partial void ClearFindCore()
        {
            await RunFindScriptAsync(FindInPage.ClearScript());
            Advanced?.OnFindResultsUpdated(-1, 0);
        }

        async Task<(int ActiveIndex, int Count)> RunFindScriptAsync(string script)
        {
            if (PlatformView is not { } webView)
                return (-1, 0);
            try
            {
                var result = await webView.EvaluateJavaScriptAsync(script);
                return FindInPage.ParseResult((result as NSString)?.ToString());
            }
            catch (Exception ex)
            {
                Log($"Find in page failed: {ex.Message}");
                return (-1, 0);
            }
        }
    }
}
