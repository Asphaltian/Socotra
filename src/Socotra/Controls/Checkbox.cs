using Microsoft.AspNetCore.Components;

namespace Socotra;

/// <summary>
/// A box the user can tick and untick by clicking it, or with Enter or Space while it has focus. It has the
/// <c>checked</c> class while ticked, and sends <c>onchange</c> with the new state, then <c>onchecked</c> or <c>onunchecked</c>.
/// </summary>
/// <example>
/// <code>
/// // A labeled option that turns the music on and off
/// var music = new Checkbox { Parent = options, LabelText = "Music", Checked = true };
/// music.ValueChanged = on => audio.MusicEnabled = on;
/// </code>
/// </example>
public class Checkbox : Panel
{
    private bool _checked;

    /// <summary>Makes an unticked checkbox with no label.</summary>
    public Checkbox()
    {
        AddClass("checkbox");
        AcceptsFocus = true;
        CheckMark = Add.Icon("check", "checkmark");
    }

    /// <summary>Called with the new state when the user ticks or unticks the box.</summary>
    [Parameter]
    public Action<bool>? ValueChanged { get; set; }

    /// <summary>The tick shown while the box is checked. It's an <see cref="IconPanel"/> with the <c>checkmark</c> class unless a subclass swaps it.</summary>
    public Panel CheckMark { get; protected set; }

    /// <summary>Whether the box is ticked.</summary>
    [Parameter]
    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value)
            {
                return;
            }

            _checked = value;
            OnValueChanged();
        }
    }

    /// <summary>The same as <see cref="Checked"/>, for binding to.</summary>
    [Parameter]
    public bool Value
    {
        get => Checked;
        set => Checked = value;
    }

    /// <summary>The label showing <see cref="LabelText"/>. Null until you set some text.</summary>
    public Label? Label { get; protected set; }

    /// <summary>Text shown next to the box.</summary>
    [Parameter]
    public string? LabelText
    {
        get => Label?.Text;
        set
        {
            if (Label is not { IsDeleted: false })
            {
                Label = Add.Label();
            }

            Label.Text = value;
        }
    }

    /// <summary>Toggles the box on Enter or Space.</summary>
    public override void OnButtonTyped(ButtonEvent e)
    {
        if (TryClickFromKeyboard(e))
        {
            return;
        }

        base.OnButtonTyped(e);
    }

    /// <summary>Sets <c>checked</c>, <c>value</c> and <c>text</c> from markup.</summary>
    public override void SetProperty(string name, string? value)
    {
        base.SetProperty(name, value);
        switch (name)
        {
            case "checked" or "value":
                Checked = Translation.ToBool(value);
                break;
            case "text":
                LabelText = value;
                break;
        }
    }

    /// <summary>Sets <see cref="LabelText"/> from the text between the checkbox's tags.</summary>
    public override void SetContent(string? value) => LabelText = value?.Trim() ?? "";

    /// <summary>Called when <see cref="Checked"/> changes. It updates the classes and sends <c>onchange</c>, then <c>onchecked</c> or <c>onunchecked</c>.</summary>
    public virtual void OnValueChanged()
    {
        UpdateState();
        CreateEvent("onchange", Checked);
        CreateEvent(Checked ? "onchecked" : "onunchecked");
    }

    /// <summary>Updates how the checkbox looks for its state. By default it turns the <c>checked</c> class on or off.</summary>
    protected virtual void UpdateState() => SetClass("checked", Checked);

    /// <summary>Toggles the box and calls <see cref="ValueChanged"/>.</summary>
    protected override void OnClick(MousePanelEvent e)
    {
        base.OnClick(e);
        Checked = !Checked;
        CreateValueEvent("checked", Checked);
        CreateValueEvent("value", Checked);
        e.StopPropagation();
        ValueChanged?.Invoke(Checked);
    }

    /// <summary>Keeps the press from reaching the checkbox's parents.</summary>
    protected override void OnMouseDown(MousePanelEvent e) => e.StopPropagation();
}
