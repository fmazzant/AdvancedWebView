using AWebView = Android.Webkit.WebView;

namespace Mafesoft.Maui.Controls.Handlers
{
    // Android: find in page (#27) with WebView.FindAllAsync, FindNext and ClearMatches. The FindListener reports the
    // active match and the count; while isDoneCounting is false the count is still growing, so only the final result is
    // reported. The search is always case-insensitive.
    public partial class AdvancedWebViewHandler
    {
        FindListener? findListener;
        TaskCompletionSource<int>? pendingFind;

        internal partial Task<int> FindCoreAsync(string text, bool caseSensitive)
        {
            if (PlatformView is not { } webView)
                return Task.FromResult(0);

            if (caseSensitive)
                Log("FindAsync: Android searches case-insensitively, caseSensitive is ignored.");

            webView.SetFindListener(findListener ??= new FindListener(this));
            pendingFind?.TrySetResult(0);
            var pending = pendingFind = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            webView.FindAllAsync(text);
            return pending.Task;
        }

        internal partial void FindNextCore(bool forward) => PlatformView?.FindNext(forward);

        internal partial void ClearFindCore()
        {
            PlatformView?.ClearMatches();
            pendingFind?.TrySetResult(0);
            pendingFind = null;
            Advanced?.OnFindResultsUpdated(-1, 0);
        }

        void OnFindResultReceived(int activeMatchOrdinal, int numberOfMatches, bool isDoneCounting)
        {
            if (!isDoneCounting)
                return;

            pendingFind?.TrySetResult(numberOfMatches);
            pendingFind = null;
            Advanced?.OnFindResultsUpdated(numberOfMatches > 0 ? activeMatchOrdinal : -1, numberOfMatches);
        }

        sealed class FindListener(AdvancedWebViewHandler owner) : Java.Lang.Object, AWebView.IFindListener
        {
            readonly WeakReference<AdvancedWebViewHandler> handler = new(owner);

            public void OnFindResultReceived(int activeMatchOrdinal, int numberOfMatches, bool isDoneCounting)
            {
                if (handler.TryGetTarget(out var target))
                    target.OnFindResultReceived(activeMatchOrdinal, numberOfMatches, isDoneCounting);
            }
        }
    }
}
