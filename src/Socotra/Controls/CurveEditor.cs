using Microsoft.AspNetCore.Components;

namespace Socotra;

/// <summary>
/// Edits a <see cref="Curve"/>, a <see cref="CurveRange"/> or several named curves at once. The user double clicks to add
/// keys, drags keys and their tangent handles, drags a box to select several, and pans and zooms the graph. Ctrl+Z and
/// Ctrl+Y undo and redo, Delete removes the selected keys, F frames the selection and Home fits every curve. Right-click
/// the graph for more commands. The toolbar above the graph has undo, snapping and framing, and its Advanced button turns
/// on <see cref="ShowInspector"/>. Below the graph is a row of preset curves to start from, and the user can save their
/// own. In markup it's <c>&lt;curveeditor&gt;</c>.
/// </summary>
/// <example>
/// <code>
/// // Edit how a particle fades out over its life
/// var editor = new CurveEditor { Parent = panel, Value = particle.FadeCurve };
/// editor.ValueChanged = curve => particle.FadeCurve = curve;
/// </code>
/// </example>
[StyleSheet.Inline("curveeditor", """
    .curveeditor { flex-direction: column; min-width: 0; flex-grow: 1; pointer-events: all; }
    .curveeditor > .curve-body { flex-direction: row; height: 380px; flex-shrink: 0; min-width: 0; }
    .curveeditor .curve-body > .curve-canvas { flex-grow: 1; min-width: 0; overflow: hidden; pointer-events: all; }
    .curveeditor .curve-inspector { display: none; width: 184px; flex-shrink: 0; flex-direction: column; align-items: stretch; }
    .curveeditor.show-inspector .curve-inspector { display: flex; }
    .curveeditor .curve-key-field { flex-direction: column; align-items: stretch; }
    .curveeditor .curve-presets { flex-direction: row; flex-shrink: 0; flex-wrap: wrap; max-height: 148px; overflow: scroll; }
    .curveeditor .curve-preset { width: 54px; height: 30px; flex-shrink: 0; }
    """)]
public partial class CurveEditor : Panel
{
    private const float KeySpacing = 0.0001f;

    private readonly HashSet<CurveKey> _selection = [];
    private readonly CurveCanvas _canvas;
    private readonly CurveKeyInspector _inspector;
    private readonly CurveEditorToolbar _tools;
    private readonly Stack<EditorState> _undo = new();
    private readonly Stack<EditorState> _redo = new();
    private Curve[] _curves = [Curve.Ease];
    private EditorState? _currentEdit;

    /// <summary>Makes an editor showing <see cref="Curve.Ease"/>.</summary>
    public CurveEditor()
    {
        AddClass("curveeditor");
        _tools = AddChild(new CurveEditorToolbar(this));
        var body = Add.Panel("curve-body");
        _canvas = body.AddChild(new CurveCanvas(this));
        _inspector = body.AddChild(new CurveKeyInspector(this));
        AddChild(new CurvePresets(this));
        Sync();
    }

    /// <summary>Called when the user starts changing the curves, like when a drag begins.</summary>
    public event Action? EditStarted;

    /// <summary>Called when the user finishes a change, like when a drag ends.</summary>
    public event Action? EditFinished;

    /// <summary>The curve being edited. Setting it edits just that curve and forgets the undo history, without calling <see cref="ValueChanged"/>.</summary>
    /// <exception cref="ArgumentException">A key's time or value isn't a finite number.</exception>
    [Parameter]
    public Curve Value
    {
        get => ActiveCurveValue;
        set => Assign([Normalize(value)]);
    }

    /// <summary>
    /// The range being edited, both of its curves drawn with the space between them filled. Setting it edits the range and
    /// forgets the undo history, without calling <see cref="RangeValueChanged"/>. The two curves can have different ranges and keys.
    /// </summary>
    /// <exception cref="ArgumentException">A key's time or value isn't a finite number.</exception>
    [Parameter]
    public CurveRange RangeValue
    {
        get => new(_curves[0], _curves.Length == 2 ? _curves[1] : _curves[0]);
        set => Assign([Normalize(value.A), Normalize(value.B)], range: true);
    }

    /// <summary>Whether the editor is editing a <see cref="RangeValue"/>.</summary>
    public bool IsRange { get; private set; }

    /// <summary>Which curve the user is working on: its index in <see cref="Channels"/>, or 0 and 1 for the sides of a range.</summary>
    public int ActiveCurve { get; private set; }

    /// <summary>The selected key on the active curve that number edits apply to, or -1.</summary>
    public int SelectedIndex { get; private set; } = -1;

    /// <summary>The selected keys, on any curve.</summary>
    public IReadOnlyCollection<CurveKey> SelectedKeys => _selection.ToArray();

    /// <summary>Whether there's a change to undo.</summary>
    public bool CanUndo => _undo.Count > 0;

    /// <summary>Whether there's an undone change to redo.</summary>
    public bool CanRedo => _redo.Count > 0;

    /// <summary>Snaps dragged keys to <see cref="SnapIncrement"/>. Holding Ctrl while dragging snaps too.</summary>
    [Parameter]
    public bool SnapToGrid { get; set; }

    /// <summary>What keys snap to in time and value, in the curve's own units. 0 or less leaves that axis alone.</summary>
    [Parameter]
    public Vector2 SnapIncrement { get; set; } = new(0.01f, 0.01f);

    /// <summary>Called with the curve after every change the user makes to it, including undo and a canceled drag.</summary>
    [Parameter]
    public Action<Curve>? ValueChanged { get; set; }

    /// <summary>Called with the range after every change the user makes to either side of it.</summary>
    [Parameter]
    public Action<CurveRange>? RangeValueChanged { get; set; }

    private Curve ActiveCurveValue => _curves[ActiveCurve];

    /// <summary>Undoes the last change.</summary>
    public void Undo() => Restore(_undo, _redo);

    /// <summary>Redoes the last undone change.</summary>
    public void Redo() => Restore(_redo, _undo);

    /// <summary>Handles Ctrl+Z, Ctrl+Y and Ctrl+Shift+Z, unless a text box inside the editor has focus.</summary>
    public override void OnButtonTyped(ButtonEvent e)
    {
        if (!Descendants.OfType<TextEntry>().Any(x => x.HasFocus) && e.HasCtrl && e.Button is "z" or "y")
        {
            if (e.Button == "y" || e.HasShift)
            {
                Redo();
            }
            else
            {
                Undo();
            }

            e.StopPropagation = true;
            return;
        }

        base.OnButtonTyped(e);
    }

    internal void BeginEdit()
    {
        if (_currentEdit is not null)
        {
            return;
        }

        _currentEdit = Capture();
        EditStarted?.Invoke();
    }

    internal void EndEdit(bool cancel = false)
    {
        if (_currentEdit is not { } before)
        {
            return;
        }

        _currentEdit = null;
        if (!Same(before.Curves, _curves))
        {
            if (cancel)
            {
                Install(before);
                Notify();
            }
            else
            {
                _undo.Push(before);
                _redo.Clear();
            }
        }

        Sync();
        EditFinished?.Invoke();
    }

    internal Vector2 SnapPoint(Vector2 displayPoint)
    {
        var (origin, span) = Units();
        var point = origin + (displayPoint * span);
        point = new Vector2(Snap(point.X, SnapIncrement.X), Snap(point.Y, SnapIncrement.Y));
        return (point - origin) / span;

        static float Snap(float value, float step) => float.IsFinite(step) && step > 0 ? MathF.Round(value / step) * step : value;
    }

    private static bool Same(Curve[] a, Curve[] b) => a.Length == b.Length && a.Zip(b).All(x => x.First.SameAs(x.Second));

    private static Curve Normalize(Curve curve)
    {
        curve = curve.WithValidRanges();
        var frames = curve.Length == 0 ? [] : curve.Frames.OrderBy(x => x.Time).ToArray();
        for (int i = 0; i < frames.Length; i++)
        {
            if (!float.IsFinite(frames[i].Time) || !float.IsFinite(frames[i].Value))
            {
                throw new ArgumentException("Curve keys must have finite time and value coordinates.", nameof(curve));
            }

            if (!float.IsFinite(frames[i].In))
            {
                frames[i].In = 0;
            }

            if (!float.IsFinite(frames[i].Out))
            {
                frames[i].Out = 0;
            }
        }

        return curve.WithFrames(frames);
    }

    private void Assign(Curve[] curves, CurveChannel[]? channels = null, bool range = false)
    {
        if (Same(_curves, curves) && IsRange == range && SameChannels(channels))
        {
            return;
        }

        _canvas.Finish();
        EndEdit();
        _curves = curves;
        _channels = channels;
        IsRange = range;
        _selection.Clear();
        ActiveCurve = 0;
        SelectedIndex = -1;
        _undo.Clear();
        _redo.Clear();
        Sync();
    }

    private EditorState Capture() => new((Curve[])_curves.Clone(), [.. _selection], ActiveCurve, SelectedIndex);

    private void Install(EditorState state)
    {
        _curves = (Curve[])state.Curves.Clone();
        ActiveCurve = state.Active;
        SelectedIndex = state.Primary;
        _selection.Clear();
        _selection.UnionWith(state.Selection);
    }

    private void Notify()
    {
        ValueChanged?.Invoke(ActiveCurveValue);
        if (IsRange)
        {
            RangeValueChanged?.Invoke(RangeValue);
        }

        if (_channels is not null)
        {
            ChannelsChanged?.Invoke(Channels);
        }
    }

    private void Change(Curve[] curves, CurveKey[]? selection = null)
    {
        if (Same(_curves, curves))
        {
            if (selection is not null)
            {
                SelectKeys(selection);
            }

            return;
        }

        bool single = _currentEdit is null;
        BeginEdit();
        _curves = curves;
        if (selection is not null)
        {
            SelectKeys(selection);
        }

        Sync();
        Notify();
        if (single)
        {
            EndEdit();
        }
    }

    private void ChangeCurve(Curve curve, int selection)
    {
        var curves = (Curve[])_curves.Clone();
        curves[ActiveCurve] = curve;
        Change(curves, selection < 0 ? [] : [new CurveKey(ActiveCurve, selection)]);
    }

    private void Restore(Stack<EditorState> from, Stack<EditorState> to)
    {
        _canvas.Finish();
        EndEdit();
        if (!from.TryPop(out var item))
        {
            return;
        }

        EditStarted?.Invoke();
        to.Push(Capture());
        Install(item);
        Sync();
        Notify();
        EditFinished?.Invoke();
    }

    private void EditNumber(string text, bool time)
    {
        if (SelectedIndex < 0 || !Translation.TryParseTypedNumber(text, out var value) || !float.IsFinite(value))
        {
            return;
        }

        var key = ActiveCurveValue.Frames[SelectedIndex];
        var (origin, span) = UnitsOf(ActiveCurveValue);
        var old = origin + (new Vector2(key.Time, key.Value) * span);
        var display = Units().Span;
        float delta = time ? (value - old.X) / display.X : (value - old.Y) / display.Y;
        MoveSelection(time ? new Vector2(delta, 0) : new Vector2(0, delta));
    }

    private void Sync()
    {
        _canvas.NavigationBounds = _navigationBounds;
        _inspector.Sync();
        _tools.Sync();
    }

    /// <summary>A key in the editor: which curve it's on and which key of that curve it is.</summary>
    /// <param name="CurveIndex">The curve's index in <see cref="Channels"/>, or 0 and 1 for the sides of a range.</param>
    /// <param name="KeyIndex">The key's index in the curve's <see cref="Curve.Frames"/>.</param>
    public readonly record struct CurveKey(int CurveIndex, int KeyIndex);

    private sealed record EditorState(Curve[] Curves, CurveKey[] Selection, int Active, int Primary);
}
