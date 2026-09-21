using Microsoft.AspNetCore.Components;

namespace Socotra;

/// <summary>
/// A button showing the picked option, which opens a list of <see cref="Options"/> to pick from. It sends
/// <c>onchange</c> and calls <see cref="ValueChanged"/> when the user picks one. In markup it's <c>&lt;select&gt;</c>
/// or <c>&lt;dropdown&gt;</c>, with an <c>&lt;option value="..."&gt;</c> inside for each choice.
/// </summary>
/// <example>
/// <code>
/// // A difficulty picker that starts on Normal
/// var difficulty = new DropDown(settingsPanel);
/// difficulty.Options.Add(new Option("Easy", 0));
/// difficulty.Options.Add(new Option("Normal", 1));
/// difficulty.Options.Add(new Option("Hard", 2));
/// difficulty.Value = 1;
/// difficulty.ValueChanged = value => settings.Difficulty = int.Parse(value);
/// </code>
/// </example>
[StyleSheet.Inline("dropdown", Styles)]
public class DropDown : PopupButton
{
    private const string Styles = """
        .dropdown
        {
            gap: 2px;
            flex-grow: 1;
            cursor: pointer;
            flex-direction: row;
            justify-content: flex-start;
            align-items: center;
            padding: 0px 12px;

            .button-right-column
            {
                flex-grow: 1;
                flex-direction: row;
                align-items: center;
            }
        }

        .button.popupbutton.dropdown
        {
            cursor: pointer;
            transition: all .1s ease-out;
            position: relative;

            > .dropdown_indicator
            {
                position: absolute;
                right: 8px;
            }

            &.open
            {
                border-bottom-left-radius: 1px;
                border-bottom-right-radius: 1px;
                transition: border-radius 0.2s ease-out;
            }
        }

        select
        {
            min-height: 40px;

            > option
            {
                display: none;
            }
        }
        """;

    private List<Option> _options = [];
    private Option? _selected;
    private object? _value;
    private int _valueHash;

    /// <summary>Makes an empty dropdown.</summary>
    public DropDown()
    {
        AddClass("dropdown");
        DropdownIndicator = Add.Icon("expand_more", "dropdown_indicator");
        AcceptsFocus = true;
    }

    /// <summary>Makes an empty dropdown inside <paramref name="parent"/>.</summary>
    public DropDown(Panel parent)
        : this()
    {
        Parent = parent;
    }

    /// <summary>Called with the picked option's value, as text, when the user picks an option.</summary>
    [Parameter]
    public Action<string>? ValueChanged { get; set; }

    /// <summary>Makes the options each time the list opens or the value is set. Use it when the choices change over time.</summary>
    [Parameter]
    public Func<List<Option>>? BuildOptions { get; set; }

    /// <summary>
    /// The options to pick from. Setting a new list shows the option matching <see cref="Value"/> from it. Options added
    /// to the current list show up the next time the list opens.
    /// </summary>
    [Parameter]
    public List<Option> Options
    {
        get => _options;
        set
        {
            _options = value ?? [];
            if (_value is not null && _options.Count > 0)
            {
                Select(_value.ToString(), false);
            }
        }
    }

    /// <summary>
    /// The value of the picked option. Setting it shows the option with that value, matched by its text and ignoring
    /// case, without sending <c>onchange</c>. A <see cref="bool"/> or an enum value fills in <see cref="Options"/> by itself
    /// if there are none yet.
    /// </summary>
    [Parameter]
    public override object? Value
    {
        get => _value;
        set
        {
            if (_valueHash == HashCode.Combine(value))
            {
                return;
            }

            _valueHash = HashCode.Combine(value);
            _value = value;
            if (BuildOptions is not null)
            {
                Options = BuildOptions();
            }

            if (_value is not null && Options.Count == 0)
            {
                PopulateOptionsFromType(_value.GetType());
            }

            Select(_value?.ToString(), false);
        }
    }

    /// <summary>The picked option. Setting it shows that option, sends <c>onchange</c> and calls <see cref="ValueChanged"/>.</summary>
    public Option? Selected
    {
        get => _selected;
        set
        {
            if (_selected == value)
            {
                return;
            }

            _selected = value;
            if (_selected is null)
            {
                return;
            }

            ShowOption(_selected);
            var text = $"{_selected.Value}";
            ValueChanged?.Invoke(text);
            CreateEvent("onchange");
            CreateValueEvent("value", _selected.Value);
        }
    }

    /// <summary>The arrow icon on the right.</summary>
    protected IconPanel DropdownIndicator { get; }

    /// <summary>Opens the list of options under the dropdown.</summary>
    public override void Open()
    {
        Popup = new Popup(this, Popup.PositionMode.BelowStretch, 0) { CloseWhenParentIsHidden = true };
        Popup.AddClass("flat-top");
        if (BuildOptions is not null)
        {
            Options = BuildOptions();
        }

        foreach (var option in Options)
        {
            var button = Popup.AddOption(option.Title, option.Icon, () => Select(option));
            if (button is Button optionButton && Selected is not null && Equals(option.Value, Selected.Value))
            {
                optionButton.Active = true;
            }
        }
    }

    /// <summary>Picks <paramref name="option"/>. With <paramref name="triggerChange"/> off, it's shown without sending <c>onchange</c> or calling <see cref="ValueChanged"/>.</summary>
    protected virtual void Select(Option? option, bool triggerChange = true)
    {
        if (triggerChange)
        {
            Selected = option;
            return;
        }

        _selected = option;
        if (option is not null)
        {
            ShowOption(option);
        }
    }

    /// <summary>Picks the option whose value, as text, is <paramref name="value"/>, ignoring case.</summary>
    protected virtual void Select(string? value, bool triggerChange = true) => Select(Options.FirstOrDefault(option => IsOptionMatch(option, value)), triggerChange);

    /// <summary>Closes the list of options.</summary>
    protected override void OnEscape(PanelEvent e)
    {
        if (ClosePopup())
        {
            e.StopPropagation();
        }
    }

    /// <summary>Reads the <c>&lt;option&gt;</c> children from markup into <see cref="Options"/>.</summary>
    protected override void OnParametersSet()
    {
        if (Children.Any(IsOptionElement))
        {
            Options.Clear();
        }

        foreach (var child in Children.Where(IsOptionElement))
        {
            var title = string.Concat(child.Descendants.OfType<Label>().Select(label => label.Text));
            Options.Add(new Option(title, child.GetAttribute("icon"), child.GetAttribute("value", title)));
        }

        if (BuildOptions is not null)
        {
            Options = BuildOptions();
        }

        if (_value is not null)
        {
            Select(_value.ToString(), false);
        }
    }

    private static bool IsOptionElement(Panel child) => child.ElementName.Equals("option", StringComparison.OrdinalIgnoreCase);

    private static bool IsOptionMatch(Option option, string? value)
    {
        if (option.Value is null || (option.Value is string && string.IsNullOrEmpty(value)))
        {
            return string.IsNullOrEmpty(value);
        }

        return string.Equals(option.Value.ToString(), value, StringComparison.OrdinalIgnoreCase);
    }

    private void PopulateOptionsFromType(Type type)
    {
        if (type == typeof(bool))
        {
            Options.Add(new Option("True", true));
            Options.Add(new Option("False", false));
            return;
        }

        if (type.IsEnum)
        {
            foreach (var value in Enum.GetValues(type))
            {
                Options.Add(new Option(value.ToString(), value));
            }
        }
    }

    private void ShowOption(Option option)
    {
        _selected = option;
        _value = option.Value;
        _valueHash = HashCode.Combine(option.Value);
        Icon = option.Icon;
        Text = option.Title;
    }
}
