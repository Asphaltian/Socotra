using Microsoft.AspNetCore.Components;

namespace Socotra;

/// <summary>
/// A small preview of a <see cref="Curve"/>, drawn as a line in the panel's text color. Clicking it, or pressing Enter
/// while it has focus, opens a <see cref="CurveEditor"/> under it.
/// </summary>
/// <example>
/// <code>
/// // A fade curve field that the user can click to edit
/// var fade = new CurveControl { Parent = row, Value = particle.FadeCurve };
/// fade.ValueChanged = curve => particle.FadeCurve = curve;
/// </code>
/// </example>
[StyleSheet.Inline("curvecontrol", ".curvecontrol { width: 100%; height: 32px; min-width: 0; flex-shrink: 0; pointer-events: all; cursor: pointer; } .curve-editor-popup { width: 760px; flex-direction: column; }")]
public class CurveControl : Panel
{
    private readonly Painter.CachedLine _lowerLine = new();
    private readonly Painter.CachedLine _upperLine = new();
    private readonly Painter.CachedPolygon _areaFill = new();
    private readonly Vector2[] _lower = new Vector2[65];
    private readonly Vector2[] _upper = new Vector2[65];
    private readonly Vector2[] _area = new Vector2[130];
    private CurveRange _applied = new(Curve.Ease, Curve.Ease);
    private CurveRange? _previewValue;
    private Rect _previewRect;
    private EditorPopup? _popup;
    private CurveEditor? _editor;

    /// <summary>Makes a preview of <see cref="Curve.Ease"/>.</summary>
    public CurveControl()
    {
        AddClass("curvecontrol");
        AcceptsFocus = true;
        Tooltip = "Edit curve";
    }

    /// <summary>The curve shown. A <see cref="CurveRangeControl"/> shows the first side of its <see cref="CurveRangeControl.RangeValue"/> here.</summary>
    [Parameter]
    public Curve Value
    {
        get => _applied.A;
        set => Apply(new CurveRange(value.WithValidRanges(), value.WithValidRanges()));
    }

    private protected CurveRange AppliedRange
    {
        get => _applied;
        set => Apply(new CurveRange(value.A.WithValidRanges(), value.B.WithValidRanges()));
    }

    /// <summary>Called with the curve after every change the user makes to it in the editor.</summary>
    [Parameter]
    public Action<Curve>? ValueChanged { get; set; }

    /// <summary>Whether this control shows a range: both curves with the space between them filled.</summary>
    protected virtual bool IsRange => false;

    /// <summary>Opens the editor under the control, unless it's already open. The user's changes show in the preview straight away.</summary>
    /// <exception cref="ArgumentException">A key's time or value isn't a finite number.</exception>
    public void OpenEditor()
    {
        if (_popup is { IsDeleted: false, IsDeleting: false })
        {
            return;
        }

        var editor = new CurveEditor();
        try
        {
            if (IsRange)
            {
                editor.RangeValue = _applied;
            }
            else
            {
                editor.Value = _applied.A;
            }
        }
        catch
        {
            editor.Delete(true);
            throw;
        }

        var popup = new EditorPopup();
        _popup = popup;
        popup.AddClass("curve-editor-popup");
        var tools = popup.AddChild<Toolbar>();
        tools.AddChild<Label>().Text = IsRange ? "Curve range" : "Curve";
        tools.AddSpacer();
        tools.AddButton("", "close", () => popup.Delete()).Tooltip = "Close";
        _editor = popup.AddChild(editor);
        editor.AcceptsFocus = true;
        if (IsRange)
        {
            editor.RangeValueChanged = value =>
            {
                _applied = value;
                UpdateNavigationBounds(editor);
                OnRangeEdited(value);
            };
        }
        else
        {
            editor.ValueChanged = value =>
            {
                _applied = new CurveRange(value, value);
                UpdateNavigationBounds(editor);
                ValueChanged?.Invoke(value);
            };
        }

        var bounds = UpdateNavigationBounds(editor);
        editor.SetViewBounds(bounds.Position, bounds.Position + bounds.Size);
        popup.CloseWhenParentIsHidden = true;
        popup.SetPositioning(this, Popup.PositionMode.BelowLeft, 4);
        editor.Focus();
    }

    /// <summary>Opens the editor on Enter or Space.</summary>
    public override void OnButtonTyped(ButtonEvent e)
    {
        if (e.Button is "enter" or "space")
        {
            OpenEditor();
            e.StopPropagation = true;
            return;
        }

        base.OnButtonTyped(e);
    }

    /// <inheritdoc/>
    public override void OnDeleted()
    {
        _popup?.Delete(true);
        base.OnDeleted();
    }

    /// <inheritdoc/>
    public override void OnDraw(Painter painter)
    {
        base.OnDraw(painter);
        var rect = new Rect(new Vector2(6, 5) * ScaleToScreen, Box.Rect.Size - (new Vector2(12, 10) * ScaleToScreen));
        if (rect.Width <= 0 || rect.Height <= 0 || !painter.IsRectVisible(rect))
        {
            return;
        }

        UpdatePreview(rect);
        var color = ComputedStyle?.FontColor ?? Color.White;
        using var scope = painter.Scope();
        painter.Clip(rect);
        if (IsRange)
        {
            _areaFill.Draw(painter, _area, color.WithAlpha(0.15f));
        }

        _lowerLine.Draw(painter, _lower, color);
        if (IsRange)
        {
            _upperLine.Draw(painter, _upper, color);
        }
    }

    /// <summary>Opens the editor.</summary>
    protected override void OnClick(MousePanelEvent e)
    {
        e.StopPropagation();
        OpenEditor();
    }

    private protected virtual void OnRangeEdited(CurveRange value)
    {
    }

    private void Apply(CurveRange value)
    {
        _applied = value;
        if (_editor is not { IsDeleted: false, IsDeleting: false } editor)
        {
            return;
        }

        if (IsRange)
        {
            editor.RangeValue = value;
        }
        else
        {
            editor.Value = value.A;
        }

        UpdateNavigationBounds(editor);
    }

    private Rect UpdateNavigationBounds(CurveEditor editor)
    {
        var first = IsRange ? editor.RangeValue.A : editor.Value;
        var second = IsRange ? editor.RangeValue.B : first;
        var min = new Vector2(Math.Min(first.TimeRange.X, second.TimeRange.X), Math.Min(first.ValueRange.X, second.ValueRange.X));
        var max = new Vector2(Math.Max(first.TimeRange.Y, second.TimeRange.Y), Math.Max(first.ValueRange.Y, second.ValueRange.Y));
        foreach (var curve in (Curve[])[first, second])
        {
            foreach (var key in curve.Frames)
            {
                float time = curve.TimeRange.X + (key.Time * (curve.TimeRange.Y - curve.TimeRange.X));
                min.X = Math.Min(min.X, time);
                max.X = Math.Max(max.X, time);
            }
        }

        var bounds = new Rect(min, max - min);
        editor.NavigationBounds = bounds;
        return bounds;
    }

    private void UpdatePreview(Rect rect)
    {
        if (_previewValue is { } cached && cached.Equals(_applied) && _previewRect == rect)
        {
            return;
        }

        _previewValue = _applied;
        _previewRect = rect;
        var first = _applied.A;
        var second = _applied.B;
        float minTime = Math.Min(first.TimeRange.X, second.TimeRange.X);
        float maxTime = Math.Max(first.TimeRange.Y, second.TimeRange.Y);
        float minValue = Math.Min(first.ValueRange.X, second.ValueRange.X);
        float maxValue = Math.Max(first.ValueRange.Y, second.ValueRange.Y);
        for (int i = 0; i < _lower.Length; i++)
        {
            float fraction = i / 64f;
            float time = minTime + ((maxTime - minTime) * fraction);
            float lower = first.Evaluate(time);
            float upper = IsRange ? second.Evaluate(time) : lower;
            _lower[i] = new Vector2(fraction, float.IsFinite(lower) ? lower : minValue);
            _upper[i] = new Vector2(fraction, float.IsFinite(upper) ? upper : minValue);
            minValue = Math.Min(minValue, Math.Min(_lower[i].Y, _upper[i].Y));
            maxValue = Math.Max(maxValue, Math.Max(_lower[i].Y, _upper[i].Y));
        }

        for (int i = 0; i < _lower.Length; i++)
        {
            _area[i] = _lower[i] = Map(_lower[i]);
            _area[^(i + 1)] = _upper[i] = Map(_upper[i]);
        }

        Vector2 Map(Vector2 point) => rect.Position + new Vector2(point.X * rect.Width, (1 - ((point.Y - minValue) / Math.Max(0.000001f, maxValue - minValue))) * rect.Height);
    }

    private sealed class EditorPopup : Popup
    {
        protected override void OnEscape(PanelEvent e)
        {
            Delete();
            e.StopPropagation();
        }
    }
}
