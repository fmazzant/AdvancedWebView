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
