using System.Text.Json;

namespace Mafesoft.Maui.Controls
{
    /// <summary>
    /// Find in page in JavaScript (#27), for the platforms without a native find that reports counts (iOS and Mac
    /// Catalyst): the matches are DOM ranges of the main frame, highlighted with the CSS Custom Highlight API where it
    /// exists (iOS 17.2+), otherwise only the active match is shown, as the selection.
    /// </summary>
    internal static class FindInPage
    {
        /// <summary>Property of <c>window</c> that holds the find state of the page.</summary>
        public const string StateName = "__advancedwebviewFind";

        /// <summary>The engine: defines <c>window.__advancedwebviewFind</c> once per page. Every call returns the result as JSON.</summary>
        public const string Engine = """
            (function () {
              if (window.__advancedwebviewFind) return;
              var hasHighlights = !!(window.CSS && CSS.highlights && window.Highlight);
              var state = { ranges: [], index: -1 };
              var skip = { SCRIPT: 1, STYLE: 1, NOSCRIPT: 1, TEMPLATE: 1, TEXTAREA: 1, SELECT: 1, OPTION: 1 };

              function result() { return JSON.stringify({ index: state.index, count: state.ranges.length }); }

              function addStyle() {
                if (!hasHighlights || document.getElementById('__advancedwebview-find')) return;
                var style = document.createElement('style');
                style.id = '__advancedwebview-find';
                style.textContent = '::highlight(advancedwebview-find){background-color:#ffeb3b;color:inherit}' +
                  '::highlight(advancedwebview-find-active){background-color:#ff9632;color:inherit}';
                (document.head || document.documentElement).appendChild(style);
              }

              function visible(element) {
                for (var e = element; e; e = e.parentElement) if (skip[e.tagName]) return false;
                return element.getClientRects().length > 0;
              }

              function show() {
                var active = state.ranges[state.index];
                if (hasHighlights) {
                  CSS.highlights.set('advancedwebview-find', new Highlight(...state.ranges));
                  if (active) CSS.highlights.set('advancedwebview-find-active', new Highlight(active));
                  else CSS.highlights.delete('advancedwebview-find-active');
                } else if (active) {
                  var selection = window.getSelection();
                  selection.removeAllRanges();
                  selection.addRange(active);
                }
                if (active) {
                  var element = active.startContainer.parentElement;
                  if (element) element.scrollIntoView({ block: 'center', inline: 'nearest' });
                }
              }

              function clear() {
                if (hasHighlights) {
                  CSS.highlights.delete('advancedwebview-find');
                  CSS.highlights.delete('advancedwebview-find-active');
                } else if (state.ranges.length) {
                  window.getSelection().removeAllRanges();
                }
                state.ranges = [];
                state.index = -1;
              }

              window.__advancedwebviewFind = {
                start: function (term, caseSensitive) {
                  clear();
                  if (!term || !document.body) return result();
                  var needle = caseSensitive ? term : term.toLowerCase();
                  var walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
                  for (var node = walker.nextNode(); node; node = walker.nextNode()) {
                    var text = caseSensitive ? node.data : node.data.toLowerCase();
                    var at = text.indexOf(needle);
                    if (at < 0 || !node.parentElement || !visible(node.parentElement)) continue;
                    for (; at >= 0; at = text.indexOf(needle, at + needle.length)) {
                      var range = document.createRange();
                      range.setStart(node, at);
                      range.setEnd(node, at + needle.length);
                      state.ranges.push(range);
                    }
                  }
                  if (state.ranges.length) { addStyle(); state.index = 0; }
                  show();
                  return result();
                },
                next: function (forward) {
                  var count = state.ranges.length;
                  if (count) { state.index = (state.index + (forward ? 1 : -1) + count) % count; show(); }
                  return result();
                },
                clear: function () { clear(); return result(); }
              };
            })();
            """;

        /// <summary>Starts a new search for <paramref name="term"/>; the result is the first match, active.</summary>
        public static string StartScript(string term, bool caseSensitive) =>
            Call($"start({JsonSerializer.Serialize(term)}, {(caseSensitive ? "true" : "false")})");

        /// <summary>Moves to the next (or previous) match, wrapping around.</summary>
        public static string NextScript(bool forward) => Call($"next({(forward ? "true" : "false")})");

        /// <summary>Removes the highlights.</summary>
        public static string ClearScript() => $"window.{StateName} ? window.{StateName}.clear() : null;";

        static string Call(string call) => Engine + "\n" + $"window.{StateName}.{call};";

        /// <summary>
        /// The active match (0-based, -1 for none) and the count from the JSON a script returns. Null (no page, a
        /// script error) means no matches.
        /// </summary>
        public static (int ActiveIndex, int Count) ParseResult(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return (-1, 0);
            try
            {
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;
                if (root.ValueKind == JsonValueKind.String)
                    return ParseResult(root.GetString());

                var count = root.TryGetProperty("count", out var c) && c.TryGetInt32(out var n) ? Math.Max(0, n) : 0;
                var index = root.TryGetProperty("index", out var i) && i.TryGetInt32(out var x) ? x : -1;
                return count == 0 || index < 0 || index >= count ? (-1, count) : (index, count);
            }
            catch (JsonException)
            {
                return (-1, 0);
            }
        }

        /// <summary>
        /// Converts a 1-based active match (WebView2's <c>ActiveMatchIndex</c>, -1 for none) to the 0-based
        /// <c>FindResultsUpdatedEventArgs.ActiveIndex</c>.
        /// </summary>
        public static int FromOneBased(int activeMatchIndex, int count) =>
            activeMatchIndex >= 1 && activeMatchIndex <= count ? activeMatchIndex - 1 : -1;
    }
}
