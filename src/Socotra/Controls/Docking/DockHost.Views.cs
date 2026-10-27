namespace Socotra;

public partial class DockHost
{
    private (float Min, float Max) SizeLimits(DockNode node, bool vertical)
    {
        if (node is DockGroup group)
        {
            return GroupSizeLimits(group, vertical);
        }

        var split = (DockSplit)node;
        var first = SizeLimits(split.First, vertical);
        var second = SizeLimits(split.Second, vertical);
        if (split.Vertical != vertical)
        {
            var minimum = MathF.Max(first.Min, second.Min);
            var maximum = MathF.Min(first.Max, second.Max);
            return (minimum, MathF.Max(minimum, maximum));
        }

        var divider = _views.TryGetValue(split, out var view) && view is SplitView sizing ? sizing.DividerSize : 5;
        return (first.Min + second.Min + divider, first.Max + second.Max + divider);
    }

    private (float Min, float Max) GroupSizeLimits(DockGroup group, bool vertical)
    {
        float minimum = vertical ? 80 : 120;
        float maximum = 0;
        var chrome = GroupChromeSize(group, vertical);
        foreach (var id in group.Items)
        {
            var style = Find(id)?.Content?.ComputedStyle;
            var min = vertical ? style?.MinHeight : style?.MinWidth;
            var max = vertical ? style?.MaxHeight : style?.MaxWidth;
            if (min is { Unit: LengthUnit.Pixels } minPixels)
            {
                minimum = MathF.Max(minimum, (minPixels.Value * ScaleFromScreen) + chrome);
            }

            var contentMaximum = max is { Unit: LengthUnit.Pixels } maxPixels ? (maxPixels.Value * ScaleFromScreen) + chrome : float.PositiveInfinity;
            maximum = MathF.Max(maximum, contentMaximum);
        }

        return (minimum, MathF.Max(minimum, maximum));
    }

    private float GroupChromeSize(DockGroup group, bool vertical)
    {
        if (!_views.TryGetValue(group, out var view) || view is not GroupView pane)
        {
            return 0;
        }

        var box = pane.Body.Box;
        var size = vertical
            ? pane.Tabs.Box.RectOuter.Height + box.Border.Top + box.Border.Bottom + box.Padding.Top + box.Padding.Bottom
            : box.Border.Left + box.Border.Right + box.Padding.Left + box.Padding.Right;
        return MathF.Max(0, size) * ScaleFromScreen;
    }

    private sealed class DockTabBar(DockHost host) : TabBar
    {
        public override void SelectTab(Tab? tab)
        {
            if (tab is DockTab dock)
            {
                host.Activate(dock.Item.Id);
            }
        }

        public override bool CloseTab(Tab? tab) => tab is DockTab dock && host.Close(dock.Item.Id);

        protected override void OnChildRemoved(Panel child)
        {
        }
    }

    private sealed class GroupView : Panel
    {
        public GroupView(DockHost host)
        {
            AddClass("dock-group");
            Tabs = AddChild(new DockTabBar(host));
            Tabs.AddClass("dock-tabs");
            Body = Add.Panel("dock-body");
        }

        public TabBar Tabs { get; }

        public Panel Body { get; }
    }

    private sealed class DockTab : Tab
    {
        private readonly DockHost _host;
        private bool _leftPressed;

        public DockTab(DockHost host, DockItem item)
        {
            _host = host;
            Item = item;
            Text = item.Title;
            Icon = item.Icon;
            CanClose = item.CanClose;
            AddClass("dock-tab");
            foreach (var child in Children)
            {
                if (child.HasClass("tab-title"))
                {
                    child.AddClass("dock-tab-title");
                }

                if (child.HasClass("tab-icon") && !string.IsNullOrWhiteSpace(item.Icon))
                {
                    child.AddClass("dock-tab-icon");
                }

                if (child.HasClass("tab-close") && item.CanClose)
                {
                    child.AddClass("dock-tab-action");
                    child.Tooltip = "Close panel";
                }
            }
        }

        public DockItem Item { get; }

        public override bool WantsDrag => true;

        protected override void OnMouseDown(MousePanelEvent e)
        {
            _leftPressed = e.Button == "mouseleft";
            base.OnMouseDown(e);
        }

        protected override void OnDragStart(DragEvent e)
        {
            e.StopPropagation();
            if (_leftPressed)
            {
                _host.BeginDrag(Item.Id);
            }
        }

        protected override void OnDrag(DragEvent e)
        {
            e.StopPropagation();
            _host.UpdateDrag(e.ScreenPosition);
        }

        protected override void OnDragEnd(DragEvent e)
        {
            e.StopPropagation();
            _host.EndDrag(e.ScreenPosition);
        }

        protected override void OnDragCancel(DragEvent e)
        {
            e.StopPropagation();
            _host.CancelDrag();
        }
    }

    private sealed class SplitView : Panel
    {
        private readonly DockHost _host;
        private readonly DockSplit _split;
        private readonly Panel _handle;
        private readonly List<(float StartSize, float Minimum, float Maximum)> _resizePanes = [];
        private readonly Dictionary<DockSplit, float> _startFractions = [];
        private bool _dragging;
        private float _grabOffset;
        private float _startPosition;
        private SplitView? _resizeRoot;
        private float[] _resizeSizes = [];
        private int _resizeBoundary;

        public SplitView(DockHost host, DockSplit split)
        {
            _host = host;
            _split = split;
            AddClass("dock-split");
            SetClass("vertical", split.Vertical);
            First = Add.Panel("dock-branch");
            _handle = Add.Panel("dock-splitter");
            _handle.AcceptsFocus = true;
            Second = Add.Panel("dock-branch");
            _handle.AddEventListener("onmousedown", e =>
            {
                e.StopPropagation();
                if (e is not MousePanelEvent { Button: "mouseleft" })
                {
                    return;
                }

                _host.CancelDrag();
                _dragging = true;
                _grabOffset = Axis(_handle.MousePosition) * ScaleFromScreen;
                BeginResize();
                SetClass("resizing", true);
            });
            _handle.AddEventListener("onmouseup", e =>
            {
                if (e is MousePanelEvent { Button: "mouseleft" })
                {
                    StopDragging();
                }
            });
        }

        public Panel First { get; }

        public Panel Second { get; }

        public float DividerSize => Axis(_handle.Box.Rect.Size) * ScaleFromScreen;

        private float Available => MathF.Max(0, (Axis(Box.RectInner.Size) * ScaleFromScreen) - DividerSize);

        public void UpdateFraction()
        {
            var fraction = ClampFraction(_split.Fraction);
            First.Style.FlexGrow = fraction;
            Second.Style.FlexGrow = 1 - fraction;
        }

        public override void Tick()
        {
            base.Tick();
            UpdateFraction();
            if (_dragging && FindRootPanel()?.Input.MousePosition is null)
            {
                StopDragging();
            }
        }

        protected override void OnMouseMove(MousePanelEvent e)
        {
            if (!_dragging || Available <= 0)
            {
                return;
            }

            e.StopPropagation();
            Resize((Axis(ScreenMousePosition) * ScaleFromScreen) - _grabOffset - _startPosition);
        }

        protected override void OnEscape(PanelEvent e)
        {
            if (!_dragging)
            {
                return;
            }

            e.StopPropagation();
            CancelResize();
        }

        private float Axis(Vector2 value) => _split.Vertical ? value.Y : value.X;

        private float ClampFraction(float fraction)
        {
            var first = _host.SizeLimits(_split.First, _split.Vertical);
            var second = _host.SizeLimits(_split.Second, _split.Vertical);
            var available = Available;
            if (available < first.Min + second.Min)
            {
                return first.Min / (first.Min + second.Min);
            }

            if (available > first.Max + second.Max)
            {
                return first.Max / (first.Max + second.Max);
            }

            var lower = MathF.Max(first.Min, available - second.Max);
            var upper = MathF.Min(first.Max, available - second.Min);
            return Math.Clamp(fraction, lower / available, upper / available);
        }

        private void BeginResize()
        {
            _startPosition = Axis(_handle.Box.Rect.Position) * ScaleFromScreen;
            _resizeRoot = this;
            while (_resizeRoot.Parent?.Parent is SplitView parent && parent._split.Vertical == _split.Vertical)
            {
                _resizeRoot = parent;
            }

            _resizePanes.Clear();
            _startFractions.Clear();
            CollectResizePanes(_resizeRoot._split);
            _resizeSizes = new float[_resizePanes.Count];
        }

        private void CollectResizePanes(DockNode node)
        {
            if (node is DockSplit split && split.Vertical == _split.Vertical)
            {
                _startFractions.Add(split, split.Fraction);
                CollectResizePanes(split.First);
                if (split == _split)
                {
                    _resizeBoundary = _resizePanes.Count;
                }

                CollectResizePanes(split.Second);
                return;
            }

            var size = Axis(_host._views[node].Box.Rect.Size) * ScaleFromScreen;
            var limits = _host.SizeLimits(node, _split.Vertical);
            _resizePanes.Add((size, MathF.Min(size, limits.Min), MathF.Max(size, limits.Max)));
        }

        private void Resize(float delta)
        {
            for (int i = 0; i < _resizePanes.Count; i++)
            {
                _resizeSizes[i] = _resizePanes[i].StartSize;
            }

            var firstCapacity = ResizeCapacity(0, _resizeBoundary, grow: delta > 0);
            var secondCapacity = ResizeCapacity(_resizeBoundary, _resizePanes.Count, grow: delta < 0);
            var movement = MathF.Min(MathF.Abs(delta), MathF.Min(firstCapacity, secondCapacity)) * MathF.Sign(delta);
            DistributeResize(_resizeBoundary - 1, -1, -1, movement);
            DistributeResize(_resizeBoundary, _resizePanes.Count, 1, -movement);

            var paneIndex = 0;
            ApplyResize(_resizeRoot!._split, ref paneIndex);
        }

        private float ResizeCapacity(int start, int end, bool grow)
        {
            float capacity = 0;
            for (int i = start; i < end; i++)
            {
                var pane = _resizePanes[i];
                capacity += grow ? pane.Maximum - pane.StartSize : pane.StartSize - pane.Minimum;
            }

            return capacity;
        }

        private void DistributeResize(int start, int end, int step, float remaining)
        {
            for (int i = start; i != end; i += step)
            {
                var pane = _resizePanes[i];
                var change = Math.Clamp(remaining, pane.Minimum - pane.StartSize, pane.Maximum - pane.StartSize);
                _resizeSizes[i] += change;
                remaining -= change;
            }
        }

        private float ApplyResize(DockNode node, ref int paneIndex)
        {
            if (node is not DockSplit split || split.Vertical != _split.Vertical)
            {
                return _resizeSizes[paneIndex++];
            }

            var firstSize = ApplyResize(split.First, ref paneIndex);
            var secondSize = ApplyResize(split.Second, ref paneIndex);
            _host._layout.SetFraction(split, Math.Clamp(firstSize / (firstSize + secondSize), 0.05f, 0.95f));
            return firstSize + secondSize + ((SplitView)_host._views[split]).DividerSize;
        }

        private void StopDragging()
        {
            _dragging = false;
            SetClass("resizing", false);
        }

        private void CancelResize()
        {
            StopDragging();
            foreach (var (split, fraction) in _startFractions)
            {
                _host._layout.SetFraction(split, fraction);
            }
        }
    }
}
