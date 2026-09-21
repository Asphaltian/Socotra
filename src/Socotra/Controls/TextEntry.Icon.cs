namespace Socotra;

public partial class TextEntry
{
    private bool _hasClearButton;

    /// <summary>The icon panel showing <see cref="Icon"/>. Null when there's no icon.</summary>
    public IconPanel? IconPanel { get; protected set; }

    /// <summary>The name of an icon to show at the end of the entry, from the icon font. Adds the <c>has-icon</c> class. See <see cref="Socotra.IconPanel"/>.</summary>
    public string? Icon
    {
        get => IconPanel?.Text;
        set
        {
            _hasClearButton = false;
            IconPanel?.RemoveClass("clearbutton");
            SetIcon(value);
        }
    }

    /// <summary>
    /// Shows a <c>cancel</c> icon that clears the text when clicked, unless the entry is read-only or disabled. The icon
    /// has the <c>clearbutton</c> class, and setting <see cref="Icon"/> replaces it.
    /// </summary>
    public bool HasClearButton
    {
        get => _hasClearButton;
        set
        {
            if (_hasClearButton == value)
            {
                return;
            }

            _hasClearButton = value;
            SetIcon(value ? "cancel" : null);
            IconPanel?.AddClass("clearbutton");
        }
    }

    private void SetIcon(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            IconPanel?.Delete(true);
            IconPanel = null;
        }
        else
        {
            if (IconPanel is null)
            {
                IconPanel = AddChild<IconPanel>(value);
                IconPanel.AddEventListener("onclick", ClearFromButton);
            }

            IconPanel.Text = value;
        }

        SetClass("has-icon", IconPanel is not null);
    }

    private void ClearFromButton()
    {
        if (!_hasClearButton || !CanEdit || TextLength == 0)
        {
            return;
        }

        RecordEdit(EditKind.Single);
        Text = "";
        OnValueChanged();
    }
}
