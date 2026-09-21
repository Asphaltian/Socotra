namespace Socotra;

public partial class CurveEditor
{
    /// <summary>Zooms to fit every curve, like pressing Home.</summary>
    public void FitView() => _canvas.Fit();

    /// <summary>Zooms to fit the selected keys, or every curve when nothing is selected, like pressing F.</summary>
    public void FitSelection() => _canvas.Fit(true);

    private sealed partial class CurveCanvas : GraphPanel
    {
        private readonly CurveEditor _editor;
        private Vector2 _dragStartMouse;
        private CurveKey[] _additiveBoxSelection = [];
        private CurveKey[] _selectionBeforeBoxDrag = [];
        private Menu? _menu;

        public CurveCanvas(CurveEditor editor)
        {
            _editor = editor;
            ConstrainVerticalNavigation = false;
            MinimumViewSpan = 0.001f;
            MaximumViewSpan = 1000;
            SetView(Vector2.Zero, Vector2.One);
            EditableAxes = true;
            AxisBoundEdited += (axis, maximum, value) => _editor.EditAxisRange(value, axis == Axis.Horizontal, maximum);
            AxisEditStarted += _editor.BeginEdit;
            AxisEditFinished += () =>
            {
                _editor.EndEdit();
                _editor.Sync();
            };
            BoxSelectionEnabled = true;
            BoxSelectionStarted += additive =>
            {
                _selectionBeforeBoxDrag = [.. _editor._selection];
                _additiveBoxSelection = additive ? _selectionBeforeBoxDrag : [];
            };
            BoxSelectionChanged += rect => _editor.SelectKeys(_additiveBoxSelection.Concat(_editor.AllKeys().Where(id => rect.IsInside(_editor.KeyPoint(id)))));
            BoxSelectionFinished += canceled =>
            {
                if (canceled)
                {
                    _editor.SelectKeys(_selectionBeforeBoxDrag);
                }
            };
            _handles = AddChild<Panel>();
            _handles.Style.Position = PositionMode.Absolute;
            _handles.Style.Overflow = OverflowMode.Hidden;
            _handles.Style.PointerEvents = PointerEvents.None;
            AddClass("curve-canvas");
            AcceptsFocus = true;
            CanDragScroll = false;
        }

        private CurveKey PrimaryKey => new(_editor.ActiveCurve, _editor.SelectedIndex);

        private bool HasTangents => _editor.SelectedIndex >= 0
            && _editor.ActiveCurveValue.Frames[_editor.SelectedIndex].Mode is Curve.HandleMode.Mirrored or Curve.HandleMode.Split;

        public void Fit(bool selection = false)
        {
            var points = new List<Vector2>();
            if (selection)
            {
                points.AddRange(_editor._selection.Select(x => _editor.KeyPoint(x)));
            }

            if (points.Count == 0)
            {
                foreach (var curve in _editor._curves)
                {
                    points.Add(_editor.ToDisplay(curve, new Vector2(0, curve.EvaluateDelta(0))));
                    points.Add(_editor.ToDisplay(curve, new Vector2(1, curve.EvaluateDelta(1))));
                    foreach (var key in curve.Frames)
                    {
                        points.Add(_editor.ToDisplay(curve, new Vector2(key.Time, key.Value)));
                    }

                    AddCurveExtrema(points, curve);
                }
            }

            points.RemoveAll(p => !Painter.IsFinite(p));
            if (points.Count == 0)
            {
                return;
            }

            var min = new Vector2(points.Min(p => p.X), points.Min(p => p.Y));
            var max = new Vector2(points.Max(p => p.X), points.Max(p => p.Y));
            FitBounds(new Rect(min, max - min), new Vector2(0.06f, 0.12f), new Vector2(0.05f, 0.1f));
        }

        public void Finish(bool cancel = false)
        {
            foreach (var handle in _handles.Children.OfType<Handle>().ToArray())
            {
                handle.FinishDrag(cancel);
            }

            FinishBoxSelection(cancel);
            _editor.EndEdit(cancel);
        }

        protected override void OnEscape(PanelEvent e)
        {
            if (_editor._currentEdit is null && !IsBoxSelecting)
            {
                base.OnEscape(e);
                return;
            }

            Finish(true);
            e.StopPropagation();
        }

        public override void OnButtonTyped(ButtonEvent e)
        {
            if (e.HasCtrl && e.Button == "a")
            {
                _editor.SelectAll();
            }
            else if (e.HasCtrl && e.Button == "z")
            {
                if (e.HasShift)
                {
                    _editor.Redo();
                }
                else
                {
                    _editor.Undo();
                }
            }
            else if (e.HasCtrl && e.Button == "y")
            {
                _editor.Redo();
            }
            else if (e.Button is "delete" or "backspace")
            {
                _editor.RemoveSelected();
            }
            else if (e.Button == "f")
            {
                Fit(true);
            }
            else if (e.Button == "home")
            {
                Fit();
            }
            else if (_editor.SelectedIndex >= 0 && e.Button is "left" or "right" or "up" or "down")
            {
                float step = e.HasShift ? 0.1f : 0.01f;
                _editor.MoveSelection(new Vector2(
                    e.Button == "left" ? -step : e.Button == "right" ? step : 0,
                    e.Button == "down" ? -step : e.Button == "up" ? step : 0));
            }
            else
            {
                base.OnButtonTyped(e);
                return;
            }

            e.StopPropagation = true;
        }

        public override void Tick()
        {
            base.Tick();
            _editor._tools.Sync();
            SyncHandles();
        }

        public override void OnDeleted()
        {
            Finish();
            _menu?.Delete(true);
            base.OnDeleted();
        }

        protected override void OnMouseDown(MousePanelEvent e)
        {
            _dragStartMouse = MousePosition;
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MousePanelEvent e)
        {
            if (IsBoxSelecting && e.Button == "mouseleft" && (MousePosition - _dragStartMouse).Length() < 3 * ScaleToScreen)
            {
                int curve = HitCurve(MousePosition);
                if (curve >= 0 && _editor._selection.Count == 0)
                {
                    _editor.ActiveCurve = curve;
                    _editor.Sync();
                }
            }

            base.OnMouseUp(e);
        }

        protected override void OnDoubleClick(MousePanelEvent e)
        {
            e.StopPropagation();
            Finish();
            if (!Plot.IsInside(MousePosition))
            {
                return;
            }

            int curve = HitCurve(MousePosition);
            if (curve >= 0)
            {
                _editor.SelectCurve(curve);
            }

            var display = ScreenToCanvas(MousePosition);
            if (_editor.SnapToGrid)
            {
                display = _editor.SnapPoint(display);
            }

            var p = _editor.FromDisplay(_editor.ActiveCurveValue, display);
            if (curve >= 0)
            {
                _editor.InsertKey(p.X);
            }
            else
            {
                _editor.AddKey(p.X, p.Y);
            }
        }

        protected override void OnRightClick(MousePanelEvent e)
        {
            e.StopPropagation();
            Finish();
            if (e.Target?.AncestorsAndSelf.OfType<CurveKeyPanel>().FirstOrDefault()?.Key is { } key)
            {
                if (!_editor._selection.Contains(key))
                {
                    _editor.SelectKeys([key]);
                }
            }
            else
            {
                var curve = HitCurve(MousePosition);
                if (curve >= 0)
                {
                    _editor.SelectCurve(curve);
                }
            }

            var point = _editor.FromDisplay(_editor.ActiveCurveValue, ScreenToCanvas(MousePosition));
            _menu?.Delete(true);
            var menu = new Menu();
            _menu = menu;
            if (_editor._selection.Count > 0)
            {
                foreach (var mode in Enum.GetValues<Curve.HandleMode>())
                {
                    menu.AddOption(mode.ToString(), null, () => _editor.SetSelectedMode(mode));
                }

                menu.AddOption("Smooth tangents", "timeline", _editor.SmoothSelected);
                menu.AddOption("Delete selected keys", "delete", _editor.RemoveSelected);
            }

            menu.AddOption("Insert key on curve", "add", () => _editor.InsertKey(point.X));
            menu.AddOption("Frame selection", "center_focus_strong", () => Fit(true));
            menu.AddOption("Fit all curves", "fit_screen", () => Fit());
            menu.Closed += _ =>
            {
                if (_menu == menu)
                {
                    _menu = null;
                }

                menu.Delete(true);
            };
            menu.Open(this, Popup.PositionMode.UnderMouse);
        }

        private Vector2 KeyPosition(CurveKey id) => CanvasToScreen(_editor.KeyPoint(id));

        private void AddCurveExtrema(List<Vector2> points, Curve curve)
        {
            for (int i = 0; i < curve.Length - 1; i++)
            {
                var start = curve.Frames[i];
                var end = curve.Frames[i + 1];
                if (start.Mode is Curve.HandleMode.Linear or Curve.HandleMode.Stepped)
                {
                    continue;
                }

                float timeSpan = end.Time - start.Time;
                float valueDelta = end.Value - start.Value;
                float outgoingSlope = start.Mode == Curve.HandleMode.Flat ? 0 : start.Out;
                float incomingSlope = end.Mode == Curve.HandleMode.Flat ? 0 : -end.In;
                float quadratic = 3 * (((incomingSlope + outgoingSlope) * timeSpan) - (2 * valueDelta));
                float linear = 2 * (((-incomingSlope - (2 * outgoingSlope)) * timeSpan) + (3 * valueDelta));
                float constant = outgoingSlope * timeSpan;
                if (MathF.Abs(quadratic) < 0.000001f)
                {
                    if (linear != 0)
                    {
                        AddRoot(-constant / linear);
                    }
                }
                else if ((linear * linear) - (4 * quadratic * constant) >= 0)
                {
                    float discriminantRoot = MathF.Sqrt((linear * linear) - (4 * quadratic * constant));
                    AddRoot((-linear + discriminantRoot) / (2 * quadratic));
                    AddRoot((-linear - discriminantRoot) / (2 * quadratic));
                }

                void AddRoot(float fraction)
                {
                    if (fraction > 0 && fraction < 1)
                    {
                        float time = start.Time + (fraction * timeSpan);
                        points.Add(_editor.ToDisplay(curve, new Vector2(time, curve.EvaluateDelta(time))));
                    }
                }
            }
        }

        private Vector2 TangentPosition(bool incoming)
        {
            var curve = _editor.ActiveCurveValue;
            var key = curve.Frames[_editor.SelectedIndex];
            var start = KeyPosition(PrimaryKey);
            var end = CanvasToScreen(_editor.ToDisplay(curve, new Vector2(key.Time, key.Value) + new Vector2(incoming ? -1 : 1, incoming ? key.In : key.Out)));
            return start + ((end - start).Normal * 48 * ScaleToScreen);
        }

        private int HitCurve(Vector2 mouse)
        {
            int hit = -1;
            float distance = 8 * ScaleToScreen;
            foreach (int c in Enumerable.Range(0, _editor._curves.Length).OrderBy(c => c != _editor.ActiveCurve))
            {
                for (int offset = -6; offset <= 6; offset += 2)
                {
                    float x = ScreenToCanvas(mouse + new Vector2(offset * ScaleToScreen, 0)).X;
                    float d = (CanvasToScreen(Evaluate(_editor._curves[c], x)) - mouse).Length();
                    if (d < distance)
                    {
                        distance = d;
                        hit = c;
                    }
                }
            }

            return hit;
        }

        private Vector2 Evaluate(Curve curve, float displayTime)
        {
            var local = _editor.FromDisplay(curve, new Vector2(displayTime, 0));
            return _editor.ToDisplay(curve, new Vector2(local.X, curve.EvaluateDelta(local.X)));
        }
    }
}
