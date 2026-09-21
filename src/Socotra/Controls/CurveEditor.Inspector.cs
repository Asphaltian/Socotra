using System.Globalization;

namespace Socotra;

public partial class CurveEditor
{
    private sealed class CurveKeyInspector : Toolbar
    {
        private readonly CurveEditor _editor;
        private readonly Label _title;
        private readonly Button _tangents;
        private readonly Button _smooth;
        private readonly NumberEntry _time;
        private readonly NumberEntry _value;

        public CurveKeyInspector(CurveEditor editor)
        {
            _editor = editor;
            AddClass("curve-inspector");
            Vertical = true;
            _title = Add.Label("Key", "curve-inspector-title");
            _time = AddField("Time");
            _value = AddField("Value");
            _time.Tooltip = "Key time; moves selected keys together";
            _value.Tooltip = "Key value; moves selected keys together";
            _time.OnTextEdited = text => editor.EditNumber(text, true);
            _value.OnTextEdited = text => editor.EditNumber(text, false);
            foreach (var entry in (NumberEntry[])[_time, _value])
            {
                entry.AddEventListener("onfocus", editor.BeginEdit);
                entry.AddEventListener("onblur", () =>
                {
                    editor.EndEdit();
                    editor.Sync();
                });
            }

            AddSeparator();
            var menu = new Menu();
            foreach (var mode in Enum.GetValues<Curve.HandleMode>())
            {
                menu.AddOption(mode.ToString(), null, () => editor.SetSelectedMode(mode));
            }

            _tangents = AddMenu("Tangents", null, menu);
            _tangents.AddClass("curve-tangent-mode");
            _tangents.Tooltip = "Interpolation of selected keys";
            _smooth = AddButton("Smooth", null, editor.SmoothSelected);
            _smooth.Tooltip = "Smooth selected tangents without overshoot";
        }

        public void Sync()
        {
            bool selected = _editor._selection.Count > 0;
            _title.Text = selected ? $"{_editor.ChannelName(_editor.ActiveCurve)} · Key {_editor.SelectedIndex + 1}" : "Select a key";
            _time.Disabled = _value.Disabled = _tangents.Disabled = _smooth.Disabled = !selected;
            var modes = _editor._selection.Select(x => _editor._curves[x.CurveIndex].Frames[x.KeyIndex].Mode).Distinct().ToArray();
            _tangents.Text = modes.Length == 1 ? modes[0].ToString() : "Tangents";
            if (_editor.SelectedIndex < 0)
            {
                _time.Text = "";
                _value.Text = "";
                return;
            }

            var curve = _editor.ActiveCurveValue;
            var key = curve.Frames[_editor.SelectedIndex];
            if (!_time.HasFocus)
            {
                _time.Text = (curve.TimeRange.X + (key.Time * (curve.TimeRange.Y - curve.TimeRange.X))).ToString("0.###", CultureInfo.InvariantCulture);
            }

            if (!_value.HasFocus)
            {
                _value.Text = (curve.ValueRange.X + (key.Value * (curve.ValueRange.Y - curve.ValueRange.X))).ToString("0.###", CultureInfo.InvariantCulture);
            }
        }

        private NumberEntry AddField(string label)
        {
            var field = Add.Panel("curve-key-field");
            field.Add.Label(label);
            return field.AddChild<NumberEntry>();
        }
    }
}
