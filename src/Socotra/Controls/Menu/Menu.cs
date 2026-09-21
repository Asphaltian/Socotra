using Microsoft.AspNetCore.Components;

namespace Socotra;

/// <summary>
/// One row in a menu, and the menu of options it opens. A menu with no options is a plain command; give it options
/// and it opens a submenu. A menu with no parent is a context menu, or a heading in a <see cref="MenuBar"/>.
/// </summary>
/// <example>
/// <code>
/// // A context menu with a command, a toggle and a submenu, opened where the mouse is
/// var menu = new Menu();
/// menu.AddOption("Rename", "edit", () => StartRename());
/// menu.AddOption("Show Grid", on => grid.Visible = on).Checked = grid.Visible;
/// var sort = menu.AddMenu("Sort By");
/// sort.AddOption("Name", () => SortByName());
/// sort.AddOption("Date", () => SortByDate());
/// menu.Open(item, Popup.PositionMode.UnderMouse);
/// </code>
/// </example>
[StyleSheet.Inline("menu", Styles)]
public partial class Menu : Panel
{
    private readonly IconPanel _icon;
    private readonly Label _label;
    private readonly Label _shortcutLabel;
    private readonly List<Panel> _rows = [];
    private readonly List<Menu> _options = [];
    private readonly Dictionary<Panel, CancellationTokenRegistration> _rowDeletions = [];
    private readonly bool _built;
    private string? _text;
    private string? _iconName;
    private string? _shortcut;
    private bool _isSeparator;
    private bool _checkable;
    private bool _checked;
    private bool? _staysOpen;
    private Action<bool>? _toggled;

    /// <summary>Makes an empty menu row.</summary>
    public Menu()
    {
        AddClass("menu");

        var gutter = Add.Panel("gutter part");
        gutter.Add.Icon("check", "check");
        _icon = gutter.Add.Icon(null, "icon");
        _label = Add.Label(null, "text part");
        _shortcutLabel = Add.Label(null, "shortcut part");
        Add.Icon("chevron_right", "chevron part");

        _built = true;
    }

    /// <summary>Makes a menu row showing <paramref name="text"/> and the icon named <paramref name="icon"/>.</summary>
    public Menu(string? text, string? icon = null)
        : this()
    {
        Text = text;
        Icon = icon;
    }

    /// <summary>Called each time this menu's options are about to show. Add, remove or disable options here.</summary>
    public event Action<Menu>? AboutToShow;

    /// <summary>Called when this menu's options close, however that happened.</summary>
    public event Action<Menu>? Closed;

    /// <summary>The text on the row.</summary>
    [Parameter]
    public string? Text
    {
        get => _text;
        set
        {
            _text = value;
            _label.Text = value;
        }
    }

    /// <summary>The name of an icon shown before the text. It needs an icon font; see <see cref="IconPanel"/>.</summary>
    [Parameter]
    public string? Icon
    {
        get => _iconName;
        set
        {
            _iconName = value;
            _icon.Text = value;
            SetClass("has-icon", !string.IsNullOrEmpty(value));
        }
    }

    /// <summary>Set it to false to dim the row and make it do nothing when clicked. It gives the row <c>:disabled</c>.</summary>
    [Parameter]
    public bool Enabled
    {
        get => !Disabled;
        set => Disabled = !value;
    }

    /// <summary>Makes the row a line between other rows.</summary>
    [Parameter]
    public bool IsSeparator
    {
        get => _isSeparator;
        set
        {
            _isSeparator = value;
            SetClass("separator", value);
        }
    }

    /// <summary>Makes clicking the row flip <see cref="Checked"/> instead of calling <see cref="Clicked"/>.</summary>
    [Parameter]
    public bool Checkable
    {
        get => _checkable;
        set
        {
            _checkable = value;
            SetClass("checkable", value);
        }
    }

    /// <summary>Shows a check mark before the text.</summary>
    [Parameter]
    public bool Checked
    {
        get => _checked;
        set
        {
            _checked = value;
            SetClass("checked", value);
        }
    }

    /// <summary>Text shown on the right of the row, like <c>Ctrl+S</c>. It's only shown; handle the key yourself.</summary>
    [Parameter]
    public string? Shortcut
    {
        get => _shortcut;
        set
        {
            _shortcut = value;
            _shortcutLabel.Text = value;
            SetClass("has-shortcut", !string.IsNullOrEmpty(value));
        }
    }

    /// <summary>
    /// Whether the menu stays open after this row is clicked. By default <see cref="Checkable"/> rows keep it open, so
    /// several can be flipped in one go, and commands close it.
    /// </summary>
    [Parameter]
    public bool StaysOpen
    {
        get => _staysOpen ?? Checkable;
        set => _staysOpen = value;
    }

    /// <summary>Called when the row is clicked.</summary>
    [Parameter]
    public Action? Clicked { get; set; }

    /// <summary>Called with the new <see cref="Checked"/> state when a checkable row is clicked. Setting it makes the row <see cref="Checkable"/>.</summary>
    [Parameter]
    public Action<bool>? Toggled
    {
        get => _toggled;
        set
        {
            _toggled = value;
            if (value is not null)
            {
                Checkable = true;
            }
        }
    }

    /// <summary>The menu this row is an option of, or null for a menu at the top.</summary>
    public Menu? ParentMenu { get; private set; }

    /// <summary>The menu at the top of this one's tree.</summary>
    public Menu RootMenu => ParentMenu?.RootMenu ?? this;

    /// <summary>This menu's option rows, in order.</summary>
    public IReadOnlyList<Menu> Options => _options;

    /// <summary>Everything the menu shows, in order: its options and any other panels added with <see cref="AddWidget"/>.</summary>
    public IReadOnlyList<Panel> Rows => _rows;

    /// <summary>Whether this row opens a submenu.</summary>
    public bool HasOptions => _rows.Count > 0;

    /// <summary>Adds an option showing <paramref name="text"/> and the icon named <paramref name="icon"/> that calls <paramref name="action"/> when clicked. Returns it, so you can set it up further or give it options of its own.</summary>
    public Menu AddOption(string? text, string? icon = null, Action? action = null) => AddOption(new Menu(text, icon) { Clicked = action });

    /// <summary>Adds an option showing <paramref name="text"/> that calls <paramref name="action"/> when clicked.</summary>
    public Menu AddOption(string? text, Action? action) => AddOption(text, null, action);

    /// <summary>Adds a checkable option showing <paramref name="text"/> that calls <paramref name="toggled"/> with its new state when clicked.</summary>
    public Menu AddOption(string? text, Action<bool> toggled) => AddOption(new Menu(text) { Toggled = toggled });

    /// <summary>Adds a submenu showing <paramref name="text"/> and the icon named <paramref name="icon"/>. Give it options of its own.</summary>
    public Menu AddMenu(string? text, string? icon = null) => AddOption(new Menu(text, icon));

    /// <summary>Adds a line between rows.</summary>
    public Menu AddSeparator() => AddOption(new Menu { IsSeparator = true });

    /// <summary>Adds a row you made yourself, taking it out of any menu it was in.</summary>
    public Menu AddOption(Menu option)
    {
        option.ParentMenu?.Remove(option);
        option.ParentMenu = this;
        _options.Add(option);
        AddRow(option);
        return option;
    }

    /// <summary>Adds any panel as a row, like a text entry or a heading. The menu shows it but leaves its input alone.</summary>
    public T AddWidget<T>(T widget)
        where T : Panel
    {
        AddRow(widget);
        return widget;
    }

    /// <summary>Takes a row out of the menu without deleting it. Deleted rows leave the menu by themselves.</summary>
    public void Remove(Panel row)
    {
        if (!_rows.Remove(row))
        {
            return;
        }

        _rowDeletions.Remove(row, out var deletion);
        deletion.Dispose();

        if (row is Menu option)
        {
            option.Close();
            option.ParentMenu = null;
            _options.Remove(option);
        }

        if (row.Parent == _list)
        {
            row.Parent = null;
        }

        row.RemoveClass("menu-row");
        SetClass("has-submenu", HasOptions);
    }

    /// <summary>The first option showing <paramref name="text"/>, or null.</summary>
    public Menu? FindOption(string text) => _options.FirstOrDefault(option => option.Text == text);

    /// <summary>Deletes every row.</summary>
    public void Clear()
    {
        foreach (var row in _rows.ToArray())
        {
            Remove(row);
            row.Delete(true);
        }
    }

    /// <inheritdoc/>
    public override void OnDeleted()
    {
        base.OnDeleted();
        Close();
    }

    /// <summary>Makes a panel put inside the menu, from markup or <see cref="Panel.AddChild{T}(T)"/>, one of its rows.</summary>
    protected override void OnChildAdded(Panel child)
    {
        base.OnChildAdded(child);
        if (!_built)
        {
            return;
        }

        if (child is Menu option)
        {
            AddOption(option);
        }
        else
        {
            AddWidget(child);
        }
    }

    private void AddRow(Panel row)
    {
        _rows.Add(row);
        _rowDeletions[row] = row.DeletionToken.Register(() => Remove(row));
        row.AddClass("menu-row");
        if (IsOpen)
        {
            row.Parent = _list;
        }

        SetClass("has-submenu", true);
    }
}
