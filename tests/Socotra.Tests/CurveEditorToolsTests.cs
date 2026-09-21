using static Socotra.Tests.ControlTesting;

namespace Socotra.Tests;

public sealed class CurveEditorToolsTests : IDisposable
{
    private readonly float _reopenGuard = Menu.ReopenGuard;

    public CurveEditorToolsTests()
    {
        Menu.ReopenGuard = 0;
    }

    public void Dispose() => Menu.ReopenGuard = _reopenGuard;

    private static (RootPanel Root, CurveEditor Editor) Build()
    {
        var root = Root();
        var editor = root.AddChild(new CurveEditor { PresetStore = new CurvePresetStore() });
        editor.Style.Width = 760;
        Update(root);
        Update(root);
        return (root, editor);
    }

    private static Toolbar Tools(CurveEditor editor) => editor.Children.OfType<Toolbar>().Single();

    private static Toolbar Inspector(CurveEditor editor) => editor.Descendants.OfType<Toolbar>().Single(x => x.HasClass("curve-inspector"));

    private static Button ToolButton(CurveEditor editor, string icon) => Tools(editor).Children.OfType<Button>().Single(x => x.Icon == icon);

    private static GraphPanel Canvas(CurveEditor editor) => editor.Descendants.OfType<GraphPanel>().Single();

    private static Menu[] MenuOptions(RootPanel root) => [.. root.Children.OfType<Popup>().Single(p => p.HasClass("menulist")).Children.OfType<Menu>()];

    private static string? TooltipText(RootPanel root) => (root.Tooltips.Current?.Children.FirstOrDefault() as Label)?.Text;

    [Fact]
    public void TheToolbarUndoesDeletesSnapsAndShowsTheInspector()
    {
        var (root, editor) = Build();
        var undo = ToolButton(editor, "undo");
        var delete = ToolButton(editor, "delete");
        Assert.True(undo.Disabled);
        Assert.True(ToolButton(editor, "redo").Disabled);
        Assert.True(delete.Disabled);
        Assert.Equal("Undo (Ctrl+Z)", undo.Tooltip);

        editor.SelectKey(1);
        Update(root);
        Assert.False(delete.Disabled);
        Click(root, delete);
        Assert.Equal(1, editor.Value.Length);
        Assert.False(undo.Disabled);
        Click(root, undo);
        Assert.Equal(2, editor.Value.Length);
        Assert.False(ToolButton(editor, "redo").Disabled);

        var snap = ToolButton(editor, "grid_on");
        Assert.Equal("Snap: time 0.01, value 0.01 (Ctrl while dragging)", snap.Tooltip);
        Click(root, snap);
        Assert.True(editor.SnapToGrid);
        Assert.True(snap.Active);

        var inspector = Inspector(editor);
        Assert.False(inspector.IsVisible);
        Click(root, ToolButton(editor, "tune"));
        Assert.True(editor.ShowInspector);
        Assert.True(editor.HasClass("show-inspector"));
        Assert.True(inspector.IsVisible);
        editor.ShowInspector = false;
        Update(root);
        Assert.False(ToolButton(editor, "tune").Active);
        Assert.False(inspector.IsVisible);
    }

    [Fact]
    public void TheInspectorEditsTheSelectedKeys()
    {
        var (root, editor) = Build();
        var curve = Curve.Linear;
        curve.ValueRange = new Vector2(0, 10);
        editor.Value = curve;
        editor.ShowInspector = true;
        Update(root);
        var inspector = Inspector(editor);
        var fields = inspector.Descendants.OfType<NumberEntry>().ToArray();
        var title = inspector.Children.OfType<Label>().Single(x => x.HasClass("curve-inspector-title"));
        Assert.Equal("Select a key", title.Text);
        Assert.All(fields, x => Assert.True(x.Disabled));

        editor.SelectKey(1);
        Update(root);
        Assert.Equal("Curve · Key 2", title.Text);
        Assert.Equal("1", fields[0].Text);
        Assert.Equal("10", fields[1].Text);
        Assert.Equal("Key value; moves selected keys together", fields[1].Tooltip);

        fields[1].Focus();
        Update(root);
        Update(root);
        fields[1].Text = "2.5";
        fields[1].OnValueChanged();
        fields[1].Text = "4";
        fields[1].OnValueChanged();
        Assert.Equal(0.4f, editor.Value.Frames[1].Value, 0.0001f);
        fields[1].Blur();
        Update(root);
        editor.Undo();
        Assert.Equal(1, editor.Value.Frames[1].Value, 0.0001f);
        editor.Redo();

        var tangents = inspector.Children.OfType<Button>().Single(x => x.HasClass("curve-tangent-mode"));
        Click(root, tangents);
        var options = MenuOptions(root);
        Assert.Equal(Enum.GetNames<Curve.HandleMode>(), options.Select(x => x.Text));
        Click(root, options.Single(x => x.Text == "Stepped"));
        Assert.Equal(Curve.HandleMode.Stepped, editor.Value.Frames[1].Mode);
        Update(root);
        Assert.Equal("Stepped", tangents.Text);

        Click(root, inspector.Children.OfType<Button>().Single(x => x.Text == "Smooth"));
        Assert.Equal(Curve.HandleMode.Mirrored, editor.Value.Frames[1].Mode);
    }

    [Fact]
    public void RightClickingTheGraphOffersKeyAndViewCommands()
    {
        var (root, editor) = Build();
        var canvas = Canvas(editor);
        MoveTo(root, canvas.Box.Rect.Center);

        Mouse(root, canvas, "onrightclick");
        Assert.Equal(["Insert key on curve", "Frame selection", "Fit all curves"], MenuOptions(root).Select(x => x.Text));
        Click(root, MenuOptions(root)[0]);
        Assert.Empty(root.Children.OfType<Popup>());
        Assert.Equal(3, editor.Value.Length);

        editor.SelectKey(1);
        MoveTo(root, canvas.Box.Rect.Position + (canvas.Box.Rect.Size * new Vector2(0.25f, 0.15f)));
        Mouse(root, canvas, "onrightclick");
        var options = MenuOptions(root);
        Assert.Equal([.. Enum.GetNames<Curve.HandleMode>(), "Smooth tangents", "Delete selected keys", "Insert key on curve", "Frame selection", "Fit all curves"], options.Select(x => x.Text));
        Click(root, options.Single(x => x.Text == "Linear"));
        Assert.Equal(Curve.HandleMode.Linear, editor.Value.Frames[1].Mode);

        Mouse(root, canvas, "onrightclick");
        Click(root, MenuOptions(root).Single(x => x.Text == "Delete selected keys"));
        Assert.Equal(2, editor.Value.Length);
    }

    [Fact]
    public void PresetsShowTheirNamesAndSavedOnesHaveAMenu()
    {
        var (root, editor) = Build();
        var presets = editor.Descendants.Single(x => x.HasClass("curve-presets"));
        var flat = presets.Children.OfType<Button>().First();
        Assert.Equal("Flat", flat.Tooltip);
        MoveTo(root, flat.Box.Rect.Center);
        Update(root);
        Assert.Equal("Flat", TooltipText(root));

        Mouse(root, flat, "onrightclick");
        Assert.Empty(root.Children.OfType<Popup>());

        var add = presets.Children.OfType<Button>().Last();
        Assert.Equal("Save current curve as a preset", add.Tooltip);
        Click(root, add);
        var saved = presets.Children.Single(x => x.HasClass("saved-curve-preset"));
        Assert.Equal("Saved curve 1 - Right-click for options", saved.Tooltip);

        var curve = Curve.EaseIn;
        curve.TimeRange = new Vector2(0, 4);
        editor.Value = curve;
        Mouse(root, saved, "onrightclick");
        var options = MenuOptions(root);
        Assert.Equal(["Apply with ranges", "Replace with current curve", "Delete preset"], options.Select(x => x.Text));
        Click(root, options[1]);
        Assert.Equal(new Vector2(0, 4), editor.SavedPresets[0].TimeRange);

        editor.Value = Curve.Linear;
        saved = presets.Children.Single(x => x.HasClass("saved-curve-preset"));
        Mouse(root, saved, "onrightclick");
        Click(root, MenuOptions(root)[0]);
        Assert.Equal(new Vector2(0, 4), editor.Value.TimeRange);
        Assert.Equal(Curve.EaseIn.Frames.ToArray(), editor.Value.Frames.ToArray());

        Mouse(root, presets.Children.Single(x => x.HasClass("saved-curve-preset")), "onrightclick");
        Click(root, MenuOptions(root)[2]);
        Assert.Empty(editor.SavedPresets);
        Assert.DoesNotContain(presets.Children, x => x.HasClass("saved-curve-preset"));
    }

    [Fact]
    public void ClickingACurveControlOpensAnEditorThatChangesIt()
    {
        var root = Root();
        var control = root.AddChild(new CurveControl { Value = Curve.Linear });
        control.Style.Width = 200;
        var changes = new List<Curve>();
        control.ValueChanged = changes.Add;
        Update(root);
        Assert.Equal("Edit curve", control.Tooltip);

        Click(root, control);
        var popup = root.Children.OfType<Popup>().Single(x => x.HasClass("curve-editor-popup"));
        var editor = popup.Children.OfType<CurveEditor>().Single();
        Assert.Same(control, popup.PopupSource);
        Assert.Equal("Curve", popup.Children.OfType<Toolbar>().Single().Children.OfType<Label>().Single().Text);
        Assert.Equal(Curve.Linear.Frames.ToArray(), editor.Value.Frames.ToArray());
        Assert.True(editor.HasFocus);

        editor.MoveKey(0, 0, 0.5f);
        Assert.Equal(0.5f, control.Value.Frames[0].Value);
        Assert.Single(changes);
        Assert.Equal(0.5f, changes[0].Frames[0].Value);

        control.Value = Curve.Ease;
        Assert.Equal(Curve.Ease.Frames.ToArray(), editor.Value.Frames.ToArray());

        Key(root, "escape");
        Assert.True(popup.IsDeleted);

        control.Focus();
        Key(root, "enter");
        popup = root.Children.OfType<Popup>().Single(x => x.HasClass("curve-editor-popup"));
        var close = popup.Children.OfType<Toolbar>().Single().Children.OfType<Button>().Single();
        Assert.Equal("Close", close.Tooltip);
        Click(root, close);
        Assert.True(popup.IsDeleted);
    }

    [Fact]
    public void ARangeControlEditsBothCurves()
    {
        var root = Root();
        var control = root.AddChild(new CurveRangeControl { RangeValue = new CurveRange(Curve.Linear, Curve.Ease) });
        control.Style.Width = 200;
        CurveRange? changed = null;
        control.RangeValueChanged = range => changed = range;
        Update(root);

        control.OpenEditor();
        Update(root);
        var popup = root.Children.OfType<Popup>().Single(x => x.HasClass("curve-editor-popup"));
        var editor = popup.Children.OfType<CurveEditor>().Single();
        Assert.True(editor.IsRange);
        Assert.Equal("Curve range", popup.Children.OfType<Toolbar>().Single().Children.OfType<Label>().Single().Text);

        editor.MoveKey(0, 0, 0.25f);
        Assert.NotNull(changed);
        Assert.Equal(0.25f, control.RangeValue.A.Frames[0].Value);
        Assert.Equal(Curve.Ease.Frames.ToArray(), control.RangeValue.B.Frames.ToArray());

        control.OpenEditor();
        Assert.Single(root.Children.OfType<Popup>());
        control.Delete(true);
        Assert.True(popup.IsDeleted);
    }
}
