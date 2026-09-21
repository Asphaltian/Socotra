using System.Text.Json;
using static Socotra.Tests.ControlTesting;

namespace Socotra.Tests;

public class CurveEditorTests
{
    private static RootPanel CreateRoot()
    {
        var root = new UnscaledRoot();
        root.StyleSheet.Parse("rootpanel { flex-direction: column; align-items: flex-start; pointer-events: all; }");
        return root;
    }

    private static void Frame(RootPanel root)
    {
        for (int i = 0; i < 3; i++)
        {
            root.Update(new Rect(0, 0, 600, 600), 0.016f);
        }
    }

    private static void Move(RootPanel root, Vector2 position)
    {
        root.SetMousePosition(position);
        Frame(root);
    }

    private static void Button(RootPanel root, bool down)
    {
        root.SetMouseButton(MouseButtons.Left, down);
        Frame(root);
    }

    private static NumberEntry Axis(CurveEditor editor, string name) => editor.Descendants.OfType<NumberEntry>().Single(x => x.HasClass(name));

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void AxisEditsRescaleTheLimitsAndUndoPutsThemBack(bool horizontal, bool maximum)
    {
        var editor = new CurveEditor { Channels = [new("X", new Color(1, 0, 0), Curve.Ease), new("Y", new Color(0, 1, 0), Curve.Ease)] };
        editor.NavigationBounds = new Rect(0, 0, 1, 1);

        Axis(editor, $"axis-{(horizontal ? "x" : "y")}-{(maximum ? "max" : "min")}").OnTextEdited!(maximum ? "10" : "-10");

        var min = horizontal ? editor.ViewMin.X : editor.ViewMin.Y;
        var max = horizontal ? editor.ViewMax.X : editor.ViewMax.Y;
        Assert.Equal(maximum ? 0 : -10, min, 0.001f);
        Assert.Equal(maximum ? 10 : 1, max, 0.001f);
        var limits = editor.NavigationBounds!.Value;
        Assert.Equal(min, horizontal ? limits.Left : limits.Top, 0.001f);
        Assert.Equal(max, horizontal ? limits.Right : limits.Bottom, 0.001f);

        editor.Undo();
        Assert.Equal(Vector2.Zero, editor.ViewMin);
        Assert.Equal(Vector2.One, editor.ViewMax);
        Assert.Equal(new Rect(0, 0, 1, 1), editor.NavigationBounds!.Value);

        editor.Redo();
        Assert.Equal(max, horizontal ? editor.ViewMax.X : editor.ViewMax.Y, 0.001f);
    }

    [Fact]
    public void ChannelsShareSelectionAndUndo()
    {
        var original = new Curve(new Curve.Frame(0.5f, 0.5f));
        var editor = new CurveEditor { Channels = [new("X", new Color(1, 0, 0), original), new("Y", new Color(0, 1, 0), original), new("Z", new Color(0, 0, 1), original)] };
        int notifications = 0;
        int rangeNotifications = 0;
        editor.ChannelsChanged = channels =>
        {
            notifications++;
            Assert.Equal("Z", channels[2].Name);
        };
        editor.RangeValueChanged = _ => rangeNotifications++;
        Assert.False(editor.IsRange);

        editor.SelectAll();
        Assert.Equal(3, editor.SelectedKeys.Count);
        editor.MoveSelection(new Vector2(0.1f, 0.2f));
        foreach (var channel in editor.Channels)
        {
            Assert.Equal(0.6f, channel.Value.Frames[0].Time, 0.0001f);
            Assert.Equal(0.7f, channel.Value.Frames[0].Value, 0.0001f);
        }

        Assert.Equal(1, notifications);
        Assert.Equal(0, rangeNotifications);

        editor.Undo();
        Assert.All(editor.Channels, channel => Assert.Equal(0.5f, channel.Value.Frames[0].Value));

        editor.Redo();
        editor.SelectCurve(2);
        editor.ApplyPreset(Curve.Linear);
        Assert.Equal(1, editor.Channels[0].Value.Length);
        Assert.Equal(2, editor.Channels[2].Value.Length);
        Assert.Equal(new Color(0, 0, 1), editor.Channels[2].Color);
    }

    [Fact]
    public void AssigningCurvesIsSilentAndForgetsHistory()
    {
        var editor = new CurveEditor();
        int changes = 0;
        editor.ChannelsChanged = _ => changes++;
        editor.ValueChanged = _ => changes++;

        editor.Channels = [new("R", new Color(1, 0, 0), Curve.Ease), new("G", new Color(0, 1, 0), Curve.Ease)];
        Assert.False(editor.IsRange);
        editor.RangeValue = new CurveRange(Curve.Ease, Curve.Ease);
        Assert.True(editor.IsRange);
        editor.Value = Curve.Linear;
        Assert.Single(editor.Channels);
        Assert.Equal(0, changes);
        Assert.False(editor.CanUndo);
        Assert.Throws<ArgumentException>(() => editor.Channels = []);
        Assert.Throws<ArgumentException>(() => editor.Value = new Curve(new Curve.Frame(float.NaN, 0)));
    }

    [Fact]
    public void AxisBoxesChangeTheCurveRangesWhileNavigationOnlyMovesTheView()
    {
        var curve = Curve.Ease;
        curve.TimeRange = new Vector2(2, 8);
        curve.ValueRange = new Vector2(-10, 10);
        var editor = new CurveEditor { Value = curve };
        int changes = 0;
        editor.ValueChanged = _ => changes++;

        editor.SetViewBounds(new Vector2(0, -20), new Vector2(10, 30));
        Assert.Equal(new Vector2(0, -20), editor.ViewMin);
        Assert.Equal(new Vector2(10, 30), editor.ViewMax);
        Assert.Equal(0, changes);

        var maximum = Axis(editor, "axis-y-max");
        maximum.OnTextEdited!("50");
        Assert.Equal(new Vector2(-10, 50), editor.Value.ValueRange);
        Assert.Equal(20, editor.Value.Evaluate(5), 0.001f);
        Assert.Equal(70, editor.ViewMax.Y, 0.001f);
        Assert.Equal(curve.Frames.ToArray(), editor.Value.Frames.ToArray());
        Assert.Equal(1, changes);

        maximum.OnTextEdited!("-30");
        maximum.OnTextEdited!("NaN");
        Assert.Equal(1, changes);

        editor.Undo();
        Assert.Equal(curve.ValueRange, editor.Value.ValueRange);
        editor.Redo();
        Assert.Equal(50, editor.Value.ValueRange.Y);
    }

    [Fact]
    public void KeysStaySortedAndApartAndKeepTheirRanges()
    {
        var curve = Curve.Linear;
        curve.TimeRange = new Vector2(2, 8);
        curve.ValueRange = new Vector2(-10, 10);
        var editor = new CurveEditor { Value = curve };

        editor.AddKey(0.5f, 0.75f);
        editor.AddKey(0.5f, 1);
        Assert.Equal(3, editor.Value.Length);
        Assert.Equal(1, editor.SelectedIndex);

        editor.MoveKey(1, 2, 0.2f);
        Assert.True(editor.Value.Frames[1].Time < editor.Value.Frames[2].Time);
        Assert.Equal(new Vector2(2, 8), editor.Value.TimeRange);
        Assert.Equal(new Vector2(-10, 10), editor.Value.ValueRange);

        editor.MoveKey(1, float.NaN, 0);
        Assert.Equal(0.2f, editor.Value.Frames[1].Value);
    }

    [Fact]
    public void TangentsFollowMirroredAndSplitModes()
    {
        var editor = new CurveEditor();
        editor.SetTangent(0, false, 3);
        Assert.Equal(-3, editor.Value.Frames[0].In);
        Assert.Equal(3, editor.Value.Frames[0].Out);

        editor.SetKeyMode(0, Curve.HandleMode.Split);
        editor.SetTangent(0, true, 2);
        Assert.Equal(3, editor.Value.Frames[0].Out);
        Assert.Equal(2, editor.Value.Frames[0].In);

        editor.SetKeyMode(0, Curve.HandleMode.Flat);
        editor.SetTangent(0, false, 10);
        Assert.Equal(3, editor.Value.Frames[0].Out);
    }

    [Fact]
    public void AWholeDragIsOneUndoStepAndCancelingPutsItBack()
    {
        var editor = new CurveEditor();
        int started = 0;
        int finished = 0;
        editor.EditStarted += () => started++;
        editor.EditFinished += () => finished++;

        editor.SelectKey(0);
        editor.BeginEdit();
        editor.MoveKey(0, 0.1f, 0.1f);
        editor.MoveKey(0, 0.2f, 0.2f);
        editor.EndEdit();
        Assert.Equal(1, started);
        Assert.Equal(1, finished);

        editor.Undo();
        Assert.Equal(0, editor.Value.Frames[0].Time);
        Assert.False(editor.CanUndo);
        editor.Redo();
        Assert.Equal(0.2f, editor.Value.Frames[0].Time);

        editor.BeginEdit();
        editor.MoveKey(0, 0.3f, 0.8f);
        editor.EndEdit(true);
        Assert.Equal(0.2f, editor.Value.Frames[0].Time);
        editor.Undo();
        Assert.Equal(0, editor.Value.Frames[0].Time);
    }

    [Fact]
    public void RangeSelectionsMoveInEachCurvesOwnUnits()
    {
        var a = new Curve(new Curve.Frame(0.5f, 0.5f)) { TimeRange = new Vector2(0, 10), ValueRange = new Vector2(-5, 5) };
        var b = new Curve(new Curve.Frame(0.5f, 0.5f)) { TimeRange = new Vector2(2, 6), ValueRange = new Vector2(0, 20) };
        var editor = new CurveEditor { RangeValue = new CurveRange(a, b) };
        int changes = 0;
        editor.RangeValueChanged = _ => changes++;

        editor.SelectKeys([new(0, 0), new(1, 0)]);
        editor.MoveSelection(new Vector2(0.1f, 0.2f));
        Assert.Equal(0.6f, editor.RangeValue.A.Frames[0].Time, 0.00001f);
        Assert.Equal(0.7f, editor.RangeValue.A.Frames[0].Value, 0.00001f);
        Assert.Equal(0.75f, editor.RangeValue.B.Frames[0].Time, 0.00001f);
        Assert.Equal(0.6f, editor.RangeValue.B.Frames[0].Value, 0.00001f);
        Assert.Equal(1, changes);

        editor.Undo();
        Assert.Equal(a.Frames.ToArray(), editor.RangeValue.A.Frames.ToArray());
        Assert.Equal(b.Frames.ToArray(), editor.RangeValue.B.Frames.ToArray());
        Assert.Equal(2, editor.SelectedKeys.Count);
        Assert.Equal(1, editor.ActiveCurve);
    }

    [Fact]
    public void GroupsStopTogetherBeforeUnselectedNeighbors()
    {
        var editor = new CurveEditor { Value = new Curve(new Curve.Frame(0.1f, 0), new Curve.Frame(0.3f, 0.2f), new Curve.Frame(0.6f, 0.8f), new Curve.Frame(0.9f, 1)) };
        editor.SelectKeys([new(0, 1), new(0, 2)]);
        editor.MoveSelection(new Vector2(1, 0.1f));

        Assert.True(editor.Value.Frames[2].Time < 0.9f);
        Assert.Equal(0.3f, editor.Value.Frames[2].Time - editor.Value.Frames[1].Time, 0.00001f);
        Assert.Equal(0.9f, editor.Value.Frames[3].Time);
        editor.Undo();
        Assert.Equal(0.3f, editor.Value.Frames[1].Time);
    }

    [Fact]
    public void InsertingKeysKeepsTheCurvesShape()
    {
        var editor = new CurveEditor();
        foreach (var mode in Enum.GetValues<Curve.HandleMode>())
        {
            var original = new Curve(new Curve.Frame(0, 0.2f, -2, 2) { Mode = mode }, new Curve.Frame(1, 0.8f, -0.5f, 0.5f));
            editor.Value = original;
            editor.InsertKey(0.37f);
            Assert.Equal(3, editor.Value.Length);
            for (int i = 0; i <= 100; i++)
            {
                Assert.Equal(original.EvaluateDelta(i / 100f), editor.Value.EvaluateDelta(i / 100f), 0.00001f);
            }

            editor.Undo();
            Assert.Equal(original.Frames.ToArray(), editor.Value.Frames.ToArray());
        }

        var outside = new Curve(new Curve.Frame(0.2f, 0.1f, -3, 3), new Curve.Frame(0.8f, 0.9f, -2, 2));
        editor.Value = outside;
        editor.InsertKey(0.1f);
        editor.InsertKey(0.9f);
        for (int i = 0; i <= 100; i++)
        {
            Assert.Equal(outside.EvaluateDelta(i / 100f), editor.Value.EvaluateDelta(i / 100f), 0.00001f);
        }
    }

    [Fact]
    public void SmoothingKeepsRisingCurvesRising()
    {
        var editor = new CurveEditor { Value = new Curve(new Curve.Frame(0, 0), new Curve.Frame(0.05f, 0.6f), new Curve.Frame(0.8f, 0.7f), new Curve.Frame(1, 1)) };
        editor.SelectAll();
        editor.SmoothSelected();

        float previous = 0;
        for (int i = 0; i <= 1000; i++)
        {
            float value = editor.Value.EvaluateDelta(i / 1000f);
            Assert.True(value >= previous - 0.000001f && value <= 1.000001f);
            previous = value;
        }

        editor.Undo();
        Assert.False(editor.CanUndo);
    }

    [Fact]
    public void SnappingUsesTheCurvesOwnUnits()
    {
        var editor = new CurveEditor();
        Assert.Equal(0.14f, editor.SnapPoint(new Vector2(0.137f, 0.263f)).X, 0.00001f);

        var curve = Curve.Linear;
        curve.TimeRange = new Vector2(2, 8);
        curve.ValueRange = new Vector2(-10, 90);
        editor.Value = curve;
        editor.SnapIncrement = new Vector2(0.01f, 1);
        var point = editor.SnapPoint(new Vector2(0.137f, 0.263f));
        Assert.Equal(2.82f, 2 + (point.X * 6), 0.00001f);
        Assert.Equal(16, -10 + (point.Y * 100), 0.00001f);
    }

    [Fact]
    public void DeletingKeepsOneKey()
    {
        var editor = new CurveEditor();
        editor.SelectKey(0);
        editor.RemoveSelected();
        editor.RemoveSelected();
        Assert.Equal(1, editor.Value.Length);
        editor.Undo();
        Assert.Equal(2, editor.Value.Length);
    }

    [Fact]
    public void KeyboardUndoesRedoesSelectsAndDeletes()
    {
        var root = CreateRoot();
        var editor = root.AddChild(new CurveEditor());
        editor.Style.Width = 600;
        Frame(root);
        editor.Descendants.OfType<GraphPanel>().Single().Focus();
        Frame(root);

        Press(root, "a", KeyboardModifiers.Ctrl);
        Assert.Equal(2, editor.SelectedKeys.Count);
        editor.SelectKey(1);
        Press(root, "delete");
        Assert.Equal(1, editor.Value.Length);
        Press(root, "z", KeyboardModifiers.Ctrl);
        Assert.Equal(2, editor.Value.Length);
        Press(root, "y", KeyboardModifiers.Ctrl);
        Assert.Equal(1, editor.Value.Length);

        editor.SelectKey(0);
        Press(root, "up", KeyboardModifiers.Shift);
        Assert.Equal(0.1f, editor.Value.Frames[0].Value, 0.0001f);
    }

    private static void Press(RootPanel root, string button, KeyboardModifiers modifiers = KeyboardModifiers.None)
    {
        root.AddButtonEvent(new ButtonEvent(button, true, modifiers));
        root.AddButtonEvent(new ButtonEvent(button, false, modifiers));
        Frame(root);
    }

    [Fact]
    public void DraggingAKeyAndItsTangentAreSingleUndoSteps()
    {
        var root = CreateRoot();
        var editor = root.AddChild(new CurveEditor { Value = new Curve(new Curve.Frame(0, 0), new Curve.Frame(0.5f, 0.5f), new Curve.Frame(1, 1)) });
        editor.Style.Width = 600;
        Frame(root);
        var canvas = editor.Descendants.OfType<GraphPanel>().Single();
        var center = canvas.Box.Rect.Position + canvas.CanvasToScreen(new Vector2(0.5f, 0.5f));

        Move(root, center);
        Button(root, true);
        Move(root, center + new Vector2(30, -15));
        Move(root, center + new Vector2(60, -30));
        Button(root, false);
        Assert.True(editor.Value.Frames[1].Time > 0.5f);
        Assert.True(editor.Value.Frames[1].Value > 0.5f);
        editor.Undo();
        Assert.Equal(0.5f, editor.Value.Frames[1].Time);
        Assert.False(editor.CanUndo);

        Frame(root);
        Move(root, center + new Vector2(48, 0));
        Button(root, true);
        Move(root, center + new Vector2(48, -30));
        Button(root, false);
        Assert.True(editor.Value.Frames[1].Out > 0);
        Assert.Equal(-editor.Value.Frames[1].Out, editor.Value.Frames[1].In);
        editor.Undo();
        Assert.Equal(0, editor.Value.Frames[1].Out);
        Assert.False(editor.CanUndo);
    }

    [Fact]
    public void BoxSelectionPicksKeysOnBothCurvesAndEscapeCancelsADrag()
    {
        var root = CreateRoot();
        var editor = root.AddChild(new CurveEditor { RangeValue = new CurveRange(new Curve(new Curve.Frame(0.5f, 0.4f)), new Curve(new Curve.Frame(0.5f, 0.6f))) });
        editor.Style.Width = 600;
        Frame(root);
        var canvas = editor.Descendants.OfType<GraphPanel>().Single();

        Move(root, Point(0.4f, 0.7f));
        Button(root, true);
        Move(root, Point(0.6f, 0.3f));
        Button(root, false);
        Assert.Equal(2, editor.SelectedKeys.Count);

        Move(root, Point(0.5f, 0.4f));
        Button(root, true);
        Move(root, Point(0.7f, 0.5f));
        Assert.Equal(0.7f, editor.RangeValue.A.Frames[0].Time, 0.001f);
        Assert.Equal(0.7f, editor.RangeValue.B.Frames[0].Time, 0.001f);
        Escape(canvas);
        Button(root, false);
        Assert.Equal(0.5f, editor.RangeValue.A.Frames[0].Time);
        Assert.Equal(0.5f, editor.RangeValue.B.Frames[0].Time);
        Assert.False(editor.CanUndo);

        Vector2 Point(float x, float y) => canvas.Box.Rect.Position + canvas.CanvasToScreen(new Vector2(x, y));
    }

    [Fact]
    public void DoubleClickingEmptySpaceAddsAKey()
    {
        var root = CreateRoot();
        var editor = root.AddChild(new CurveEditor { Value = Curve.Linear });
        editor.Style.Width = 600;
        Frame(root);
        var canvas = editor.Descendants.OfType<GraphPanel>().Single();

        Move(root, canvas.Box.Rect.Position + canvas.CanvasToScreen(new Vector2(0.25f, 0.8f)));
        Button(root, true);
        Button(root, false);
        Button(root, true);
        Button(root, false);

        Assert.Equal(3, editor.Value.Length);
        Assert.Equal(0.25f, editor.Value.Frames[1].Time, 0.01f);
        Assert.Equal(0.8f, editor.Value.Frames[1].Value, 0.01f);
        Assert.Equal(1, editor.SelectedIndex);
    }

    [Fact]
    public void FilledRangesKeepSteppedJumps()
    {
        var a = new Curve(new Curve.Frame(0, 0.2f) { Mode = Curve.HandleMode.Stepped }, new Curve.Frame(0.37f, 0.8f), new Curve.Frame(1, 0.8f));
        a.TimeRange = new Vector2(0, 10);
        a.ValueRange = new Vector2(-10, 10);
        var b = new Curve(new Curve.Frame(0.5f, 0.5f)) { TimeRange = new Vector2(2, 6), ValueRange = new Vector2(0, 20) };

        var area = CurveEditor.BuildRangeArea(new CurveRange(a, b), 0, 1, 32);
        var first = area.Take(area.Length / 2).ToArray();
        Assert.Contains(first, p => p.X == 0.37f && MathF.Abs(p.Y - 0.8f) < 0.00001f);
        Assert.Contains(first, p => p.X < 0.37f && p.X > 0.3699f && MathF.Abs(p.Y - 0.2f) < 0.00001f);
        Assert.All(area.Skip(area.Length / 2), p => Assert.True(MathF.Abs(p.Y - 1) < 0.00001f));
    }

    [Fact]
    public void PresetsAreSharedAndSaveAndLoadAsJson()
    {
        var store = new CurvePresetStore();
        var root = CreateRoot();
        var editor = root.AddChild(new CurveEditor { PresetStore = store });
        var other = root.AddChild(new CurveEditor { PresetStore = store });
        var changes = 0;
        store.Changed += () => changes++;

        var curve = Curve.EaseIn;
        curve.TimeRange = new Vector2(0, 4);
        editor.Value = curve;
        editor.SavePreset();
        Frame(root);
        Assert.Single(other.SavedPresets);
        Assert.Equal(new Vector2(0, 4), other.SavedPresets[0].TimeRange);
        Assert.Contains(other.Descendants, p => p.HasClass("saved-curve-preset"));

        var json = store.Save();
        editor.Value = Curve.Linear;
        editor.ReplacePreset(0);
        Assert.Equal(Curve.Linear.Frames.ToArray(), store.Presets[0].Frames.ToArray());

        store.Load(json);
        Assert.Equal(new Vector2(0, 4), store.Presets[0].TimeRange);
        Assert.Equal(curve.Frames.ToArray(), store.Presets[0].Frames.ToArray());

        other.ApplyPreset(store.Presets[0], includeRanges: true);
        Assert.Equal(new Vector2(0, 4), other.Value.TimeRange);

        editor.DeletePreset(0);
        Frame(root);
        Assert.Empty(store.Presets);
        Assert.DoesNotContain(other.Descendants, p => p.HasClass("saved-curve-preset"));
        Assert.Equal(4, changes);
        Assert.Throws<JsonException>(() => store.Load("{}"));
    }

    [Fact]
    public void ReversingMirrorsTheCurveInTime()
    {
        var curve = new Curve(
            new Curve.Frame(0, 0, 0, 2) { Mode = Curve.HandleMode.Split },
            new Curve.Frame(0.3f, 0.8f, -0.5f, 1.5f) { Mode = Curve.HandleMode.Linear },
            new Curve.Frame(0.5f, 0.4f, 1, -1) { Mode = Curve.HandleMode.Split },
            new Curve.Frame(0.8f, 0.6f) { Mode = Curve.HandleMode.Flat },
            new Curve.Frame(1, 1, -1, 1));

        var reversed = curve.Reverse();

        for (int i = 0; i <= 100; i++)
        {
            var time = i / 100f;
            Assert.Equal(curve.EvaluateDelta(1 - time), reversed.EvaluateDelta(time), 0.0001f);
        }
    }

    [Fact]
    public void EditorsShareOneStoreUnlessGivenTheirOwn()
    {
        Assert.Same(CurvePresetStore.Shared, new CurveEditor().PresetStore);
    }

    [Fact]
    public void CurvesRoundTripThroughJson()
    {
        var curve = new Curve(new Curve.Frame(0, 0.25f, -1, 1) { Mode = Curve.HandleMode.Split }, new Curve.Frame(1, 1)) { TimeRange = new Vector2(0, 2) };

        var json = JsonSerializer.Serialize(curve);
        var back = JsonSerializer.Deserialize<Curve>(json);

        Assert.Equal(curve.TimeRange, back.TimeRange);
        Assert.Equal(curve.ValueRange, back.ValueRange);
        Assert.Equal(curve.Frames.ToArray(), back.Frames.ToArray());
        Assert.StartsWith("[", JsonSerializer.Serialize(Curve.Ease));
        Assert.Equal(0.5f, JsonSerializer.Deserialize<Curve>("0.5").Evaluate(0.3f));
    }

    [Fact]
    public void CurveControlsPreviewCurvesAndRanges()
    {
        var root = CreateRoot();
        var control = root.AddChild(new CurveControl { Value = Curve.Linear });
        var range = root.AddChild(new CurveRangeControl { RangeValue = new CurveRange(Curve.Linear, Curve.Ease) });
        Frame(root);

        Assert.Equal(Curve.Linear.Frames.ToArray(), control.Value.Frames.ToArray());
        Assert.Equal(Curve.Ease.Frames.ToArray(), range.RangeValue.B.Frames.ToArray());
        Assert.True(control.Box.Rect.Height > 0);
    }
}
