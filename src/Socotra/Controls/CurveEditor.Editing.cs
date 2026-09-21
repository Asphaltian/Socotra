namespace Socotra;

public partial class CurveEditor
{
    /// <summary>Makes curve <paramref name="index"/> the one the user works on, and deselects every key. The curves and the view don't change.</summary>
    public void SelectCurve(int index)
    {
        if (index < 0 || index >= _curves.Length)
        {
            return;
        }

        _canvas.Finish();
        ActiveCurve = index;
        SelectKeys([]);
    }

    /// <summary>Selects key <paramref name="index"/> of the active curve on its own, or deselects everything if there's no such key.</summary>
    public void SelectKey(int index) => SelectKeys(index >= 0 && index < ActiveCurveValue.Length ? [new CurveKey(ActiveCurve, index)] : []);

    /// <summary>Selects <paramref name="keys"/>, on any curves. Keys that don't exist are left out. The last one becomes <see cref="SelectedIndex"/> and its curve the active one.</summary>
    public void SelectKeys(IEnumerable<CurveKey> keys)
    {
        var valid = keys.Where(x => x.CurveIndex >= 0 && x.CurveIndex < _curves.Length && x.KeyIndex >= 0 && x.KeyIndex < _curves[x.CurveIndex].Length).Distinct().ToArray();
        _selection.Clear();
        _selection.UnionWith(valid);
        SelectedIndex = -1;
        if (valid.Length > 0)
        {
            ActiveCurve = valid[^1].CurveIndex;
            SelectedIndex = valid[^1].KeyIndex;
        }

        Sync();
    }

    /// <summary>Selects every key on every curve.</summary>
    public void SelectAll() => SelectKeys(AllKeys());

    /// <summary>Adds a key to the active curve at <paramref name="time"/> and <paramref name="value"/>, both from 0 to 1, and selects it. If there's already a key at that time, that one is selected instead.</summary>
    public void AddKey(float time, float value) => AddKey(new Curve.Frame(time, value));

    /// <summary>Adds a key to the active curve at <paramref name="time"/>, from 0 to 1, without changing the curve's shape.</summary>
    public void InsertKey(float time)
    {
        if (!float.IsFinite(time))
        {
            return;
        }

        time = Math.Clamp(time, 0, 1);
        var curve = ActiveCurveValue;
        var key = new Curve.Frame(time, curve.EvaluateDelta(time));
        if (curve.Length > 0 && time < curve.Frames[0].Time)
        {
            key.Mode = Curve.HandleMode.Linear;
        }

        if (curve.Length > 0 && time > curve.Frames[^1].Time)
        {
            var keys = curve.Frames.ToArray();
            if (keys[^1].Mode is Curve.HandleMode.Mirrored or Curve.HandleMode.Split)
            {
                keys[^1].Out = 0;
                keys[^1].Mode = Curve.HandleMode.Split;
            }

            curve = curve.WithFrames(keys);
        }

        for (int i = 0; i < curve.Length - 1; i++)
        {
            var start = curve.Frames[i];
            var end = curve.Frames[i + 1];
            if (time <= start.Time || time >= end.Time)
            {
                continue;
            }

            if (start.Mode is Curve.HandleMode.Linear or Curve.HandleMode.Stepped)
            {
                key.Mode = start.Mode;
            }
            else
            {
                float timeSpan = end.Time - start.Time;
                float fraction = (time - start.Time) / timeSpan;
                float outgoing = start.Mode == Curve.HandleMode.Flat ? 0 : start.Out;
                float incoming = end.Mode == Curve.HandleMode.Flat ? 0 : -end.In;
                float valueDelta = end.Value - start.Value;
                float slope = ((3 * fraction * fraction * (((incoming + outgoing) * timeSpan) - (2 * valueDelta)))
                    + (2 * fraction * (((-incoming - (2 * outgoing)) * timeSpan) + (3 * valueDelta))) + (outgoing * timeSpan)) / timeSpan;
                key.Mode = Curve.HandleMode.Split;
                key.In = -slope;
                key.Out = slope;
            }

            break;
        }

        AddKey(key, curve);
    }

    /// <summary>Moves key <paramref name="index"/> of the active curve to <paramref name="time"/> and <paramref name="value"/>, both from 0 to 1. It can't pass the keys either side of it.</summary>
    public void MoveKey(int index, float time, float value)
    {
        if (index < 0 || index >= ActiveCurveValue.Length || !float.IsFinite(time) || !float.IsFinite(value))
        {
            return;
        }

        var keys = ActiveCurveValue.Frames.ToArray();
        float min = index == 0 ? Math.Min(0, keys[index].Time) : Math.Min(keys[index].Time, keys[index - 1].Time + KeySpacing);
        float max = index == keys.Length - 1 ? Math.Max(1, keys[index].Time) : Math.Max(keys[index].Time, keys[index + 1].Time - KeySpacing);
        keys[index].Time = min <= max ? Math.Clamp(time, min, max) : keys[index].Time;
        keys[index].Value = value;
        ChangeCurve(ActiveCurveValue.WithFrames(keys), index);
    }

    /// <summary>Moves every selected key by <paramref name="delta"/>, in the first curve's 0 to 1 space, keeping their spacing. Keys can't pass unselected neighbors.</summary>
    public void MoveSelection(Vector2 delta) => MoveSelection(_curves, _selection, delta);

    /// <summary>Selects key <paramref name="index"/> of the active curve and sets how the curve leaves it.</summary>
    public void SetKeyMode(int index, Curve.HandleMode mode)
    {
        if (index < 0 || index >= ActiveCurveValue.Length || !Enum.IsDefined(mode))
        {
            return;
        }

        SelectKey(index);
        SetSelectedMode(mode);
    }

    /// <summary>Sets how the curve leaves every selected key, on all curves, as one change.</summary>
    public void SetSelectedMode(Curve.HandleMode mode)
    {
        if (!Enum.IsDefined(mode))
        {
            return;
        }

        EditSelected((keys, index) =>
        {
            keys[index].Mode = mode;
            if (mode == Curve.HandleMode.Mirrored)
            {
                keys[index].In = -keys[index].Out;
            }
        });
    }

    /// <summary>Gives the selected keys smooth tangents that don't overshoot their neighbors. The keys become <see cref="Curve.HandleMode.Mirrored"/>.</summary>
    public void SmoothSelected() => EditSelected((keys, index) =>
    {
        float slope = 0;
        if (keys.Length > 1)
        {
            if (index == 0)
            {
                slope = Slope(0, 1);
            }
            else if (index == keys.Length - 1)
            {
                slope = Slope(index - 1, index);
            }
            else
            {
                float left = Slope(index - 1, index);
                float right = Slope(index, index + 1);
                if (left * right > 0)
                {
                    float a = keys[index].Time - keys[index - 1].Time;
                    float b = keys[index + 1].Time - keys[index].Time;
                    slope = 3 * (a + b) / ((((2 * b) + a) / left) + ((b + (2 * a)) / right));
                }
            }
        }

        keys[index].Mode = Curve.HandleMode.Mirrored;
        keys[index].In = -slope;
        keys[index].Out = slope;

        float Slope(int a, int b) => (keys[b].Value - keys[a].Value) / (keys[b].Time - keys[a].Time);
    });

    /// <summary>Sets the incoming or outgoing slope of key <paramref name="index"/> on the active curve. It only applies to <see cref="Curve.HandleMode.Mirrored"/> and <see cref="Curve.HandleMode.Split"/> keys; a mirrored key's other side follows.</summary>
    public void SetTangent(int index, bool incoming, float slope)
    {
        if (index < 0 || index >= ActiveCurveValue.Length || !float.IsFinite(slope))
        {
            return;
        }

        var keys = ActiveCurveValue.Frames.ToArray();
        if (keys[index].Mode is not (Curve.HandleMode.Mirrored or Curve.HandleMode.Split))
        {
            return;
        }

        if (incoming)
        {
            keys[index].In = slope;
        }
        else
        {
            keys[index].Out = slope;
        }

        if (keys[index].Mode == Curve.HandleMode.Mirrored)
        {
            if (incoming)
            {
                keys[index].Out = -slope;
            }
            else
            {
                keys[index].In = -slope;
            }
        }

        var curves = (Curve[])_curves.Clone();
        curves[ActiveCurve] = ActiveCurveValue.WithFrames(keys);
        Change(curves);
    }

    /// <summary>Deletes the selected keys. Each curve keeps at least one key.</summary>
    public void RemoveSelected()
    {
        var curves = (Curve[])_curves.Clone();
        foreach (var group in _selection.GroupBy(x => x.CurveIndex))
        {
            var keys = curves[group.Key].Frames.ToList();
            foreach (var id in group.OrderByDescending(x => x.KeyIndex))
            {
                if (keys.Count > 1)
                {
                    keys.RemoveAt(id.KeyIndex);
                }
            }

            curves[group.Key] = curves[group.Key].WithFrames(keys);
        }

        Change(curves, []);
    }

    /// <summary>Replaces the active curve's keys with <paramref name="preset"/>'s. Pass <paramref name="includeRanges"/> to take its time and value ranges too.</summary>
    public void ApplyPreset(Curve preset, bool includeRanges = false) =>
        ChangeCurve(includeRanges ? Normalize(preset) : ActiveCurveValue.WithFrames(Normalize(preset).Frames), -1);

    private IEnumerable<CurveKey> AllKeys()
    {
        for (int c = 0; c < _curves.Length; c++)
        {
            for (int k = 0; k < _curves[c].Length; k++)
            {
                yield return new CurveKey(c, k);
            }
        }
    }

    private Vector2 ToDisplay(Curve curve, Vector2 point) => Remap(curve, _curves[0], point);

    private Vector2 FromDisplay(Curve curve, Vector2 point) => Remap(_curves[0], curve, point);

    private Vector2 KeyPoint(CurveKey id, Curve[]? curves = null)
    {
        var curve = (curves ?? _curves)[id.CurveIndex];
        var key = curve.Frames[id.KeyIndex];
        return ToDisplay(curve, new Vector2(key.Time, key.Value));
    }

    private void AddKey(Curve.Frame key, Curve? source = null)
    {
        if (!float.IsFinite(key.Time) || !float.IsFinite(key.Value))
        {
            return;
        }

        var curve = source ?? ActiveCurveValue;
        key.Time = Math.Clamp(key.Time, 0, 1);
        var keys = curve.Frames.ToList();
        int existing = keys.FindIndex(x => MathF.Abs(x.Time - key.Time) < KeySpacing);
        if (existing >= 0)
        {
            SelectKey(existing);
            return;
        }

        keys.Add(key);
        keys.Sort();
        ChangeCurve(curve.WithFrames(keys), keys.FindIndex(x => x.Time == key.Time));
    }

    private bool IsCurrentDrag(EditorState? edit) =>
        edit is not null && ReferenceEquals(edit, _currentEdit)
        && edit.Active == ActiveCurve && edit.Primary == SelectedIndex && SelectedIndex >= 0
        && _selection.SetEquals(edit.Selection) && edit.Curves.Length == _curves.Length
        && !edit.Curves.Where((curve, i) => curve.Length != _curves[i].Length).Any();

    private void MoveSelectionFromStart(Vector2 delta)
    {
        if (_currentEdit is not null)
        {
            MoveSelection(_currentEdit.Curves, [.. _currentEdit.Selection], delta);
        }
    }

    private void MoveSelection(Curve[] source, HashSet<CurveKey> selection, Vector2 delta)
    {
        if (selection.Count == 0 || !Painter.IsFinite(delta))
        {
            return;
        }

        float minDelta = float.NegativeInfinity;
        float maxDelta = float.PositiveInfinity;
        foreach (var id in selection)
        {
            var curve = source[id.CurveIndex];
            var keys = curve.Frames;
            var key = keys[id.KeyIndex];
            float min = Math.Min(0, key.Time);
            float max = Math.Max(1, key.Time);
            if (id.KeyIndex > 0 && !selection.Contains(id with { KeyIndex = id.KeyIndex - 1 }))
            {
                min = Math.Min(key.Time, keys[id.KeyIndex - 1].Time + KeySpacing);
            }

            if (id.KeyIndex < keys.Length - 1 && !selection.Contains(id with { KeyIndex = id.KeyIndex + 1 }))
            {
                max = Math.Max(key.Time, keys[id.KeyIndex + 1].Time - KeySpacing);
            }

            float scale = UnitsOf(curve).Span.X / Units().Span.X;
            minDelta = Math.Max(minDelta, (min - key.Time) * scale);
            maxDelta = Math.Min(maxDelta, (max - key.Time) * scale);
        }

        delta.X = minDelta <= maxDelta ? Math.Clamp(delta.X, minDelta, maxDelta) : 0;
        var result = (Curve[])source.Clone();
        foreach (var group in selection.GroupBy(x => x.CurveIndex))
        {
            var curve = source[group.Key];
            var keys = curve.Frames.ToArray();
            foreach (var id in group)
            {
                var p = FromDisplay(curve, KeyPoint(id, source) + delta);
                keys[id.KeyIndex].Time = delta.X == 0 ? keys[id.KeyIndex].Time : p.X;
                keys[id.KeyIndex].Value = delta.Y == 0 ? keys[id.KeyIndex].Value : p.Y;
            }

            result[group.Key] = curve.WithFrames(keys);
        }

        Change(result);
    }

    private void EditSelected(Action<Curve.Frame[], int> edit)
    {
        var curves = (Curve[])_curves.Clone();
        foreach (var group in _selection.GroupBy(x => x.CurveIndex))
        {
            var keys = curves[group.Key].Frames.ToArray();
            foreach (var id in group)
            {
                edit(keys, id.KeyIndex);
            }

            curves[group.Key] = curves[group.Key].WithFrames(keys);
        }

        Change(curves);
    }
}
