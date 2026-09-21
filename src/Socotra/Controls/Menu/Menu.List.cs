namespace Socotra;

public partial class Menu
{
    private const float SubmenuOverlap = -4;

    private MenuList? _list;
    private (RootPanel Root, double Time)? _dismissed;
    private bool _closing;

    /// <summary>Whether this menu's options are showing.</summary>
    public bool IsOpen => _list is { IsDeleted: false, IsDeleting: false };

    internal static float ReopenGuard { get; set; } = 0.3f;

    internal Popup? ListPanel => IsOpen ? _list : null;

    /// <summary>
    /// Shows this menu's options next to <paramref name="source"/>, <paramref name="offset"/> pixels away on the side
    /// <paramref name="position"/> picks. Use it for context menus and menu buttons. A menu with no options doesn't open.
    /// </summary>
    public void Open(Panel source, Popup.PositionMode position, float offset = 0)
    {
        if (IsOpen)
        {
            return;
        }

        if (_dismissed is var (root, time) && root == source.FindRootPanel() && root.Time - time < ReopenGuard)
        {
            _dismissed = null;
            return;
        }

        AboutToShow?.Invoke(this);
        if (_rows.Count == 0)
        {
            return;
        }

        _list = new MenuList(this);
        _list.SetPositioning(source, position, offset);
        _list.RemoveClass("popup-panel");
        foreach (var row in _rows)
        {
            row.Parent = _list;
        }

        SetHighlighted(null);
        SetClass("open", true);
        _list.Focus();
    }

    /// <summary>Shows this menu's options beside its own row, the way a submenu opens.</summary>
    public void Open() => Open(this, Popup.PositionMode.RightTop, SubmenuOverlap);

    /// <summary>Hides this menu's options, and any submenus open from them.</summary>
    public void Close()
    {
        _closing = true;
        _list?.Delete(true);
        _closing = false;
    }

    private void OnListClosing(MenuList list)
    {
        if (list != _list)
        {
            return;
        }

        _list = null;
        if (!_closing && list.FindRootPanel() is { } root)
        {
            _dismissed = (root, root.Time);
        }

        foreach (var row in _rows)
        {
            (row as Menu)?.Close();
            row.Parent = null;
            row.Switch(PseudoClass.Hover, false);
        }

        SetHighlighted(null);
        _hoverRow = null;
        SetClass("open", false);
        Closed?.Invoke(this);
    }

    [StyleSheet.Inline("menu", Styles)]
    private sealed class MenuList : Popup
    {
        private readonly Menu _owner;
        private bool _closing;

        public MenuList(Menu owner)
        {
            _owner = owner;
            AcceptsFocus = true;
            AddClass("menulist");
        }

        protected override BasePopup? ParentPopup => _owner.ParentMenu?._list ?? PopupSource?.AncestorsAndSelf.OfType<BasePopup>().FirstOrDefault();

        internal override Panel? StyleParent => _owner.Parent is not null ? _owner : PopupSource;

        public override void Delete(bool immediate = false)
        {
            if (_closing)
            {
                return;
            }

            _closing = true;
            _owner.OnListClosing(this);
            base.Delete(true);
            _owner.ParentMenu?.ListPanel?.Focus();
        }

        public override void Tick()
        {
            base.Tick();
            _owner.TickHover(TimeNow);
        }

        public override void OnButtonTyped(ButtonEvent e)
        {
            if (_owner.OnKey(e))
            {
                e.StopPropagation = true;
                return;
            }

            base.OnButtonTyped(e);
        }

        protected override void OnEscape(PanelEvent e) => _owner.Close();

        protected override void OnLayout(ref Rect rect)
        {
            if (Position == PositionMode.RightTop && PopupSource is { } source && FindRootPanel() is { } root && rect.Right > root.Box.Rect.Right)
            {
                var width = rect.Width;
                rect.Left = source.Box.Rect.Left - width - (PopupSourceOffset * source.ScaleToScreen);
                rect.Right = rect.Left + width;
            }

            base.OnLayout(ref rect);
        }
    }
}
