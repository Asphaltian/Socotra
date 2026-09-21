namespace Socotra;

/// <summary>
/// A button that opens a <see cref="Socotra.Popup"/> when clicked, and closes it when clicked again. Derive from it and
/// build the popup in <see cref="Open"/>.
/// While the popup is up, the button has the <c>open</c> and <c>active</c> classes and the popup is as wide as the button.
/// </summary>
public abstract class PopupButton : Button
{
    /// <summary>Makes a popup button.</summary>
    protected PopupButton()
    {
        AddClass("popupbutton");
    }

    /// <summary>The popup <see cref="Open"/> made. Set it there.</summary>
    protected Popup? Popup { get; set; }

    /// <summary>Whether the popup is open.</summary>
    protected bool IsOpen => Popup is { IsDeleted: false, IsDeleting: false };

    /// <summary>Opens the popup. Make it here and keep it in <see cref="Popup"/>.</summary>
    public abstract void Open();

    /// <inheritdoc/>
    public override void Tick()
    {
        base.Tick();
        var open = IsOpen;
        SetClass("open", open);
        SetClass("active", open);
        if (Popup is { IsDeleted: false } popup)
        {
            popup.Style.Width = Box.Rect.Width * ScaleFromScreen;
        }
    }

    /// <summary>Opens the popup, or closes it if it's open.</summary>
    protected override void OnClick(MousePanelEvent e)
    {
        base.OnClick(e);
        if (!ClosePopup())
        {
            Open();
        }
    }

    /// <summary>Closes the popup if it's open, and returns whether it was.</summary>
    protected bool ClosePopup()
    {
        if (!IsOpen)
        {
            return false;
        }

        Popup!.Delete();
        return true;
    }
}
