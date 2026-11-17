using Jint;

namespace Mafesoft.Maui.Controls.Tests;

// #27: find in page. The JavaScript engine (iOS and Mac Catalyst) runs in Jint on a small fake DOM: text nodes, ranges,
// the CSS Custom Highlight API and scrollIntoView.
public class FindInPageTests
{
    const string FakeDom = """
        var window = globalThis;
        var scrolled = [];
        var selected = [];
        function element(tag, visible, parent) {
          return { tagName: tag, parentElement: parent || null,
            getClientRects: function () { return visible ? [1] : []; },
            scrollIntoView: function () { scrolled.push(this.tagName); } };
        }
        var body = element('BODY', true);
        var nodes = [];
        function text(data, tag, visible) { nodes.push({ data: data, parentElement: element(tag || 'P', visible !== false, body) }); }
        var NodeFilter = { SHOW_TEXT: 4 };
        var document = {
          body: body, head: { appendChild: function () { } },
          getElementById: function () { return null; },
          createElement: function () { return {}; },
          createTreeWalker: function () { var i = -1; return { nextNode: function () { i++; return i < nodes.length ? nodes[i] : null; } }; },
          createRange: function () { return { setStart: function (n, o) { this.startContainer = n; this.start = o; }, setEnd: function (n, o) { this.end = o; } }; }
        };
        window.getSelection = function () { return { removeAllRanges: function () { selected = []; }, addRange: function (r) { selected.push(r); } }; };
        """;

    const string Highlights = """
        var CSS = { highlights: new Map() };
        function Highlight() { this.ranges = Array.prototype.slice.call(arguments); }
        window.CSS = CSS; window.Highlight = Highlight;
        """;

    static Engine CreatePage(bool highlights, string texts)
    {
        var engine = new Engine();
        engine.Execute(FakeDom);
        if (highlights)
            engine.Execute(Highlights);
        engine.Execute(texts);
        return engine;
    }

    static (int ActiveIndex, int Count) Run(Engine engine, string script) =>
        FindInPage.ParseResult(engine.Evaluate(script).AsString());

    [Fact]
    public void Start_counts_the_matches_case_insensitively_and_activates_the_first()
    {
        var page = CreatePage(true, "text('Invoice 1 and invoice 2'); text('no match'); text('INVOICE');");

        Assert.Equal((0, 3), Run(page, FindInPage.StartScript("invoice", caseSensitive: false)));
        Assert.Equal(3, page.Evaluate("CSS.highlights.get('advancedwebview-find').ranges.length").AsNumber());
        Assert.Equal(0, page.Evaluate("CSS.highlights.get('advancedwebview-find-active').ranges[0].start").AsNumber());
        Assert.Equal("P", page.Evaluate("scrolled[0]").AsString());
    }

    [Fact]
    public void Start_case_sensitive_counts_exact_matches_only()
    {
        var page = CreatePage(true, "text('Invoice 1 and invoice 2'); text('INVOICE');");

        Assert.Equal((0, 1), Run(page, FindInPage.StartScript("invoice", caseSensitive: true)));
    }

    [Fact]
    public void Hidden_text_and_scripts_are_not_searched()
    {
        var page = CreatePage(true, "text('invoice', 'P', false); text('var invoice = 1;', 'SCRIPT'); text('invoice', 'STYLE'); text('invoice');");

        Assert.Equal((0, 1), Run(page, FindInPage.StartScript("invoice", false)));
    }

    [Fact]
    public void Next_and_previous_wrap_around()
    {
        var page = CreatePage(true, "text('a a a');");
        Run(page, FindInPage.StartScript("a", false));

        Assert.Equal((1, 3), Run(page, FindInPage.NextScript(forward: true)));
        Assert.Equal((2, 3), Run(page, FindInPage.NextScript(forward: true)));
        Assert.Equal((0, 3), Run(page, FindInPage.NextScript(forward: true)));
        Assert.Equal((2, 3), Run(page, FindInPage.NextScript(forward: false)));
        Assert.Equal(4, page.Evaluate("CSS.highlights.get('advancedwebview-find-active').ranges[0].start").AsNumber());
    }

    [Fact]
    public void No_matches_and_clear_report_no_active_match()
    {
        var page = CreatePage(true, "text('hello');");

        Assert.Equal((-1, 0), Run(page, FindInPage.StartScript("invoice", false)));
        Assert.Equal((-1, 0), Run(page, FindInPage.NextScript(true)));

        Run(page, FindInPage.StartScript("hello", false));
        Assert.Equal((-1, 0), Run(page, FindInPage.ClearScript()));
        Assert.Equal(0, page.Evaluate("CSS.highlights.size").AsNumber());
    }

    [Fact]
    public void Without_the_highlight_api_the_active_match_is_selected()
    {
        var page = CreatePage(false, "text('one two one');");

        Assert.Equal((0, 2), Run(page, FindInPage.StartScript("one", false)));
        Assert.Equal(0, page.Evaluate("selected[0].start").AsNumber());

        Run(page, FindInPage.NextScript(true));
        Assert.Equal(8, page.Evaluate("selected[0].start").AsNumber());
        Assert.Equal(1, page.Evaluate("selected.length").AsNumber());
    }

    [Fact]
    public void Search_text_is_escaped()
    {
        var page = CreatePage(true, "text('say \"hi\" \\\\ </script>');");

        Assert.Equal((0, 1), Run(page, FindInPage.StartScript("\"hi\" \\ </script>", false)));
    }

    [Fact]
    public void Clear_script_does_nothing_on_a_page_without_a_search() =>
        Assert.True(CreatePage(true, "").Evaluate(FindInPage.ClearScript()).IsNull());

    [Theory]
    [InlineData(null, -1, 0)]
    [InlineData("", -1, 0)]
    [InlineData("not json", -1, 0)]
    [InlineData("{\"index\":2,\"count\":5}", 2, 5)]
    [InlineData("\"{\\\"index\\\":1,\\\"count\\\":2}\"", 1, 2)]
    [InlineData("{\"index\":0,\"count\":0}", -1, 0)]
    [InlineData("{\"index\":7,\"count\":3}", -1, 3)]
    [InlineData("{\"count\":3}", -1, 3)]
    public void Result_is_parsed(string? json, int index, int count) =>
        Assert.Equal((index, count), FindInPage.ParseResult(json));

    [Theory]
    [InlineData(1, 3, 0)]
    [InlineData(3, 3, 2)]
    [InlineData(-1, 3, -1)]
    [InlineData(0, 3, -1)]
    [InlineData(4, 3, -1)]
    [InlineData(1, 0, -1)]
    public void WebView2_active_index_becomes_zero_based(int activeMatchIndex, int count, int expected) =>
        Assert.Equal(expected, FindInPage.FromOneBased(activeMatchIndex, count));
}
