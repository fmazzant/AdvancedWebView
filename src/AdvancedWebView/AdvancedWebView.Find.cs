using Mafesoft.Maui.Controls.Handlers;

namespace Mafesoft.Maui.Controls
{
    /// <summary>The state of the search started with <see cref="AdvancedWebView.FindAsync"/>.</summary>
    public sealed class FindResultsUpdatedEventArgs : EventArgs
    {
        internal FindResultsUpdatedEventArgs(int activeIndex, int count)
        {
            ActiveIndex = activeIndex;
            Count = count;
        }

        /// <summary>The active match, from 0; -1 when there is none (no matches, or the search was cleared).</summary>
        public int ActiveIndex { get; }

        /// <summary>How many matches the page has.</summary>
        public int Count { get; }
    }

    // Find in page (#27).
    public partial class AdvancedWebView
    {
        /// <summary>
        /// Raised on the main thread when the matches or the active match change: after <see cref="FindAsync"/>,
        /// <see cref="FindNext"/>, <see cref="FindPrevious"/> and <see cref="ClearFind"/>.
        /// </summary>
        public event EventHandler<FindResultsUpdatedEventArgs>? FindResultsUpdated;

        /// <summary>
        /// Searches <paramref name="text"/> in the page, highlights the matches and scrolls to the first one, which
        /// becomes the active match. Returns how many matches the page has. An empty text clears the search.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item>Android: the search is always case-insensitive, <paramref name="caseSensitive"/> is ignored.</item>
        /// <item>iOS and Mac Catalyst: the search covers the text of the main frame, not inside iframes, and a match
        /// cannot span two elements (for example half in bold). All the matches are highlighted from iOS 17.2; earlier,
        /// only the active match is shown, as the selection.</item>
        /// <item>Windows: needs a WebView2 Runtime with the Find API; with an older one nothing is found and a warning is logged.</item>
        /// </list>
        /// </remarks>
        /// <exception cref="InvalidOperationException">The control is not attached to its handler.</exception>
        public async Task<int> FindAsync(string text, bool caseSensitive = false)
        {
            ArgumentNullException.ThrowIfNull(text);
            var handler = FindHandler();

            if (text.Length == 0)
            {
                await MainThread.InvokeOnMainThreadAsync(handler.ClearFindCore);
                return 0;
            }

            return await MainThread.InvokeOnMainThreadAsync(() => handler.FindCoreAsync(text, caseSensitive));
        }

        /// <summary>Moves to the next match, wrapping around to the first one. Does nothing without a search.</summary>
        public void FindNext() => OnFindHandler(handler => handler.FindNextCore(forward: true));

        /// <summary>Moves to the previous match, wrapping around to the last one. Does nothing without a search.</summary>
        public void FindPrevious() => OnFindHandler(handler => handler.FindNextCore(forward: false));

        /// <summary>Ends the search and removes the highlights.</summary>
        public void ClearFind() => OnFindHandler(handler => handler.ClearFindCore());

        AdvancedWebViewHandler FindHandler() =>
            Handler as AdvancedWebViewHandler ?? throw new InvalidOperationException(
                "AdvancedWebView is not attached to its handler. Call builder.UseAdvancedWebView() in MauiProgram and wait until the control is loaded.");

        // Without a handler there is no search to move or clear: nothing to do.
        void OnFindHandler(Action<AdvancedWebViewHandler> action)
        {
            if (Handler is AdvancedWebViewHandler handler)
                MainThread.BeginInvokeOnMainThread(() => action(handler));
        }

        /// <summary>Raises <see cref="FindResultsUpdated"/>; called by the handler on the main thread.</summary>
        internal void OnFindResultsUpdated(int activeIndex, int count)
        {
            try
            {
                FindResultsUpdated?.Invoke(this, new FindResultsUpdatedEventArgs(activeIndex, count));
            }
            catch (Exception ex)
            {
                LogWarning($"FindResultsUpdated handler failed: {ex.Message}");
            }
        }
    }
}
