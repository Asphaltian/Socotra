namespace Socotra;

internal sealed partial class PanelInput(RootPanel root)
{
    private const float DoubleClickTime = 0.4f;
    private const float DoubleClickDistance = 5;
    private const float DragThreshold = 5;

    private readonly Queue<(MouseButtons Button, bool Down, KeyboardModifiers Modifiers)> _mouseButtons = new();
    private readonly InputEventQueue _queue = new();
    private readonly Selection _selection = new();
    private readonly MouseButtonState[] _mouseStates = [new(MouseButtons.Left), new(MouseButtons.Right), new(MouseButtons.Middle), new(MouseButtons.Back), new(MouseButtons.Forward)];
    private Vector2 _wheel;
    private Vector2 _relativeMovement;
    private Vector2? _lastPosition;
    private Vector2 _lastClickPosition;
    private double _lastClickTime = double.NegativeInfinity;
    private int _clickCounter;
    private bool _usingButtons = true;
    private bool _composing;
    private bool _escapeDown;
    private Panel? _mouseCapture;
    private Panel? _tooltipTarget;

    public Vector2? MousePosition { get; private set; }

    public Vector2 CursorDelta { get; private set; }

    public Vector2 CursorVelocity { get; private set; }

    public Panel? Hovered { get; private set; }

    public Panel? Active { get; private set; }

    public Panel? DropTarget { get; private set; }

    public Panel? MouseCapture => _mouseCapture is { IsDeleted: false } ? _mouseCapture : null;

    public KeyboardModifiers MouseModifiers { get; private set; }

    public RootPanel Root => root;

    public TooltipSystem Tooltips { get; } = new(root);

    public void SetMousePosition(Vector2? position)
    {
        if (position != MousePosition)
        {
            _queue.MouseMoved();
            _usingButtons &= position is null;
        }

        MousePosition = position;
    }

    public void AddMouseDelta(Vector2 delta)
    {
        _relativeMovement += delta;
        _queue.MouseMoved();
    }

    public void SetMouseButton(MouseButtons button, bool down, KeyboardModifiers modifiers)
    {
        _usingButtons = false;
        _mouseButtons.Enqueue((button, down, modifiers));
        var e = new ButtonEvent(MouseButtonState.NameOf(button), down, modifiers);
        _queue.AddButtonEvent(e);
        if (down)
        {
            _queue.AddButtonTyped(e);
        }
    }

    public void AddMouseWheel(Vector2 delta, KeyboardModifiers modifiers) =>
        _wheel += (modifiers & KeyboardModifiers.Shift) != 0 ? new Vector2(delta.Y, 0) : delta;

    public void AddButtonEvent(ButtonEvent e)
    {
        _usingButtons = true;
        if (e.Button == "escape")
        {
            if (e.Pressed && !_escapeDown)
            {
                _queue.AddEscape();
            }

            _escapeDown = e.Pressed;
            return;
        }

        _queue.AddButtonEvent(e);
        if (e.Pressed)
        {
            _queue.AddButtonTyped(e);
        }
    }

    public void TypeText(string text)
    {
        foreach (var c in text)
        {
            _queue.AddKeyTyped(c);
        }
    }

    public void SetImeComposition(string? text) => _composing = ImeComposition.Update(Focused, _composing, text);

    public void SetMouseCapture(Panel panel, bool capture)
    {
        if (capture)
        {
            _mouseCapture = panel;
        }
        else if (_mouseCapture == panel)
        {
            _mouseCapture = null;
        }
    }

    public void ReleaseSubtree(Panel subtree)
    {
        ReleaseFocus(subtree);
        _selection.Release(subtree);
        if (_mouseCapture?.IsAncestor(subtree) == true)
        {
            _mouseCapture = null;
        }

        if (Hovered?.IsAncestor(subtree) == true)
        {
            SetHovered(null);
        }

        if (DropTarget?.IsAncestor(subtree) == true || _mouseStates[0].DragTarget?.IsAncestor(subtree) == true)
        {
            ClearDropTarget();
        }

        foreach (var state in _mouseStates)
        {
            state.Release(subtree);
        }

        RestoreActive();
        UpdateActive();
    }

    public void Tick()
    {
        UpdateCursor();
        UpdateHovered();
        var (leftPressed, leftReleased) = UpdateButtons();
        if (_wheel != Vector2.Zero)
        {
            Hovered?.OnMouseWheel(_wheel);
            _wheel = Vector2.Zero;
        }

        if (MousePosition is { } cursor)
        {
            _selection.UpdateSelection(Hovered, _mouseStates[0].Pressed, leftPressed, leftReleased, cursor);
        }

        TickFocus();
        _queue.TickFocused(() => Focused ?? root, TickFocus);
        _queue.Tick(Hovered, Active);
        Tooltips.SetHovered(_mouseStates[0].Dragged ? Hovered : _tooltipTarget, MousePosition ?? Vector2.Zero);
        Tooltips.Frame(MousePosition ?? Vector2.Zero, MousePosition is not null);
        Focused?.Switch(PseudoClass.FocusVisible, _usingButtons);
    }

    private static bool CanHover(Panel panel) => !panel.IsDeleting && !IsDisabled(panel);

    private static bool CanShowTooltip(Panel panel) => !panel.IsDeleting;

    private static bool IsDisabled(Panel panel) => panel.AncestorsAndSelf.Any(static p => p.Disabled);

    private static void SwitchWithAncestors(PseudoClass pseudoClass, bool state, Panel? panel, Panel? unlessAncestorOf = null)
    {
        for (var target = panel; target is not null; target = target.Parent)
        {
            if (unlessAncestorOf?.IsAncestor(target) != true)
            {
                target.Switch(pseudoClass, state);
            }
        }
    }

    private void UpdateHovered()
    {
        Panel? PanelAt(Vector2 position, Func<Panel, bool> match) =>
            root.FindFixedPanelAt(position, needPointerEvents: true, match) ?? root.FindVisualPanelAt(position, needPointerEvents: true, match);

        _tooltipTarget = MousePosition is { } cursor ? PanelAt(cursor, CanShowTooltip) : null;
        var found = _tooltipTarget is null || CanHover(_tooltipTarget) ? _tooltipTarget : PanelAt(MousePosition!.Value, CanHover);

        if (_mouseStates[0].Dragged)
        {
            UpdateDropTarget(found);
        }
        else
        {
            SetHovered(found);
        }

        if (found is null)
        {
            ClearDropTarget();
        }
    }

    private (bool LeftPressed, bool LeftReleased) UpdateButtons()
    {
        bool leftPressed = false;
        bool leftReleased = false;
        var changed = new HashSet<MouseButtonState>();
        while (_mouseButtons.TryDequeue(out var change))
        {
            var state = StateOf(change.Button);
            changed.Add(state);
            CountClick(change.Button, change.Down, state);
            MouseModifiers = change.Modifiers;
            state.Update(this, change.Down, Hovered);
            leftPressed |= state == _mouseStates[0] && change.Down;
            leftReleased |= state == _mouseStates[0] && !change.Down;
        }

        foreach (var state in _mouseStates)
        {
            if (!changed.Contains(state))
            {
                state.Update(this, state.Pressed, Hovered);
            }
        }

        UpdateActive();
        if (Active is null && changed.Count > 0)
        {
            SwitchWithAncestors(PseudoClass.Hover, true, Hovered);
        }

        return (leftPressed, leftReleased);
    }

    private MouseButtonState StateOf(MouseButtons button) => _mouseStates.First(s => s.MouseButton == button);

    private void UpdateCursor()
    {
        var delta = _relativeMovement;
        if (MousePosition is { } position && _lastPosition is { } last)
        {
            delta += position - last;
        }

        _lastPosition = MousePosition;
        _relativeMovement = Vector2.Zero;
        CursorDelta = delta;
        CursorVelocity = ((CursorVelocity * 2) + delta) / 3;
    }

    private void CountClick(MouseButtons button, bool down, MouseButtonState state)
    {
        var position = MousePosition ?? _lastClickPosition;
        if (root.Time - _lastClickTime > DoubleClickTime || Vector2.Distance(position, _lastClickPosition) > DoubleClickDistance)
        {
            _clickCounter = 0;
        }

        _lastClickPosition = position;
        _lastClickTime = root.Time;
        if (down)
        {
            state.ClickCount = _clickCounter + 1;
            return;
        }

        _clickCounter++;
        if (_clickCounter == 2)
        {
            _queue.AddDoubleClick(MouseButtonState.NameOf(button));
        }
        else if (_clickCounter == 3)
        {
            _queue.AddTripleClick(MouseButtonState.NameOf(button));
        }
    }

    private void SetHovered(Panel? panel)
    {
        if (panel == Hovered)
        {
            return;
        }

        if (Hovered is { IsDeleted: false } previous)
        {
            SwitchWithAncestors(PseudoClass.Hover, false, previous, panel);
            previous.CreateEvent(new MousePanelEvent("onmouseout", previous, "none"));
        }

        Hovered = panel;
        if (panel is not null)
        {
            if (Active is null || Active == panel)
            {
                SwitchWithAncestors(PseudoClass.Hover, true, panel);
            }

            panel.CreateEvent(new MousePanelEvent("onmouseover", panel, "none"));
        }
    }

    private void UpdateDropTarget(Panel? current)
    {
        if (current == DropTarget)
        {
            return;
        }

        var dragSource = _mouseStates[0].DragTarget;
        DropTarget?.CreateEvent(new PanelEvent("ondragleave", dragSource));
        DropTarget = current;
        DropTarget?.CreateEvent(new PanelEvent("ondragenter", dragSource));
    }

    private void ClearDropTarget()
    {
        if (DropTarget is null)
        {
            return;
        }

        DropTarget.CreateEvent(new PanelEvent("ondragleave", _mouseStates[0].DragTarget));
        DropTarget = null;
    }

    private void UpdateActive()
    {
        Active = null;
        foreach (var state in (ReadOnlySpan<MouseButtonState>)[_mouseStates[2], _mouseStates[1], _mouseStates[0]])
        {
            if (state.Active is { IsDeleted: false } pressed)
            {
                Active = pressed;
            }
        }
    }

    private void RestoreActive()
    {
        foreach (var state in _mouseStates)
        {
            if (state.Active is { IsDeleted: false } pressed)
            {
                SwitchWithAncestors(PseudoClass.Active, true, pressed);
            }
        }
    }

    private sealed class MouseButtonState(MouseButtons button)
    {
        private MousePanelEvent? _mouseDownEvent;
        private Vector2 _startHoldOffsetLocal;
        private Vector2 _startHoldOffsetScreen;

        public MouseButtons MouseButton => button;

        public int ClickCount { get; set; } = 1;

        public bool Pressed { get; private set; }

        public Panel? Active { get; private set; }

        public bool Dragged { get; private set; }

        public Panel? DragTarget { get; private set; }

        public static string NameOf(MouseButtons button) => button switch
        {
            MouseButtons.Left => "mouseleft",
            MouseButtons.Right => "mouseright",
            MouseButtons.Middle => "mousemiddle",
            MouseButtons.Back => "mouseback",
            MouseButtons.Forward => "mouseforward",
            _ => "none",
        };

        public void Release(Panel subtree)
        {
            if (DragTarget?.IsAncestor(subtree) == true)
            {
                Dragged = false;
                DragTarget = null;
            }

            if (Active?.IsAncestor(subtree) == true)
            {
                SwitchWithAncestors(PseudoClass.Active, false, Active);
                Active = null;
            }
        }

        public void Update(PanelInput input, bool down, Panel? hovered)
        {
            if (Pressed && down && DragTarget is { IsDeleted: false } dragTarget && input.CursorDelta != Vector2.Zero && _mouseDownEvent?.Propagate == true)
            {
                var delta = _startHoldOffsetLocal - (dragTarget.MousePosition + dragTarget.ScrollOffset);
                if (delta.Length() > DragThreshold && !Dragged)
                {
                    Dragged = true;
                    dragTarget.CreateEvent(new DragEvent("ondragstart", dragTarget, _startHoldOffsetLocal, _startHoldOffsetScreen));
                    if (Active is not { IsDeleted: false } active)
                    {
                        Active = null;
                        DragTarget = null;
                        return;
                    }

                    SwitchWithAncestors(PseudoClass.Active, false, active);
                    SwitchWithAncestors(PseudoClass.Hover, false, active);
                    active.CreateEvent(new MousePanelEvent("onmouseup", active, NameOf(button)) { KeyboardModifiers = input.MouseModifiers });
                    active.OnButtonEvent(new ButtonEvent(NameOf(button), false));
                    Active = null;
                    input.RestoreActive();
                }

                if (Dragged)
                {
                    dragTarget.CreateEvent(new DragEvent("ondrag", dragTarget, _startHoldOffsetLocal, _startHoldOffsetScreen) { MouseDelta = input.CursorDelta });
                }
            }

            if (Pressed == down)
            {
                return;
            }

            Pressed = down;
            if (down)
            {
                OnPressed(input, hovered);
            }
            else
            {
                OnReleased(input, hovered);
            }
        }

        private void OnPressed(PanelInput input, Panel? hovered)
        {
            if (button is MouseButtons.Back or MouseButtons.Forward)
            {
                hovered?.CreateEvent(new PanelEvent(button == MouseButtons.Back ? "onback" : "onforward", hovered));
                hovered?.OnButtonEvent(new ButtonEvent(NameOf(button), true));
                return;
            }

            Active = hovered;
            input.Root.ClosePopups(hovered);
            if (hovered is null)
            {
                Dragged = false;
                DragTarget = null;
                return;
            }

            SwitchWithAncestors(PseudoClass.Active, true, hovered);
            if (button is MouseButtons.Left or MouseButtons.Right)
            {
                Dragged = false;
                DragTarget = hovered.FindDragTarget();
                if (DragTarget is not null)
                {
                    _startHoldOffsetLocal = DragTarget.MousePosition + DragTarget.ScrollOffset;
                    _startHoldOffsetScreen = input.MousePosition ?? Vector2.Zero;
                }
            }

            hovered.Focus();
            _mouseDownEvent = new MousePanelEvent("onmousedown", hovered, NameOf(button)) { KeyboardModifiers = input.MouseModifiers, ClickCount = ClickCount };
            ClickCount = 1;
            hovered.CreateEvent(_mouseDownEvent);
            hovered.OnButtonEvent(new ButtonEvent(NameOf(button), true));
        }

        private void OnReleased(PanelInput input, Panel? hovered)
        {
            if (button is MouseButtons.Back or MouseButtons.Forward)
            {
                hovered?.OnButtonEvent(new ButtonEvent(NameOf(button), false));
                return;
            }

            bool canClick = hovered == Active && !Dragged;
            if (Dragged && DragTarget is { } dragTarget)
            {
                dragTarget.CreateEvent(new DragEvent("ondragend", dragTarget, _startHoldOffsetLocal, _startHoldOffsetScreen));
                input.DropTarget?.CreateEvent(new PanelEvent("ondrop", dragTarget));
                input.ClearDropTarget();
            }

            Dragged = false;
            DragTarget = null;
            if (Active is not { IsDeleted: false } active)
            {
                Active = null;
                return;
            }

            var name = NameOf(button);
            active.CreateEvent(new MousePanelEvent("onmouseup", active, name) { KeyboardModifiers = input.MouseModifiers });
            if (canClick)
            {
                var click = button switch
                {
                    MouseButtons.Middle => "onmiddleclick",
                    MouseButtons.Right => "onrightclick",
                    _ => "onclick",
                };
                active.CreateEvent(new MousePanelEvent(click, active, name) { KeyboardModifiers = input.MouseModifiers });
            }
            else
            {
                SwitchWithAncestors(PseudoClass.Hover, false, active, hovered);
            }

            SwitchWithAncestors(PseudoClass.Active, false, active);
            active.OnButtonEvent(new ButtonEvent(name, false));
            Active = null;
            input.RestoreActive();
        }
    }
}
