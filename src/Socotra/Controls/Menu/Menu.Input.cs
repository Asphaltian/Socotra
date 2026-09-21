namespace Socotra;

public partial class Menu
{
    private Menu? _hoverRow;
    private double _hoverSince;

    /// <summary>The row in this menu's open list that the mouse or the arrow keys picked. It has the <c>active</c> class.</summary>
    public Menu? Highlighted { get; private set; }

    internal static float SubmenuOpenDelay { get; set; } = 0.1f;

    internal static float SubmenuCloseDelay { get; set; } = 0.2f;

    /// <summary>Does what clicking this row does: a submenu opens, a checkable row flips, and a command runs and closes the whole menu.</summary>
    public void Activate()
    {
        if (IsSeparator || !Enabled || ParentMenu is null)
        {
            return;
        }

        if (HasOptions)
        {
            ParentMenu.SetHighlighted(this);
            Open();
            return;
        }

        if (Checkable)
        {
            Checked = !Checked;
            Toggled?.Invoke(Checked);
        }
        else
        {
            Clicked?.Invoke();
        }

        if (!StaysOpen)
        {
            RootMenu.Close();
        }
    }

    /// <summary>Activates the row, unless it's a heading at the top of a menu.</summary>
    protected override void OnClick(MousePanelEvent e)
    {
        base.OnClick(e);
        if (ParentMenu is null)
        {
            return;
        }

        e.StopPropagation();
        Activate();
    }

    /// <summary>Highlights the row.</summary>
    protected override void OnMouseOver(MousePanelEvent e)
    {
        base.OnMouseOver(e);
        ParentMenu?.OnRowHovered(this);
    }

    /// <summary>Highlights the row.</summary>
    protected override void OnMouseMove(MousePanelEvent e)
    {
        base.OnMouseMove(e);
        ParentMenu?.OnRowHovered(this);
    }

    /// <summary>Takes the highlight off the row, unless its submenu is open.</summary>
    protected override void OnMouseOut(MousePanelEvent e)
    {
        base.OnMouseOut(e);
        ParentMenu?.OnRowLeft(this);
    }

    private void SetHighlighted(Menu? row)
    {
        if (Highlighted == row)
        {
            return;
        }

        Highlighted?.SetClass("active", false);
        Highlighted = row;
        Highlighted?.SetClass("active", true);
    }

    private void OnRowHovered(Menu row)
    {
        SetHighlighted(row);
        if (_hoverRow == row)
        {
            return;
        }

        _hoverRow = row;
        _hoverSince = row.TimeNow;
    }

    private void OnRowLeft(Menu row)
    {
        if (_hoverRow == row)
        {
            _hoverRow = null;
        }

        if (Highlighted == row && !row.IsOpen)
        {
            SetHighlighted(null);
        }
    }

    private void TickHover(double now)
    {
        if (_hoverRow is null)
        {
            return;
        }

        var openChild = _options.FirstOrDefault(option => option.IsOpen);
        var waited = now - _hoverSince;
        if (_hoverRow is { HasOptions: true, Enabled: true, IsOpen: false })
        {
            if (waited < SubmenuOpenDelay)
            {
                return;
            }

            openChild?.Close();
            _hoverRow.Open();
            return;
        }

        if (openChild is null || openChild == _hoverRow || openChild.ListPanel is { HasHovered: true })
        {
            return;
        }

        if (waited >= SubmenuCloseDelay)
        {
            openChild.Close();
        }
    }

    private bool OnKey(ButtonEvent e)
    {
        _hoverRow = null;
        switch (e.Button)
        {
            case "down":
                MoveHighlight(1);
                return true;
            case "up":
                MoveHighlight(-1);
                return true;
            case "right":
                return OpenHighlightedSubmenu();
            case "left":
                if (ParentMenu is null)
                {
                    return false;
                }

                Close();
                return true;
            case "enter" or "pad_enter" or "space":
                if (!OpenHighlightedSubmenu())
                {
                    Highlighted?.Activate();
                }

                return true;
        }

        if (e.Button.Length == 1 && char.IsLetterOrDigit(e.Button[0]))
        {
            JumpTo(e.Button[0]);
            return true;
        }

        return false;
    }

    private bool OpenHighlightedSubmenu()
    {
        if (Highlighted is not { HasOptions: true, Enabled: true } submenu)
        {
            return false;
        }

        submenu.Open();
        submenu.MoveHighlight(1);
        return true;
    }

    private void MoveHighlight(int direction)
    {
        var count = _options.Count;
        if (count == 0)
        {
            return;
        }

        var index = Highlighted is null ? -1 : _options.IndexOf(Highlighted);
        if (index < 0 && direction < 0)
        {
            index = count;
        }

        for (int i = 0; i < count; i++)
        {
            index = (index + direction + count) % count;
            var row = _options[index];
            if (row.IsSeparator || !row.Enabled)
            {
                continue;
            }

            SetHighlighted(row);
            return;
        }
    }

    private void JumpTo(char letter)
    {
        var count = _options.Count;
        var start = Highlighted is null ? -1 : _options.IndexOf(Highlighted);
        for (int i = 1; i <= count; i++)
        {
            var row = _options[(start + i) % count];
            if (row.IsSeparator || !row.Enabled || string.IsNullOrEmpty(row.Text))
            {
                continue;
            }

            if (char.ToLowerInvariant(row.Text[0]) == char.ToLowerInvariant(letter))
            {
                SetHighlighted(row);
                return;
            }
        }
    }
}
