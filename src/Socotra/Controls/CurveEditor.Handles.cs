namespace Socotra;

public partial class CurveEditor
{
    private sealed partial class CurveCanvas
    {
        private readonly Panel _handles;
        private readonly Dictionary<CurveKey, CurveKeyPanel> _keys = [];
        private CurveTangentPanel? _incoming;
        private CurveTangentPanel? _outgoing;

        private void SyncHandles()
        {
            _handles.Style.Left = Plot.Left / ScaleToScreen;
            _handles.Style.Top = Plot.Top / ScaleToScreen;
            _handles.Style.Width = Plot.Width / ScaleToScreen;
            _handles.Style.Height = Plot.Height / ScaleToScreen;

            var keys = _editor.AllKeys().ToHashSet();
            foreach (var id in _keys.Keys.Where(x => !keys.Contains(x)).ToArray())
            {
                _keys[id].Delete(true);
                _keys.Remove(id);
            }

            foreach (var id in keys)
            {
                if (!_keys.TryGetValue(id, out var panel))
                {
                    panel = _handles.AddChild(new CurveKeyPanel(this, id));
                    _keys.Add(id, panel);
                }

                panel.Position = _editor.KeyPoint(id);
                panel.Style.ZIndex = id.CurveIndex == _editor.ActiveCurve ? 2 : 1;
            }

            _incoming ??= _handles.AddChild(new CurveTangentPanel(this, true));
            _outgoing ??= _handles.AddChild(new CurveTangentPanel(this, false));
            _incoming.Style.Display = _outgoing.Style.Display = HasTangents ? DisplayMode.Flex : DisplayMode.None;
            if (HasTangents)
            {
                _incoming.Position = ScreenToCanvas(TangentPosition(true));
                _outgoing.Position = ScreenToCanvas(TangentPosition(false));
            }
        }

        private sealed class CurveKeyPanel : Handle
        {
            private readonly CurveCanvas _canvas;
            private bool _snap;
            private EditorState? _edit;
            private Vector2 _origin;
            private Vector2 _minimumDelta;
            private Vector2 _maximumDelta;

            public CurveKeyPanel(CurveCanvas canvas, CurveKey key)
            {
                _canvas = canvas;
                Key = key;
                AddClass("curve-key");
                Style.Width = Style.Height = 18;
            }

            public CurveKey Key { get; }

            private CurveEditor Editor => _canvas._editor;

            public override void OnDraw(Painter painter)
            {
                using var scope = painter.Scope();
                bool selected = Editor._selection.Contains(Key);
                float radius = (selected || HasHovered ? 6 : 5) * ScaleToScreen;
                var center = Box.Rect.Size * 0.5f;
                painter.Stroke = Stroke.Solid(_canvas.ComputedStyle?.BackgroundColor ?? Color.Black, 2);
                painter.Fill = selected || HasHovered ? Color.White : Editor.ChannelColor(Key.CurveIndex);
                painter.Polygon([center + new Vector2(0, -radius), center + new Vector2(radius, 0), center + new Vector2(0, radius), center + new Vector2(-radius, 0)]);
            }

            protected override bool OnDragStart(MousePanelEvent e)
            {
                var selected = Editor._selection.ToList();
                if (e.HasShift && e.HasCtrl && selected.Contains(Key))
                {
                    selected.Remove(Key);
                    Editor.SelectKeys(selected);
                    return false;
                }

                if (!e.HasShift && !selected.Contains(Key))
                {
                    selected.Clear();
                }

                selected.Remove(Key);
                selected.Add(Key);
                Editor.SelectKeys(selected);
                _snap = e.HasCtrl;
                Editor.BeginEdit();
                _edit = Editor._currentEdit;
                if (_edit is null || !Editor.IsCurrentDrag(_edit))
                {
                    return false;
                }

                _origin = Editor.KeyPoint(new CurveKey(_edit.Active, _edit.Primary), _edit.Curves);
                var a = _canvas.ScreenToCanvas(_canvas.Plot.Position);
                var b = _canvas.ScreenToCanvas(_canvas.Plot.Position + _canvas.Plot.Size);
                var min = Vector2.Min(a, b);
                var max = Vector2.Max(a, b);
                _minimumDelta = new Vector2(float.NegativeInfinity);
                _maximumDelta = new Vector2(float.PositiveInfinity);
                foreach (var id in Editor._selection)
                {
                    var point = Editor.KeyPoint(id);
                    _minimumDelta = Vector2.Max(_minimumDelta, Vector2.Min(Vector2.Zero, min - point));
                    _maximumDelta = Vector2.Min(_maximumDelta, Vector2.Max(Vector2.Zero, max - point));
                }

                return true;
            }

            protected override void OnDragMove(Vector2 delta)
            {
                if (!Editor.IsCurrentDrag(_edit))
                {
                    FinishDrag();
                    return;
                }

                if (_snap || Editor.SnapToGrid)
                {
                    delta = Editor.SnapPoint(_origin + delta) - _origin;
                }

                if (DragAxis == Axis.Horizontal)
                {
                    delta.Y = 0;
                }
                else if (DragAxis == Axis.Vertical)
                {
                    delta.X = 0;
                }

                Editor.MoveSelectionFromStart(Vector2.Max(_minimumDelta, Vector2.Min(_maximumDelta, delta)));
            }

            protected override void OnDragFinish(bool canceled)
            {
                if (ReferenceEquals(Editor._currentEdit, _edit))
                {
                    Editor.EndEdit(canceled && Editor.IsCurrentDrag(_edit));
                }

                _edit = null;
            }

            protected override void OnDoubleClick(MousePanelEvent e) => e.StopPropagation();
        }

        private sealed class CurveTangentPanel : Handle
        {
            private readonly CurveCanvas _canvas;
            private readonly bool _incoming;
            private EditorState? _edit;

            public CurveTangentPanel(CurveCanvas canvas, bool incoming)
            {
                _canvas = canvas;
                _incoming = incoming;
                LockAxisWithShift = false;
                AddClass("curve-tangent");
                Style.Width = Style.Height = 18;
            }

            private CurveEditor Editor => _canvas._editor;

            public override void OnDraw(Painter painter)
            {
                using var scope = painter.Scope();
                painter.Stroke = Stroke.None;
                painter.Fill = HasHovered ? Color.White : Editor.ChannelColor(Editor.ActiveCurve);
                painter.Circle(Box.Rect.Size * 0.5f, 3 * ScaleToScreen);
            }

            protected override bool OnDragStart(MousePanelEvent e)
            {
                Editor.BeginEdit();
                _edit = Editor._currentEdit;
                return Editor.IsCurrentDrag(_edit);
            }

            protected override void OnDragMove(Vector2 delta)
            {
                if (!Editor.IsCurrentDrag(_edit))
                {
                    FinishDrag();
                    return;
                }

                var point = Editor.FromDisplay(Editor.ActiveCurveValue, _canvas.ScreenToCanvas(_canvas.MousePosition));
                var key = Editor.ActiveCurveValue.Frames[Editor.SelectedIndex];
                float dx = _incoming ? Math.Max(0.001f, key.Time - point.X) : Math.Max(0.001f, point.X - key.Time);
                Editor.SetTangent(Editor.SelectedIndex, _incoming, Math.Clamp((point.Y - key.Value) / dx, -10000, 10000));
            }

            protected override void OnDragFinish(bool canceled)
            {
                if (ReferenceEquals(Editor._currentEdit, _edit))
                {
                    Editor.EndEdit(canceled && Editor.IsCurrentDrag(_edit));
                }

                _edit = null;
            }

            protected override void OnDoubleClick(MousePanelEvent e) => e.StopPropagation();
        }
    }
}
