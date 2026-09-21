namespace Socotra;

public partial class DockHost
{
    private static Vector2 MinimumSize(DockNode node)
    {
        if (node is not DockSplit split)
        {
            return new Vector2(120, 80);
        }

        var first = MinimumSize(split.First);
        var second = MinimumSize(split.Second);
        return split.Vertical
            ? new Vector2(MathF.Max(first.X, second.X), first.Y + second.Y + 5)
            : new Vector2(first.X + second.X + 5, MathF.Max(first.Y, second.Y));
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

        protected override void OnEscape(PanelEvent e)
        {
            e.StopPropagation();
            _host.CancelDrag();
        }

        protected override void OnBlur(PanelEvent e)
        {
            _host.CancelDrag();
            base.OnBlur(e);
        }
    }

    private sealed class SplitView : Panel
    {
        private readonly DockHost _host;
        private readonly DockSplit _split;
        private readonly Panel _handle;
        private bool _dragging;
        private float _grabOffset;
        private float _startFraction;

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
                _startFraction = split.Fraction;
                _grabOffset = Axis(_handle.MousePosition) * ScaleFromScreen;
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

        private float Available => MathF.Max(0, (Axis(Box.Rect.Size) * ScaleFromScreen) - 5);

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
            var fraction = ((Axis(MousePosition) * ScaleFromScreen) - _grabOffset) / Available;
            _host._layout.SetFraction(_split, Math.Clamp(ClampFraction(fraction), 0.05f, 0.95f));
        }

        protected override void OnEscape(PanelEvent e)
        {
            if (!_dragging)
            {
                return;
            }

            e.StopPropagation();
            CancelDragging();
        }

        private float Axis(Vector2 value) => _split.Vertical ? value.Y : value.X;

        private float ClampFraction(float fraction)
        {
            var first = Axis(MinimumSize(_split.First));
            var second = Axis(MinimumSize(_split.Second));
            var available = Available;
            return available < first + second ? first / (first + second) : Math.Clamp(fraction, first / available, 1 - (second / available));
        }

        private void StopDragging()
        {
            _dragging = false;
            SetClass("resizing", false);
        }

        private void CancelDragging()
        {
            StopDragging();
            _host._layout.SetFraction(_split, _startFraction);
        }
    }
}
