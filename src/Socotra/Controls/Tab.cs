using System.Globalization;
using Microsoft.AspNetCore.Components;

namespace Socotra;

/// <summary>
/// A tab in a <see cref="TabBar"/>. Clicking it selects it. It can show an icon, a count badge and a close button,
/// and the user can middle-click it to close it or right-click it for a menu.
/// </summary>
public class Tab : Panel
{
    private readonly Label _title;
    private readonly Label _badge;
    private readonly IconPanel _icon;
    private readonly Image _image;
    private readonly Button _close;
    private int? _count;
    private string? _iconSource;
    private Menu? _menu;
    private bool _leftPressed;

    /// <summary>Makes a tab with no text.</summary>
    public Tab()
    {
        AddClass("tab");
        AcceptsFocus = true;
        CanDragScroll = false;
        _icon = Add.Icon(null, "tab-icon");
        _image = AddChild(new Image());
        _image.AddClass("tab-icon");
        _title = Add.Label("", "tab-title");
        _badge = Add.Label("", "tab-count");
        Count = null;
        _close = AddChild(new Button("", "close"));
        _close.RemoveClass("button");
        _close.AddClass("tab-close");
        _close.Tooltip = "Close tab";
        _close.AcceptsFocus = false;
        _close.AddEventListener("onmousedown", e => e.StopPropagation());
        _close.AddEventListener("onclick", e =>
        {
            e.StopPropagation();
            Bar?.CloseTab(this);
        });
        CanClose = false;
        UpdateIcon();
    }

    /// <summary>The bar this tab is in.</summary>
    public TabBar? Bar => Parent as TabBar;

    /// <summary>The text on the tab. It's also the tab's <see cref="Panel.Tooltip"/>, unless you give it another one.</summary>
    [Parameter]
    public string? Text
    {
        get => _title.Text;
        set
        {
            if (string.IsNullOrEmpty(Tooltip) || Tooltip == _title.Text)
            {
                Tooltip = value;
            }

            _title.Text = value;
        }
    }

    /// <summary>A number shown in a badge on the tab, like unread messages. Null hides the badge; zero is shown.</summary>
    [Parameter]
    public int? Count
    {
        get => _count;
        set
        {
            _count = value;
            _badge.Text = value?.ToString(CultureInfo.InvariantCulture) ?? "";
            _badge.Style.Display = value.HasValue ? DisplayMode.Flex : DisplayMode.None;
        }
    }

    /// <summary>Whether the bar is hiding this tab's text to save space, showing only its icon.</summary>
    public bool IsIconOnly => HasClass("icon-only");

    /// <summary>The name of an icon shown before the text. It needs an icon font; see <see cref="IconPanel"/>.</summary>
    [Parameter]
    public string? Icon
    {
        get => _iconSource;
        set
        {
            _iconSource = value;
            _icon.Text = value;
            UpdateIcon();
        }
    }

    /// <summary>A picture shown before the text, in place of <see cref="Icon"/>.</summary>
    [Parameter]
    public Texture? IconTexture
    {
        get => _image.Texture;
        set
        {
            _image.Texture = value;
            UpdateIcon();
        }
    }

    /// <summary>Shows a close button on the tab and lets the user middle-click it to close it.</summary>
    [Parameter]
    public bool CanClose
    {
        get => _close.Style.Display != DisplayMode.None;
        set => _close.Style.Display = value ? DisplayMode.Flex : DisplayMode.None;
    }

    /// <summary>Whether this is the selected tab. The selected tab has the <c>selected</c> class.</summary>
    public bool Selected => HasClass("selected");

    /// <summary>Called with the menu to show when the tab is right-clicked; add options to it. Closable tabs get a Close option too.</summary>
    [Parameter]
    public Action<Menu>? BuildContextMenu { get; set; }

    /// <summary>Whether the tab can be dragged, which is when its bar has <see cref="TabBar.AllowReorder"/> on.</summary>
    public override bool WantsDrag => Bar?.AllowReorder == true;

    private bool HasIcon => IconTexture is not null || !string.IsNullOrWhiteSpace(Icon);

    /// <summary>Clicks the tab on Enter or Space.</summary>
    public override void OnButtonTyped(ButtonEvent e)
    {
        if (TryClickFromKeyboard(e))
        {
            return;
        }

        base.OnButtonTyped(e);
    }

    /// <inheritdoc/>
    public override void OnDeleted()
    {
        _menu?.Delete(true);
        _menu = null;
        base.OnDeleted();
    }

    internal void SetIconOnly(bool value)
    {
        SetClass("icon-only", value && HasIcon);
        _title.Style.Display = IsIconOnly ? DisplayMode.None : DisplayMode.Flex;
    }

    /// <summary>Selects the tab when the left button goes down on it.</summary>
    protected override void OnMouseDown(MousePanelEvent e)
    {
        _leftPressed = e.Button == "mouseleft";
        if (!_leftPressed)
        {
            e.StopPropagation();
        }
        else
        {
            Bar?.SelectTab(this);
        }
    }

    /// <summary>Selects the tab.</summary>
    protected override void OnClick(MousePanelEvent e)
    {
        e.StopPropagation();
        Bar?.SelectTab(this);
    }

    /// <summary>Closes the tab, if it <see cref="CanClose"/>.</summary>
    protected override void OnMiddleClick(MousePanelEvent e)
    {
        e.StopPropagation();
        Bar?.CloseTab(this);
    }

    /// <summary>Opens the tab's menu under the mouse. See <see cref="BuildContextMenu"/>.</summary>
    protected override void OnRightClick(MousePanelEvent e)
    {
        e.StopPropagation();
        _menu?.Delete(true);
        var menu = new Menu();
        _menu = menu;
        BuildContextMenu?.Invoke(menu);
        if (CanClose)
        {
            menu.AddOption("Close", "close", () => Bar?.CloseTab(this));
        }

        if (menu.Options.Count == 0)
        {
            menu.Delete(true);
            _menu = null;
            return;
        }

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

    /// <summary>Starts moving the tab, when its bar allows it.</summary>
    protected override void OnDragStart(DragEvent e)
    {
        e.StopPropagation();
        if (_leftPressed)
        {
            Bar?.BeginReorder(this);
        }
    }

    /// <summary>Shows where the tab would go.</summary>
    protected override void OnDrag(DragEvent e)
    {
        e.StopPropagation();
        Bar?.UpdateReorder(e.ScreenPosition);
    }

    /// <summary>Moves the tab to where it was dropped.</summary>
    protected override void OnDragEnd(DragEvent e)
    {
        e.StopPropagation();
        Bar?.EndReorder(e.ScreenPosition);
    }

    /// <summary>Stops a drag, if one is going on.</summary>
    protected override void OnEscape(PanelEvent e)
    {
        if (Bar?.IsReordering == true)
        {
            Bar.CancelReorder();
            e.StopPropagation();
            return;
        }

        base.OnEscape(e);
    }

    /// <summary>Stops a drag, if one is going on.</summary>
    protected override void OnBlur(PanelEvent e)
    {
        Bar?.CancelReorder();
        base.OnBlur(e);
    }

    private void UpdateIcon()
    {
        _icon.Style.Display = IconTexture is null && !string.IsNullOrWhiteSpace(Icon) ? DisplayMode.Flex : DisplayMode.None;
        _image.Style.Display = IconTexture is not null ? DisplayMode.Flex : DisplayMode.None;
    }
}
