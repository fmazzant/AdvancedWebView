using Microsoft.Web.WebView2.Core;

namespace Mafesoft.Maui.Controls.Handlers
{
    // Windows: find in page (#27) with the WebView2 Find API (CoreWebView2.Find), without WebView2's own find bar.
    // StartAsync returns before the matches are counted: the count comes with MatchCountChanged. ActiveMatchIndex is
    // 1-based. A WebView2 Runtime without the Find API throws: nothing is found and a warning is logged.
    public partial class AdvancedWebViewHandler
    {
        // How long FindAsync waits for the first MatchCountChanged before returning the count it has.
        static readonly TimeSpan FindCountTimeout = TimeSpan.FromSeconds(2);

        CoreWebView2Find? find;
        TaskCompletionSource? findCounted;

        internal partial async Task<int> FindCoreAsync(string text, bool caseSensitive)
        {
            if (core is not { } target)
                return 0;

            try
            {
                var current = AttachFind(target);
                var options = target.Environment.CreateFindOptions();
                options.FindTerm = text;
                options.IsCaseSensitive = caseSensitive;
                options.ShouldHighlightAllMatches = true;
                options.SuppressDefaultFindDialog = true;

                var counted = findCounted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                await current.StartAsync(options);
                await Task.WhenAny(counted.Task, Task.Delay(FindCountTimeout));
                return Math.Max(0, current.MatchCount);
            }
            catch (Exception ex)
            {
                Advanced?.LogWarning($"FindAsync: the WebView2 Runtime does not support find in page ({ex.Message}).");
                return 0;
            }
        }

        internal partial void FindNextCore(bool forward)
        {
            try
            {
                if (forward)
                    find?.FindNext();
                else
                    find?.FindPrevious();
            }
            catch (Exception ex)
            {
                Log($"Find in page failed: {ex.Message}");
            }
        }

        internal partial void ClearFindCore()
        {
            try
            {
                find?.Stop();
            }
            catch (Exception ex)
            {
                Log($"Find in page failed: {ex.Message}");
            }
            Advanced?.OnFindResultsUpdated(-1, 0);
        }

        CoreWebView2Find AttachFind(CoreWebView2 target)
        {
            if (find is not null)
                return find;

            var created = target.Find;
            created.MatchCountChanged += OnFindMatchesChanged;
            created.ActiveMatchIndexChanged += OnFindMatchesChanged;
            return find = created;
        }

        void TearDownFind()
        {
            if (find is null)
                return;

            find.MatchCountChanged -= OnFindMatchesChanged;
            find.ActiveMatchIndexChanged -= OnFindMatchesChanged;
            find = null;
        }

        void OnFindMatchesChanged(CoreWebView2Find sender, object args)
        {
            var count = Math.Max(0, sender.MatchCount);
            findCounted?.TrySetResult();
            Advanced?.OnFindResultsUpdated(FindInPage.FromOneBased(sender.ActiveMatchIndex, count), count);
        }
    }
}
