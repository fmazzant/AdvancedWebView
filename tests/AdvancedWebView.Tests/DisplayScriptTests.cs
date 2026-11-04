using Jint;

namespace Mafesoft.Maui.Controls.Tests;

// UserScriptCode.Display: TextSelectionEnabled and (iOS) ZoomEnabled, run in Jint against a minimal DOM.
public class DisplayScriptTests
{
    const string Dom = """
        var elements = {};
        var viewport = null;
        var document = {
          documentElement: {},
          head: { appendChild: function (e) { if (e.id) { elements[e.id] = e; } if (e.name === "viewport") { viewport = e; } } },
          getElementById: function (id) { return elements[id] || null; },
          createElement: function (tag) { return { tag: tag, id: "", name: "", content: "", textContent: "" }; },
          querySelector: function () { return viewport; },
          addEventListener: function () { throw new Error("DOM is ready: no listener expected"); }
        };
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
    public void Selection_off_adds_the_style()
    {
        var css = Css(Run(UserScriptCode.Display(textSelection: false, disableZoom: false)));

        Assert.Contains("user-select: none", css);
        Assert.Contains("-webkit-touch-callout: none", css);
        Assert.Contains("input, textarea", css); // form fields stay editable
    }

    [Fact]
    public void Running_again_with_selection_on_empties_the_same_style()
    {
        var engine = Run(UserScriptCode.Display(false, false), UserScriptCode.Display(true, false));

        Assert.Equal("", Css(engine));
        Assert.Equal(1, engine.Evaluate("Object.keys(elements).length").AsNumber());
    }

    [Fact]
    public void Zoom_off_adds_user_scalable_no_to_a_new_viewport()
    {
        var engine = Run(UserScriptCode.Display(true, disableZoom: true));

        Assert.Equal("width=device-width, initial-scale=1, maximum-scale=1, user-scalable=no", engine.Evaluate("viewport.content").AsString());
    }

    [Fact]
    public void Zoom_off_keeps_an_existing_viewport_and_its_user_scalable()
    {
        var engine = Run("viewport = { name: 'viewport', content: 'width=device-width, user-scalable=yes' };",
            UserScriptCode.Display(true, disableZoom: true));

        Assert.Equal("width=device-width, user-scalable=yes", engine.Evaluate("viewport.content").AsString());
    }

    [Fact]
    public void Zoom_allowed_leaves_the_viewport_alone() =>
        Assert.True(Run(UserScriptCode.Display(true, disableZoom: false)).Evaluate("viewport === null").AsBoolean());
}
