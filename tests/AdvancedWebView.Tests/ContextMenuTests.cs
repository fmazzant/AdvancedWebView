using Jint;

namespace Mafesoft.Maui.Controls.Tests;

// #57: context menu targets and the script that turns the menu off.
public class ContextMenuTests
{
    [Theory]
    [InlineData(null, null, null, false, false, ContextMenuKind.Page)]
    [InlineData("", "", "", false, false, ContextMenuKind.Page)]
    [InlineData(null, "https://example.com/", null, false, false, ContextMenuKind.Link)]
    [InlineData(null, "https://example.com/", "https://example.com/a.png", false, false, ContextMenuKind.Link)]
    [InlineData(null, null, "https://example.com/a.png", false, false, ContextMenuKind.Image)]
    [InlineData("hello", "https://example.com/", null, false, false, ContextMenuKind.Text)]
    [InlineData(null, null, null, false, true, ContextMenuKind.Text)]
    [InlineData(null, null, null, true, false, ContextMenuKind.Media)]
    [InlineData(null, "https://example.com/", null, true, false, ContextMenuKind.Link)]
    public void Kind_follows_what_the_platform_reports(string? text, string? link, string? image, bool isMedia, bool isEditable, ContextMenuKind kind) =>
        Assert.Equal(kind, ContextMenus.GetKind(text, link, image, isMedia, isEditable));

    [Theory]
    [InlineData(0, null, ContextMenuKind.Page, null, null, false)]
    [InlineData(7, "https://example.com/", ContextMenuKind.Link, "https://example.com/", null, false)]
    [InlineData(1, "https://example.com/", ContextMenuKind.Link, "https://example.com/", null, false)]
    [InlineData(5, "https://example.com/a.png", ContextMenuKind.Image, null, "https://example.com/a.png", false)]
    [InlineData(8, "https://example.com/a.png", ContextMenuKind.Image, null, "https://example.com/a.png", false)]
    [InlineData(2, "+39 055 123", ContextMenuKind.Link, "tel:+39 055 123", null, false)]
    [InlineData(4, "info@example.com", ContextMenuKind.Link, "mailto:info@example.com", null, false)]
    [InlineData(3, "1 Main St", ContextMenuKind.Link, "geo:0,0?q=1%20Main%20St", null, false)]
    [InlineData(9, null, ContextMenuKind.Text, null, null, true)]
    [InlineData(7, null, ContextMenuKind.Page, null, null, false)]
    [InlineData(5, "", ContextMenuKind.Page, null, null, false)]
    [InlineData(42, "x", ContextMenuKind.Page, null, null, false)]
    public void Android_hit_test_results_are_mapped(int type, string? extra, ContextMenuKind kind, string? link, string? image, bool editable) =>
        Assert.Equal((kind, link, image, editable), ContextMenus.FromAndroidHitTest(type, extra));

    [Theory]
    [InlineData(0, true)]
    [InlineData(7, true)]
    [InlineData(5, true)]
    [InlineData(9, false)]
    public void Android_long_press_is_consumed_except_in_text_fields(int type, bool consumed) =>
        Assert.Equal(consumed, ContextMenus.ConsumeAndroidLongPress(type));

    const string Dom = """
        var elements = {};
        var listeners = [];
        var document = {
          documentElement: {},
          head: { appendChild: function (e) { if (e.id) { elements[e.id] = e; } } },
          getElementById: function (id) { return elements[id] || null; },
          createElement: function (tag) { return { tag: tag, id: "", name: "", content: "", textContent: "" }; },
          querySelector: function () { return null; },
          addEventListener: function (type, f, capture) { listeners.push({ type: type, f: f, capture: capture }); }
        };
        function fire() {
          var e = { prevented: false, preventDefault: function () { this.prevented = true; } };
          listeners.forEach(function (l) { if (l.type === "contextmenu") { l.f(e); } });
          return e.prevented;
        }
        """;

    static Engine Run(params string[] scripts)
    {
        var engine = new Engine();
        engine.Execute(Dom);
        foreach (var script in scripts)
            engine.Execute(script);
        return engine;
    }

    static string Css(Engine engine) => engine.Evaluate($"elements['{UserScriptCode.DisplayStyleId}'].textContent").AsString();

    [Fact]
    public void Menu_off_stops_the_contextmenu_event_and_the_callout()
    {
        var engine = Run(UserScriptCode.Display(textSelection: true, disableZoom: false, disableContextMenu: true));

        Assert.True(engine.Evaluate("fire()").AsBoolean());
        Assert.Equal("contextmenu", engine.Evaluate("listeners[0].type").AsString());
        Assert.True(engine.Evaluate("listeners[0].capture").AsBoolean());
        Assert.Contains("-webkit-touch-callout: none", Css(engine));
        Assert.DoesNotContain("user-select", Css(engine)); // text selection stays
    }

    [Fact]
    public void Menu_on_adds_no_listener()
    {
        var engine = Run(UserScriptCode.Display(true, false));

        Assert.Equal(0, engine.Evaluate("listeners.length").AsNumber());
        Assert.False(engine.Evaluate("fire()").AsBoolean());
        Assert.Equal("", Css(engine));
    }

    [Fact]
    public void Running_again_turns_the_menu_back_on_without_a_second_listener()
    {
        var engine = Run(
            UserScriptCode.Display(true, false, disableContextMenu: true),
            UserScriptCode.Display(true, false, disableContextMenu: true),
            UserScriptCode.Display(true, false, disableContextMenu: false));

        Assert.Equal(1, engine.Evaluate("listeners.length").AsNumber());
        Assert.False(engine.Evaluate("fire()").AsBoolean());
        Assert.Equal("", Css(engine));

        engine.Execute(UserScriptCode.Display(true, false, disableContextMenu: true));
        Assert.Equal(1, engine.Evaluate("listeners.length").AsNumber());
        Assert.True(engine.Evaluate("fire()").AsBoolean());
    }

    [Fact]
    public void Menu_off_with_selection_off_keeps_the_selection_rules()
    {
        var css = Css(Run(UserScriptCode.Display(textSelection: false, disableZoom: false, disableContextMenu: true)));

        Assert.Contains("user-select: none", css);
        Assert.Contains("-webkit-touch-callout: none", css);
        Assert.Contains("input, textarea", css);
    }
}
