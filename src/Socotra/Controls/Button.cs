using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Socotra;

/// <summary>
/// A button. It takes focus, so it can be reached with Tab and the arrow keys and clicked with Enter or Space.
/// Give it <see cref="Text"/> and <see cref="Help"/> to show, an <see cref="Href"/> to navigate to, or markup of its own.
/// </summary>
[StyleSheet.Inline("button", Styles)]
public class Button : Panel, INavigationEvent
{
    private const string Styles = """
        button
        {
            cursor: pointer;
        }

        .button
        {
            position: relative;

            > .button-right-column
            {
                flex-direction: column;
            }
        }

        .button > .icon-texture
        {
            width: 1em;
            height: 1em;
            flex-shrink: 0;
            object-fit: contain;
        }

        .button-hover-menu
        {
            position: absolute;
            top: 100%;
            flex-direction: column;

            &.hidden
            {
                opacity: 0;
                pointer-events: none;
            }
        }
        """;

    private Image? _iconImage;
    private string? _icon;
    private NavigationHost? _navigatorCache;

    /// <summary>Makes an empty button.</summary>
    public Button()
    {
        AddClass("button");
        AcceptsFocus = true;

        IconPanel = AddChild(new IconPanel(null, "icon"));
        IconPanel.Style.Display = DisplayMode.None;

        RightColumn = new Panel(this, "button-right-column");
        RightColumn.Style.Display = DisplayMode.None;

        TextLabel = RightColumn.AddChild(new Label { Text = "Empty Label" });
        TextLabel.AddClass("button-label button-text");
        TextLabel.Style.Display = DisplayMode.None;

        HelpLabel = RightColumn.AddChild<Label>("button-help");
        HelpLabel.Style.Display = DisplayMode.None;
    }

    /// <summary>Makes a button showing <paramref name="text"/> that calls <paramref name="action"/> when clicked.</summary>
    public Button(string? text, Action? action = null)
        : this()
    {
        if (text is not null)
        {
            Text = text;
        }

        if (action is not null)
        {
            AddEventListener("onclick", action);
        }
    }

    /// <summary>Makes a button showing <paramref name="text"/> with the icon named <paramref name="icon"/> before it.</summary>
    public Button(string? text, string? icon)
        : this()
    {
        if (icon is not null)
        {
            Icon = icon;
        }

        if (text is not null)
        {
            Text = text;
        }
    }

    /// <summary>Makes a button showing <paramref name="text"/> and the icon named <paramref name="icon"/> that calls <paramref name="onClick"/> when clicked.</summary>
    public Button(string? text, string? icon, Action onClick)
        : this(text, icon)
    {
        AddEventListener("onclick", onClick);
    }

    /// <summary>Makes a button showing <paramref name="text"/> and the icon named <paramref name="icon"/>, with the classes in <paramref name="className"/>, that calls <paramref name="onClick"/> when clicked.</summary>
    public Button(string? text, string? icon, string? className, Action onClick)
        : this(text, icon, onClick)
    {
        AddClass(className);
    }

    /// <summary>The url to navigate the closest <see cref="NavigationHost"/> to when the button is pressed. The button has the <c>active</c> class while that url is showing.</summary>
    [Parameter]
    public string? Href { get; set; }

    /// <summary>A value the button stands for, for things like picking one of a group of buttons.</summary>
    public virtual object? Value { get; set; }

    /// <summary>Markup shown in a menu under the button while it's hovered.</summary>
    [Parameter]
    public RenderFragment? HoverMenu { get; set; }

    /// <summary>Keeps the <c>active</c> class on, whatever <see cref="Href"/> says.</summary>
    [Parameter]
    public bool Active { get; set; }

    /// <summary>The text on the button. It's hidden while empty.</summary>
    [Parameter]
    public string? Text
    {
        get => TextLabel?.Text;
        set
        {
            if (TextLabel is not { IsDeleted: false })
            {
                return;
            }

            if (string.IsNullOrEmpty(value))
            {
                TextLabel.Style.Display = DisplayMode.None;
                TextLabel.Text = "";
                return;
            }

            TextLabel.Style.Display = DisplayMode.Flex;
            RightColumn.Style.Display = DisplayMode.Flex;
            TextLabel.Text = value;
        }
    }

    /// <summary>Smaller text under <see cref="Text"/>, explaining what the button does. It's hidden while empty.</summary>
    [Parameter]
    public string? Help
    {
        get => HelpLabel?.Text;
        set
        {
            if (HelpLabel is not { IsDeleted: false })
            {
                return;
            }

            if (string.IsNullOrEmpty(value))
            {
                HelpLabel.Style.Display = DisplayMode.None;
                HelpLabel.Text = "";
                return;
            }

            HelpLabel.Style.Display = DisplayMode.Flex;
            RightColumn.Style.Display = DisplayMode.Flex;
            HelpLabel.Text = value;
        }
    }

    /// <summary>
    /// The name of an icon shown at the start of the button, like <c>settings</c>. It needs an icon font; see
    /// <see cref="Socotra.IconPanel"/>. <see cref="IconTexture"/> shows instead while it's set.
    /// </summary>
    [Parameter]
    public string? Icon
    {
        get => _icon;
        set
        {
            _icon = value;
            IconPanel.Text = value;
            UpdateIcon();
        }
    }

    /// <summary>A picture shown at the start of the button, in place of <see cref="Icon"/>. Set it to null to take it away.</summary>
    [Parameter]
    public Texture? IconTexture
    {
        get => _iconImage?.Texture;
        set
        {
            if (value is not null && _iconImage is not { IsDeleted: false })
            {
                _iconImage = AddChild(new Image());
                _iconImage.AddClass("icon icon-texture");
                SetChildIndex(_iconImage, 0);
            }

            if (_iconImage is { IsDeleted: false } image)
            {
                image.Texture = value;
            }

            UpdateIcon();
        }
    }

    /// <summary>The icon panel showing <see cref="Icon"/>.</summary>
    protected IconPanel IconPanel { get; }

    /// <summary>The label showing <see cref="Text"/>.</summary>
    protected Label? TextLabel { get; set; }

    /// <summary>The label showing <see cref="Help"/>.</summary>
    protected Label? HelpLabel { get; set; }

    /// <summary>The column holding the text and help labels.</summary>
    protected Panel RightColumn { get; set; }

    /// <summary>Clicks the button on Enter or Space, unless it's disabled.</summary>
    public override void OnButtonTyped(ButtonEvent e)
    {
        if (!Disabled && TryClickFromKeyboard(e))
        {
            return;
        }

        base.OnButtonTyped(e);
    }

    /// <summary>Deletes the label showing <see cref="Text"/>, for buttons that show something else.</summary>
    public void DeleteText()
    {
        TextLabel?.Delete();
        TextLabel = null;
    }

    /// <summary>Takes away the <see cref="Icon"/> and the <see cref="IconTexture"/>.</summary>
    public void DeleteIcon()
    {
        Icon = null;
        IconTexture = null;
    }

    /// <summary>Sets <see cref="Text"/>.</summary>
    public virtual void SetText(string? text) => Text = text;

    /// <summary>Acts as if the button was clicked.</summary>
    public void Click() => CreateClick();

    /// <summary>Sets <c>text</c>, <c>html</c>, <c>icon</c>, <c>href</c> and <c>active</c> from markup.</summary>
    public override void SetProperty(string name, string? value)
    {
        switch (name)
        {
            case "text" or "html":
                SetText(value);
                return;
            case "icon":
                Icon = value;
                return;
            case "href":
                Href = value;
                return;
            case "active":
                SetClass("active", Translation.ToBool(value));
                return;
        }

        base.SetProperty(name, value);
    }

    /// <summary>Sets <see cref="Text"/> from the text between the button's tags.</summary>
    public override void SetContent(string? value) => SetText(value?.Trim() ?? "");

    /// <inheritdoc/>
    public override void Tick()
    {
        base.Tick();
        UpdateActiveState();
    }

    /// <summary>Turns the <c>active</c> class on or off from <see cref="Active"/> and whether <see cref="Href"/> is showing.</summary>
    protected void UpdateActiveState()
    {
        if (Active)
        {
            SetClass("active", true);
            return;
        }

        if (string.IsNullOrWhiteSpace(Href))
        {
            SetClass("active", false);
            return;
        }

        _navigatorCache ??= Ancestors.OfType<NavigationHost>().FirstOrDefault();
        SetClass("active", _navigatorCache?.CurrentUrlMatches(Href) ?? false);
    }

    /// <summary>Navigates to <see cref="Href"/>, if it has one.</summary>
    protected override void OnMouseDown(MousePanelEvent e)
    {
        base.OnMouseDown(e);
        if (!string.IsNullOrWhiteSpace(Href))
        {
            this.Navigate(Href);
            e.StopPropagation();
        }
    }

    /// <summary>Builds the <see cref="HoverMenu"/>.</summary>
    protected override void BuildRenderTree(RenderTreeBuilder tree)
    {
        if (HoverMenu is null)
        {
            return;
        }

        tree.OpenElement<Panel>(0);
        tree.AddAttribute(1, "class", HasHovered ? "button-hover-menu" : "button-hover-menu hidden");
        HoverMenu(tree);
        tree.CloseElement();
    }

    /// <inheritdoc/>
    protected override string? GetRenderTreeChecksum() => $"{BuildHash()}";

    /// <inheritdoc/>
    protected override int BuildHash() => HashCode.Combine(HoverMenu, HasHovered);

    private void UpdateIcon()
    {
        var hasTexture = IconTexture is not null;
        var hasGlyph = !string.IsNullOrEmpty(Icon);
        IconPanel.Style.Display = !hasTexture && hasGlyph ? DisplayMode.Flex : DisplayMode.None;
        if (_iconImage is { IsDeleted: false } image)
        {
            image.Style.Display = hasTexture ? DisplayMode.Flex : DisplayMode.None;
        }

        SetClass("has-icon", hasTexture || hasGlyph);
    }

    void INavigationEvent.OnNavigated(string? url) => UpdateActiveState();
}
