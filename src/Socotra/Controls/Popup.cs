using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components;

namespace Socotra;

/// <summary>
/// A panel that floats next to the panel that opened it, above everything else, and closes when the user clicks
/// somewhere else. Give it options with <see cref="AddOption(string?, Action?)"/> or fill it with panels of your own.
/// </summary>
/// <example>
/// <code>
/// // A small menu under a button, closed by picking an option or clicking elsewhere
/// var popup = new Popup(button, Popup.PositionMode.BelowLeft, 4);
/// popup.AddOption("Rename", () => StartRename());
/// popup.AddOption("Delete", "delete", () => DeleteItem());
/// </code>
/// </example>
[StyleSheet.Inline("popup", Styles)]
public partial class Popup : BasePopup
{
    private const string Styles = """
        .popup-panel
        {
            position: absolute;
            z-index: 2000;
            flex-direction: column;
            min-height: 20px;
            min-width: 10px;
            overflow: scroll;
            pointer-events: all;
        }

        .popup-panel.below-center, .popup-panel.above-center
        {
            transform: translateX( -50% );
        }

        .popup-panel.left, .popup-panel.right
        {
            transform: translateY( -50% );
        }
        """;

    /// <summary>Makes a popup that isn't placed anywhere yet. Call <see cref="SetPositioning"/> to show it next to a panel.</summary>
    public Popup()
    {
    }

    /// <summary>Makes a popup and shows it next to <paramref name="sourcePanel"/>, <paramref name="offset"/> pixels away on the side <paramref name="position"/> picks.</summary>
    public Popup(Panel sourcePanel, PositionMode position, float offset)
    {
        SetPositioning(sourcePanel, position, offset);
    }

    /// <summary>Where a <see cref="Popup"/> goes, next to the panel that opened it.</summary>
    public enum PositionMode
    {
        /// <summary>To the left, centered vertically.</summary>
        Left,

        /// <summary>To the right, centered vertically.</summary>
        Right,

        /// <summary>To the left, lined up with the bottom edge.</summary>
        LeftBottom,

        /// <summary>To the right, lined up with the bottom edge.</summary>
        RightBottom,

        /// <summary>To the right, lined up with the top edge. Where a submenu goes.</summary>
        RightTop,

        /// <summary>Above, lined up with the left edge.</summary>
        AboveLeft,

        /// <summary>Above, lined up with the right edge.</summary>
        AboveRight,

        /// <summary>Below, lined up with the left edge.</summary>
        BelowLeft,

        /// <summary>Below, centered horizontally.</summary>
        BelowCenter,

        /// <summary>Below, lined up with the right edge.</summary>
        BelowRight,

        /// <summary>Below, as wide as the panel that opened it.</summary>
        BelowStretch,

        /// <summary>Above, centered horizontally.</summary>
        AboveCenter,

        /// <summary>Where the mouse is when the popup opens.</summary>
        UnderMouse,
    }

    /// <summary>
    /// A rectangle on screen to open next to instead of the whole <see cref="PopupSource"/>, like the caret in a text box.
    /// The popup goes above or below it, whichever side has room.
    /// </summary>
    public Rect? AnchorRect { get; set; }

    /// <summary>The panel that opened this popup. Keys the popup doesn't use go to it.</summary>
    [Parameter]
    public Panel? PopupSource { get; set; }

    /// <summary>The option picked with <see cref="MoveSelection"/>, or null.</summary>
    public Panel? SelectedChild { get; set; }

    /// <summary>Where the popup goes next to <see cref="PopupSource"/>.</summary>
    [Parameter]
    public PositionMode Position { get; set; }

    /// <summary>How far the popup sits from <see cref="PopupSource"/>, in pixels.</summary>
    [Parameter]
    public float PopupSourceOffset { get; set; }

    /// <summary>Closes the popup when <see cref="PopupSource"/> is deleted.</summary>
    [Parameter]
    public bool CloseWhenParentIsHidden { get; set; }

    /// <summary>Shows an unselectable header with this text at the top of the popup, after the <see cref="Icon"/>.</summary>
    [Parameter]
    public string? Title
    {
        get => TitleLabel?.Text;
        set
        {
            CreateHeader();
            TitleLabel.Text = value;
        }
    }

    /// <summary>Shows an unselectable header with the icon of this name at the top of the popup, before the <see cref="Title"/>.</summary>
    [Parameter]
    public string? Icon
    {
        get => IconPanel?.Text;
        set
        {
            CreateHeader();
            IconPanel.Text = value;
        }
    }

    /// <summary>The header holding <see cref="TitleLabel"/> and <see cref="IconPanel"/>, once <see cref="Title"/> or <see cref="Icon"/> is set.</summary>
    protected Panel? Header { get; set; }

    /// <summary>The label showing <see cref="Title"/>.</summary>
    protected Label? TitleLabel { get; set; }

    /// <summary>The icon panel showing <see cref="Icon"/>.</summary>
    protected IconPanel? IconPanel { get; set; }

    /// <summary>
    /// Shows the popup next to <paramref name="sourcePanel"/>, <paramref name="offset"/> pixels away on the side
    /// <paramref name="position"/> picks. It adds the <c>popup-panel</c> class and a class for the position, like <c>below-left</c>.
    /// </summary>
    public void SetPositioning(Panel sourcePanel, PositionMode position, float offset)
    {
        PopupSource = sourcePanel;
        Position = position;
        PopupSourceOffset = offset;

        AddClass("popup-panel");
        Parent = sourcePanel.FindPopupPanel();
        PositionMe(true);

        AddClass(position switch
        {
            PositionMode.Left => "left",
            PositionMode.Right => "right",
            PositionMode.LeftBottom => "left-bottom",
            PositionMode.RightBottom => "right-bottom",
            PositionMode.RightTop => "right-top",
            PositionMode.AboveLeft => "above-left",
            PositionMode.AboveCenter => "above-center",
            PositionMode.AboveRight => "above-right",
            PositionMode.BelowLeft => "below-left",
            PositionMode.BelowCenter => "below-center",
            PositionMode.BelowRight => "below-right",
            PositionMode.BelowStretch => "below-stretch",
            _ => null,
        });
    }

    /// <summary>Closes the open popups and adds the <c>success</c> class to this one, for styling how it goes away.</summary>
    public void Success()
    {
        AddClass("success");
        FindRootPanel()?.ClosePopups();
    }

    /// <summary>Closes the open popups and adds the <c>failure</c> class to this one, for styling how it goes away.</summary>
    public void Failure()
    {
        AddClass("failure");
        FindRootPanel()?.ClosePopups();
    }

    /// <summary>Adds a button showing <paramref name="text"/>. Clicking it closes the open popups, then calls <paramref name="action"/>.</summary>
    public Panel AddOption(string? text, Action? action = null) => AddChild(new Button(text, () =>
    {
        FindRootPanel()?.ClosePopups();
        action?.Invoke();
    }));

    /// <summary>Adds a button showing <paramref name="text"/> and the icon named <paramref name="icon"/>. Clicking it closes the open popups, then calls <paramref name="action"/>.</summary>
    public Panel AddOption(string? text, string? icon, Action? action = null) => AddChild(new Button(text, icon, () =>
    {
        FindRootPanel()?.ClosePopups();
        action?.Invoke();
    }));

    /// <summary>
    /// Moves <see cref="SelectedChild"/> to the next option, or the previous one when <paramref name="dir"/> is negative.
    /// It wraps around at the ends and skips the header.
    /// </summary>
    public void MoveSelection(int dir)
    {
        var options = Children.Where(child => child != Header && child is not ScrollBar && !child.IsDeleting).ToList();
        if (options.Count == 0)
        {
            return;
        }

        var index = SelectedChild is { } selected ? options.IndexOf(selected) : -1;
        index = index < 0 ? (dir > 0 ? 0 : options.Count - 1) : (((index + dir) % options.Count) + options.Count) % options.Count;
        Highlight(SelectedChild, false);
        SelectedChild = options[index];
        Highlight(SelectedChild, true);
    }

    private static void Highlight(Panel? child, bool on)
    {
        if (child is Button button)
        {
            button.Active = on;
        }
        else
        {
            child?.SetClass("active", on);
        }
    }

    /// <inheritdoc/>
    public override void Tick()
    {
        base.Tick();
        if (IsDeleted)
        {
            return;
        }

        if (CloseWhenParentIsHidden && PopupSource is not { IsDeleted: false })
        {
            Delete();
            return;
        }

        PositionMe(false);
    }

    /// <summary>Passes keys the popup doesn't use to <see cref="PopupSource"/>, or to the parent when it has none.</summary>
    public override void OnButtonTyped(ButtonEvent e)
    {
        if (PopupSource is { IsDeleted: false } source)
        {
            source.OnButtonTyped(e);
        }
        else
        {
            base.OnButtonTyped(e);
        }
    }

    /// <summary>Passes Escape on to <see cref="PopupSource"/>, so a dropdown closes its list when Escape is pressed inside it.</summary>
    protected override void OnEscape(PanelEvent e)
    {
        if (PopupSource is not { IsDeleted: false } source)
        {
            base.OnEscape(e);
            return;
        }

        e.StopPropagation();
        source.CreateEvent("onescape");
    }

    /// <summary>Moves the popup back inside the screen when it would hang off the right or bottom edge.</summary>
    protected override void OnLayout(ref Rect rect)
    {
        if (FindRootPanel() is not { } root || root.Box.Rect.Width < 1 || root.Box.Rect.Height < 1)
        {
            return;
        }

        const float padding = 10;
        var bottom = root.Box.Rect.Bottom - padding;
        var right = root.Box.Rect.Right - padding;
        if (rect.Bottom > bottom)
        {
            rect.Top -= rect.Bottom - bottom;
            rect.Bottom = bottom;
        }

        if (rect.Right > right)
        {
            rect.Left -= rect.Right - right;
            rect.Right = right;
        }
    }

    [MemberNotNull(nameof(TitleLabel), nameof(IconPanel))]
    private void CreateHeader()
    {
        if (Header is { IsDeleted: false } && TitleLabel is not null && IconPanel is not null)
        {
            return;
        }

        Header = Add.Panel("header");
        IconPanel = Header.Add.Icon(null);
        TitleLabel = Header.Add.Label(null, "title");
    }

    private void PositionMe(bool isInitial)
    {
        if (PopupSource is not { } source || Parent is not { } container)
        {
            return;
        }

        var scale = source.ScaleFromScreen;
        var origin = container.Box.Rect.Position;
        var size = container.Box.Rect.Size * scale;
        if (AnchorRect is { } anchor)
        {
            var bounds = new Rect(Vector2.Zero, size);
            var position = AnchorPosition((anchor - origin) * scale, Box.Rect.Size * scale, bounds, Position, PopupSourceOffset);
            Style.Left = position.X;
            Style.Top = position.Y;
            Style.MaxWidth = bounds.Width;
            Style.MaxHeight = bounds.Height;
            return;
        }

        var rect = (source.Box.Rect - origin) * scale;
        if (size.Y > 100)
        {
            Style.MaxHeight = size.Y - 50;
        }

        switch (Position)
        {
            case PositionMode.Left:
                Style.Left = null;
                Style.Right = size.X - rect.Left + PopupSourceOffset;
                Style.Top = rect.Top + (rect.Height * 0.5f);
                break;
            case PositionMode.Right:
                Style.Right = null;
                Style.Left = rect.Right + PopupSourceOffset;
                Style.Top = rect.Top + (rect.Height * 0.5f);
                break;
            case PositionMode.RightBottom:
                Style.Right = null;
                Style.Left = rect.Right + PopupSourceOffset;
                Style.Top = null;
                Style.Bottom = size.Y - rect.Bottom;
                break;
            case PositionMode.LeftBottom:
                Style.Left = null;
                Style.Right = size.X - rect.Left + PopupSourceOffset;
                Style.Top = null;
                Style.Bottom = size.Y - rect.Bottom;
                break;
            case PositionMode.RightTop:
                Style.Right = null;
                Style.Left = rect.Right + PopupSourceOffset;
                Style.Top = rect.Top;
                break;
            case PositionMode.AboveLeft:
                Style.Left = rect.Left;
                Style.Bottom = size.Y - rect.Top + PopupSourceOffset;
                break;
            case PositionMode.AboveCenter:
                Style.Left = rect.Left + (rect.Width * 0.5f);
                Style.Bottom = size.Y - rect.Top + PopupSourceOffset;
                break;
            case PositionMode.AboveRight:
                Style.Left = null;
                Style.Right = size.X - rect.Right;
                Style.Bottom = size.Y - rect.Top + PopupSourceOffset;
                break;
            case PositionMode.BelowLeft:
                Style.Left = rect.Left;
                Style.Top = rect.Bottom + PopupSourceOffset;
                break;
            case PositionMode.BelowCenter:
                Style.Left = rect.Center.X;
                Style.Top = rect.Bottom + PopupSourceOffset;
                break;
            case PositionMode.BelowRight:
                Style.Left = null;
                Style.Right = size.X - rect.Right;
                Style.Top = rect.Bottom + PopupSourceOffset;
                break;
            case PositionMode.BelowStretch:
                Style.Left = rect.Left;
                Style.Width = rect.Width;
                Style.Top = rect.Bottom + PopupSourceOffset;
                break;
            case PositionMode.UnderMouse when isInitial:
                var mouse = (source.ScreenMousePosition - origin) * scale;
                Style.Left = mouse.X;
                Style.Top = mouse.Y + (PopupSourceOffset * scale);
                break;
        }
    }
}
