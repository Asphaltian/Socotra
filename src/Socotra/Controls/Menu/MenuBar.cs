namespace Socotra;

/// <summary>A row of menu headings, like File, Edit and View. Click one to open it; while one is open, hovering another switches to it.</summary>
/// <example>
/// <code>
/// // A menu bar with a File menu
/// var bar = new MenuBar { Parent = window };
/// var file = bar.AddMenu("File");
/// file.AddOption("Open", "folder_open", () => OpenFile());
/// file.AddSeparator();
/// file.AddOption("Quit", () => Quit());
/// </code>
/// </example>
[StyleSheet.Inline("menubar", Styles)]
public class MenuBar : Panel
{
    private const string Styles = """
        .menubar
        {
            flex-direction: row;
            align-items: center;
            flex-shrink: 0;
        }

        .menubar > .menu
        {
            height: auto;
            padding: 4px 8px;
        }

        .menubar > .menu:active { background-color: rgba( 255, 255, 255, 0.16 ); }

        .menubar > .menu > .gutter,
        .menubar > .menu > .shortcut,
        .menubar > .menu.has-submenu > .chevron { display: none; }

        .menubar > .menu.has-icon > .gutter { display: flex; width: auto; margin-right: 4px; }
        .menubar > .menu.has-icon > .gutter > .icon { display: flex; }
        .menubar > .menu.has-icon > .gutter > .check { display: none; }
        """;

    /// <summary>Makes an empty menu bar.</summary>
    public MenuBar()
    {
        AddClass("menubar");
    }

    /// <summary>The heading whose menu is showing, or null.</summary>
    public Menu? OpenMenu { get; private set; }

    /// <summary>The headings, in order.</summary>
    public IEnumerable<Menu> Menus => Children.OfType<Menu>();

    /// <summary>Adds <paramref name="menu"/> as a heading. Its text is the heading and its options open under it.</summary>
    public Menu AddMenu(Menu menu)
    {
        menu.Parent = this;
        return menu;
    }

    /// <summary>Adds a heading showing <paramref name="text"/>. Give the returned menu options.</summary>
    public Menu AddMenu(string? text) => AddMenu(new Menu(text));

    /// <summary>Opens the menu of <paramref name="menu"/>, closing whichever was open.</summary>
    public void Show(Menu menu)
    {
        if (OpenMenu == menu)
        {
            return;
        }

        OpenMenu?.Close();
        menu.Open(menu, Popup.PositionMode.BelowLeft);
        OpenMenu = menu.IsOpen ? menu : null;
    }

    /// <summary>Closes the open menu.</summary>
    public void CloseAll() => OpenMenu?.Close();

    /// <summary>Moves between the headings with Left and Right while a menu is open and doesn't use them.</summary>
    public override void OnButtonTyped(ButtonEvent e)
    {
        if (OpenMenu is not null && e.Button is "left" or "right")
        {
            MoveOpen(e.Button == "right" ? 1 : -1);
            e.StopPropagation = true;
            return;
        }

        base.OnButtonTyped(e);
    }

    /// <inheritdoc/>
    protected override void OnChildAdded(Panel child)
    {
        base.OnChildAdded(child);
        if (child is Menu menu)
        {
            menu.Closed += OnMenuClosed;
        }
    }

    /// <inheritdoc/>
    protected override void OnChildRemoved(Panel child)
    {
        base.OnChildRemoved(child);
        if (child is Menu menu)
        {
            menu.Close();
            menu.Closed -= OnMenuClosed;
        }
    }

    /// <summary>Opens the clicked heading's menu, or closes it if it's open.</summary>
    protected override void OnClick(MousePanelEvent e)
    {
        base.OnClick(e);
        if (Heading(e.Target) is not { Enabled: true } menu)
        {
            return;
        }

        e.StopPropagation();
        if (OpenMenu == menu)
        {
            menu.Close();
        }
        else
        {
            Show(menu);
        }
    }

    /// <summary>Switches to the hovered heading's menu while another one is open.</summary>
    protected override void OnMouseOver(MousePanelEvent e)
    {
        base.OnMouseOver(e);
        if (OpenMenu is null || Heading(e.Target) is not { Enabled: true } menu || menu == OpenMenu)
        {
            return;
        }

        Show(menu);
    }

    private Menu? Heading(Panel? panel)
    {
        for (var current = panel; current is not null && current != this; current = current.Parent)
        {
            if (current is Menu menu && menu.Parent == this)
            {
                return menu;
            }
        }

        return null;
    }

    private void MoveOpen(int direction)
    {
        if (OpenMenu is null)
        {
            return;
        }

        var menus = Menus.ToList();
        if (menus.Count < 2)
        {
            return;
        }

        var index = menus.IndexOf(OpenMenu);
        Show(menus[(index + direction + menus.Count) % menus.Count]);
    }

    private void OnMenuClosed(Menu menu)
    {
        if (OpenMenu == menu)
        {
            OpenMenu = null;
        }
    }
}
