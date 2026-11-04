using System.Globalization;
using System.Text;

namespace Mafesoft.Maui.Controls
{
    /// <summary>Builds the JavaScript that is actually injected for user scripts and style sheets.</summary>
    internal static class UserScriptCode
    {
        /// <summary>
        /// Wraps <paramref name="source"/> in an <c>if</c> that checks the page origin and, optionally, that the script
        /// runs in the top-level page. Returns the source unchanged when there is nothing to check.
        /// </summary>
        /// <remarks>
        /// The guard is a block, not a function, so <c>var</c> and function declarations stay global.
        /// <c>let</c>/<c>const</c> at the top level of a guarded script become block-scoped.
        /// </remarks>
        public static string Wrap(string source, IReadOnlyCollection<OriginRule> rules, bool mainFrameOnly)
        {
            var conditions = new List<string>();

            if (mainFrameOnly)
                conditions.Add("window === window.top");

            if (rules.Count > 0 && !rules.Any(r => r.IsAny))
                conditions.Add(OriginCondition(rules));

            if (conditions.Count == 0)
                return source;

            return "if (" + string.Join(" && ", conditions) + ") {\n" + source + "\n}";
        }

        /// <summary>Id of the style element of <see cref="Display"/>.</summary>
        public const string DisplayStyleId = "__advancedwebview-display";

        /// <summary>
        /// A script for the display options: a style element (always the same one, so running it again replaces it)
        /// that turns off text selection, and the viewport's <c>user-scalable=no</c> to turn off pinch zoom.
        /// With text selection on and zoom allowed it empties the style element, so it can also undo a previous run.
        /// </summary>
        public static string Display(bool textSelection, bool disableZoom)
        {
            var css = textSelection
                ? string.Empty
                : "* { -webkit-user-select: none !important; user-select: none !important; -webkit-touch-callout: none !important; }\n" +
                  "input, textarea, select, [contenteditable], [contenteditable] * { -webkit-user-select: text !important; user-select: text !important; }";

            return "(function () {\n" +
                   "  var css = " + BridgeProtocol.JsonString(css) + ";\n" +
                   "  var noZoom = " + (disableZoom ? "true" : "false") + ";\n" +
                   "  function style() {\n" +
                   "    var s = document.getElementById(\"" + DisplayStyleId + "\");\n" +
                   "    if (!s) { s = document.createElement(\"style\"); s.id = \"" + DisplayStyleId + "\"; (document.head || document.documentElement).appendChild(s); }\n" +
                   "    s.textContent = css;\n" +
                   "  }\n" +
                   "  function viewport() {\n" +
                   "    var m = document.querySelector(\"meta[name=viewport]\");\n" +
                   "    if (!m) { m = document.createElement(\"meta\"); m.name = \"viewport\"; m.content = \"width=device-width, initial-scale=1\"; document.head.appendChild(m); }\n" +
                   "    if (!/user-scalable/i.test(m.content)) { m.content += \", maximum-scale=1, user-scalable=no\"; }\n" +
                   "  }\n" +
                   "  if (document.documentElement) { style(); } else { document.addEventListener(\"DOMContentLoaded\", style); }\n" +
                   "  if (noZoom) { if (document.head) { viewport(); } else { document.addEventListener(\"DOMContentLoaded\", viewport); } }\n" +
                   "})();";
        }

        /// <summary>A script that adds the style sheets to the page as <c>&lt;style data-advancedwebview&gt;</c> elements.</summary>
        public static string StyleSheets(IEnumerable<string> css)
        {
            var sheets = css.Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
            if (sheets.Count == 0)
                return string.Empty;

            var text = BridgeProtocol.JsonString(string.Join("\n", sheets));
            return "(function () {\n" +
                   "  function add() {\n" +
                   "    var style = document.createElement(\"style\");\n" +
                   "    style.setAttribute(\"data-advancedwebview\", \"\");\n" +
                   "    style.textContent = " + text + ";\n" +
                   "    (document.head || document.documentElement).appendChild(style);\n" +
                   "  }\n" +
                   "  if (document.documentElement) { add(); } else { document.addEventListener(\"DOMContentLoaded\", add); }\n" +
                   "})();";
        }

        // Same semantics as OriginRule.Matches, evaluated against window.location.
        static string OriginCondition(IEnumerable<OriginRule> rules)
        {
            var sb = new StringBuilder();
            sb.Append("(function (l) { var h = l.hostname.toLowerCase(); var p = l.port || (l.protocol === \"https:\" ? \"443\" : l.protocol === \"http:\" ? \"80\" : \"\"); return [");
            sb.Append(string.Join(",", rules.Select(r =>
                "[" + BridgeProtocol.JsonString(r.Scheme + ":") + "," +
                BridgeProtocol.JsonString(r.Host!) + "," +
                (r.Subdomains ? "true" : "false") + "," +
                BridgeProtocol.JsonString(r.Port < 0 ? string.Empty : r.Port.ToString(CultureInfo.InvariantCulture)) + "]")));
            sb.Append("].some(function (r) { return l.protocol === r[0] && p === r[3] && (r[2] ? h.slice(-(r[1].length + 1)) === \".\" + r[1] : h === r[1]); }); })(window.location)");
            return sb.ToString();
        }
    }
}
