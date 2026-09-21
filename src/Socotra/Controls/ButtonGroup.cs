using Microsoft.AspNetCore.Components;

namespace Socotra;

/// <summary>
/// A row of buttons where one is picked at a time. The picked one has the <c>active</c> class. Fill it from
/// <see cref="Options"/>, or add buttons yourself and give each a <see cref="Button.Value"/>.
/// </summary>
/// <example>
/// <code>
/// // Three quality presets, starting on Medium
/// var quality = new ButtonGroup { Parent = settingsPanel };
/// quality.Options = [new Option("Low", "low"), new Option("Medium", "medium"), new Option("High", "high")];
/// quality.Value = "medium";
/// quality.ValueChanged = value => settings.Quality = (string?)value;
/// </code>
/// </example>
public class ButtonGroup : Panel
{
    private object? _value;
    private Panel? _selected;

    /// <summary>Makes an empty button group.</summary>
    public ButtonGroup()
    {
        AddClass("buttongroup");
    }

    /// <summary>Called with the new <see cref="Value"/> when it changes.</summary>
    [Parameter]
    public Action<object?>? ValueChanged { get; set; }

    /// <summary>The value of the picked button. Setting it picks the button with that value, and sends <c>onchange</c>.</summary>
    [Parameter]
    public object? Value
    {
        get => _value;
        set
        {
            if (Equals(_value, value))
            {
                return;
            }

            _value = value;
            ValueChanged?.Invoke(_value);
            CreateEvent("onchange");
            CreateValueEvent("value", _value);
            SetSelectedButton();
        }
    }

    /// <summary>The choices to show, one button each. Set it before the group first updates, or from markup.</summary>
    [Parameter]
    public List<Option>? Options { get; set; }

    /// <summary>Classes added to each button the group makes.</summary>
    [Parameter]
    public string ButtonClass { get; set; } = "";

    /// <summary>The picked button. Setting it moves the <c>active</c> class to it and sends it <c>startactive</c>, and sends the one it replaces <c>stopactive</c>.</summary>
    public Panel? SelectedButton
    {
        get => _selected;
        set
        {
            if (_selected == value)
            {
                return;
            }

            if (_selected is Button oldButton)
            {
                oldButton.Active = false;
            }

            _selected?.RemoveClass("active");
            _selected?.CreateEvent("stopactive");
            _selected = value;
            if (_selected is Button newButton)
            {
                newButton.Active = true;
                if (newButton.Value is not null)
                {
                    Value = newButton.Value;
                }
            }

            _selected?.AddClass("active");
            _selected?.CreateEvent("startactive");
        }
    }

    /// <summary>Adds a button showing <paramref name="value"/> that calls <paramref name="action"/> when clicked.</summary>
    public Button AddButton(string value, Action action)
    {
        var button = AddChild(new Button(value, action));
        button.AddClass(ButtonClass);
        return button;
    }

    /// <summary>Adds a button showing <paramref name="value"/> that calls <paramref name="action"/> with true when it's picked and false when another one is.</summary>
    public Button AddButtonActive(string value, Action<bool> action)
    {
        var button = AddChild(new Button(value));
        button.AddClass(ButtonClass);
        button.AddEventListener("startactive", () => action(true));
        button.AddEventListener("stopactive", () => action(false));
        return button;
    }

    /// <inheritdoc/>
    public override void Tick()
    {
        base.Tick();
        if (Options is not null)
        {
            return;
        }

        foreach (var button in ChildrenOfType<Button>())
        {
            button.Active = button.Value is not null && Equals(button.Value, _value);
        }
    }

    /// <summary>Makes clicking <paramref name="child"/> pick it.</summary>
    protected override void OnChildAdded(Panel child)
    {
        base.OnChildAdded(child);
        child.AddEventListener("onclick", () => SelectedButton = child);
        if (child.HasClass("active"))
        {
            SelectedButton = child;
        }
    }

    /// <summary>Makes a button for each of the <see cref="Options"/>.</summary>
    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        if (Options is null)
        {
            return;
        }

        DeleteChildren();
        foreach (var option in Options)
        {
            var button = AddButton(option.Title ?? "", () => Value = option.Value);
            button.Value = option.Value;
        }

        SetSelectedButton();
    }

    private void SetSelectedButton()
    {
        if (Options is null)
        {
            return;
        }

        for (int i = 0; i < Options.Count; i++)
        {
            if (string.Equals(Options[i].Value?.ToString(), _value?.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                SelectedButton = Children.ElementAtOrDefault(i);
            }
        }
    }
}
