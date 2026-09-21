namespace Socotra;

public partial class CurveEditor
{
    /// <summary>
    /// Where the editor keeps the presets the user saves. Every curve editor shares <see cref="CurvePresetStore.Shared"/>
    /// unless you set another. See <see cref="CurvePresetStore"/> to keep them between runs.
    /// </summary>
    public CurvePresetStore PresetStore { get; set; } = CurvePresetStore.Shared;

    /// <summary>The presets the user has saved in <see cref="PresetStore"/>.</summary>
    public IReadOnlyList<Curve> SavedPresets => PresetStore.Presets;

    /// <summary>Saves the active curve, ranges included, as a new preset.</summary>
    public void SavePreset() => PresetStore.Add(ActiveCurveValue);

    /// <summary>Replaces saved preset <paramref name="index"/> with the active curve.</summary>
    public void ReplacePreset(int index) => PresetStore.Replace(index, ActiveCurveValue);

    /// <summary>Deletes saved preset <paramref name="index"/>.</summary>
    public void DeletePreset(int index) => PresetStore.Remove(index);

    private sealed class CurvePresets : Panel
    {
        private static readonly (string Name, Curve Curve)[] BuiltInPresets =
        [
            ("Flat", 0f),
            ("Ease", Curve.Ease),
            ("Ease reversed", Curve.Ease.Reverse()),
            ("Linear", Curve.Linear),
            ("Linear reversed", Curve.Linear.Reverse()),
            ("Ease in", Curve.EaseIn),
            ("Ease in reversed", Curve.EaseIn.Reverse()),
            ("Ease out", Curve.EaseOut),
            ("Ease out reversed", Curve.EaseOut.Reverse()),
        ];

        private readonly CurveEditor _editor;
        private CurvePresetStore? _store;
        private int _version;

        public CurvePresets(CurveEditor editor)
        {
            _editor = editor;
            AddClass("curve-presets");
            Rebuild();
        }

        public override void Tick()
        {
            base.Tick();
            if (_store != _editor.PresetStore || _version != _store.Version)
            {
                Rebuild();
            }
        }

        private void Rebuild()
        {
            _store = _editor.PresetStore;
            _version = _store.Version;
            DeleteChildren(true);
            foreach (var (name, curve) in BuiltInPresets)
            {
                AddChild(new PresetButton(name, curve, () => _editor.ApplyPreset(curve)));
            }

            var saved = _store.Presets;
            for (int i = 0; i < saved.Count; i++)
            {
                var index = i;
                var curve = saved[i];
                var button = AddChild(new PresetButton($"Saved curve {i + 1} - Right-click for options", curve, () => _editor.ApplyPreset(curve)));
                button.AddClass("saved-curve-preset");
                button.BuildContextMenu = menu =>
                {
                    menu.AddOption("Apply with ranges", "open_in_full", () => _editor.ApplyPreset(curve, true));
                    menu.AddOption("Replace with current curve", "refresh", () => _editor.ReplacePreset(index));
                    menu.AddOption("Delete preset", "delete", () => _editor.DeletePreset(index));
                };
            }

            var add = AddChild(new Button("", "add", _editor.SavePreset));
            add.AddClass("curve-preset");
            add.Tooltip = "Save current curve as a preset";
        }
    }

    private sealed class PresetButton : Button
    {
        private readonly Curve _curve;
        private readonly Painter.CachedLine _line = new();
        private readonly Vector2[] _points = new Vector2[25];
        private Rect _previewRect;
        private Menu? _menu;

        public PresetButton(string name, Curve curve, Action apply)
            : base("", null, apply)
        {
            _curve = curve;
            Tooltip = name;
            AddClass("curve-preset");
        }

        public Action<Menu>? BuildContextMenu { get; set; }

        public override void OnDeleted()
        {
            _menu?.Delete(true);
            base.OnDeleted();
        }

        public override void OnDraw(Painter painter)
        {
            base.OnDraw(painter);
            var inset = 7 * ScaleToScreen;
            var rect = new Rect(new Vector2(inset), Box.Rect.Size - new Vector2(inset * 2));
            if (rect.Width <= 0 || rect.Height <= 0 || !painter.IsRectVisible(rect))
            {
                return;
            }

            if (_previewRect != rect)
            {
                _previewRect = rect;
                for (int i = 0; i < _points.Length; i++)
                {
                    float time = i / 24f;
                    _points[i] = rect.Position + new Vector2(time * rect.Width, (1 - _curve.EvaluateDelta(time)) * rect.Height);
                }
            }

            _line.Draw(painter, _points, ComputedStyle?.FontColor ?? Color.White);
        }

        protected override void OnRightClick(MousePanelEvent e)
        {
            if (BuildContextMenu is null)
            {
                return;
            }

            e.StopPropagation();
            _menu?.Delete(true);
            _menu = new Menu();
            BuildContextMenu(_menu);
            _menu.Closed += menu => menu.Delete(true);
            _menu.Open(this, Popup.PositionMode.UnderMouse);
        }
    }
}
