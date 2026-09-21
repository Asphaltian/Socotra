using Microsoft.AspNetCore.Components;

namespace Socotra;

/// <summary>
/// Picks a value of an enum. Up to four values show as a row of buttons, more as a <see cref="DropDown"/>. When the user
/// picks one, it sends <c>onchange</c>.
/// </summary>
/// <example>
/// <code>
/// // Pick the window mode
/// var mode = new EnumControl { Parent = row, Value = settings.WindowMode };
/// mode.ValueChanged = value =&gt; settings.WindowMode = (WindowMode)value;
/// </code>
/// </example>
[StyleSheet.Inline("enumcontrol", Styles)]
public class EnumControl : Panel
{
    private const string Styles = """
        EnumControl
        {
            gap: 2px;
            flex-grow: 1;
        }

        EnumControl DropDown,
        EnumControl ButtonGroup
        {
            border-radius: 8px;
            background-color: #000a;
            flex-grow: 1;
        }

        EnumControl DropDown
        {
            flex-grow: 1;
            min-height: 32px;
        }

        EnumControl ButtonGroup
        {
            border-radius: 12px;
            overflow: hidden;
            min-height: 32px;

            Button
            {
                flex-grow: 1;
                justify-content: center;
                align-items: center;
                gap: 4px;
                color: #aaa;
                font-size: 1rem;
                cursor: pointer;

                .icon
                {
                    color: #08f;
                }

                &:hover
                {
                    color: #ddd;

                    .icon
                    {
                        color: #3af;
                    }
                }

                &:active
                {
                    background-color: #04a;
                    color: white;
                    transform: translateX( 1px ) translateY( 1px );

                    .icon
                    {
                        color: #fff;
                    }
                }

                &.active
                {
                    background-color: #08f;
                    color: white;
                    pointer-events: none;

                    .icon
                    {
                        color: #fff;
                    }
                }
            }
        }
        """;

    private Enum? _value;
    private ButtonGroup? _buttons;
    private DropDown? _dropDown;

    /// <summary>
    /// The picked value. Its enum decides the choices, so a value of another enum shows that enum's values instead.
    /// Setting it doesn't call <see cref="ValueChanged"/>.
    /// </summary>
    [Parameter]
    public Enum? Value
    {
        get => _value;
        set
        {
            if (Equals(_value, value))
            {
                return;
            }

            var rebuild = _value?.GetType() != value?.GetType();
            _value = value;
            if (rebuild)
            {
                Rebuild();
            }
            else if (_buttons is not null)
            {
                _buttons.Value = value;
            }
            else if (_dropDown is not null)
            {
                _dropDown.Value = value;
            }
        }
    }

    /// <summary>Called with the new <see cref="Value"/> when the user picks one.</summary>
    [Parameter]
    public Action<Enum>? ValueChanged { get; set; }

    private void Rebuild()
    {
        _buttons?.Delete(true);
        _dropDown?.Delete(true);
        _buttons = null;
        _dropDown = null;
        if (_value is null)
        {
            return;
        }

        var options = Enum.GetValues(_value.GetType());
        if (options.Length <= 4)
        {
            CreateButtonGroup(options);
        }
        else
        {
            CreateDropDown(options);
        }
    }

    private void CreateDropDown(Array options)
    {
        var dropDown = AddChild(new DropDown());
        foreach (Enum option in options)
        {
            dropDown.Options.Add(new Option(option.ToString(), option));
        }

        dropDown.Value = _value;
        dropDown.ValueChanged = _ => Picked(dropDown.Value as Enum);
        _dropDown = dropDown;
    }

    private void CreateButtonGroup(Array options)
    {
        var group = AddChild(new ButtonGroup());
        group.Value = _value;
        foreach (Enum option in options)
        {
            var button = group.AddChild(new Button());
            button.Text = option.ToString();
            button.Value = option;
        }

        group.ValueChanged = value => Picked(value as Enum);
        _buttons = group;
    }

    private void Picked(Enum? value)
    {
        if (value is null || Equals(_value, value))
        {
            return;
        }

        _value = value;
        ValueChanged?.Invoke(value);
        CreateEvent("onchange", value);
        CreateValueEvent("value", value);
    }
}
