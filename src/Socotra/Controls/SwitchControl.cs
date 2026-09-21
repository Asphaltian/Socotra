using Microsoft.AspNetCore.Components;

namespace Socotra;

/// <summary>
/// An on/off switch: a knob that slides across a track when the user presses it. It has the <c>active</c> class while
/// on and <c>inactive</c> while off, so a stylesheet can restyle the <c>.switch-frame</c> track and <c>.switch-inner</c> knob.
/// When the user flips it, it sends <c>onchange</c>.
/// </summary>
/// <example>
/// <code>
/// // A labeled switch for fullscreen mode
/// var fullscreen = new SwitchControl { Parent = options, Label = "Fullscreen" };
/// fullscreen.OnValueChanged = on => window.SetFullscreen(on);
/// </code>
/// </example>
[StyleSheet.Inline("switchcontrol", Styles)]
public class SwitchControl : Panel
{
    private const string Styles = """
        .switchcontrol
        {
            flex-direction: row;
            align-items: center;
            flex-shrink: 0;
            cursor: pointer;

            .switch-frame
            {
                flex-shrink: 0;
                width: 32px;
                height: 18px;
                padding: 2px;
                flex-direction: row;
                align-items: center;
                border-radius: 9px;
                background-color: #ffffff20;
                transition: background-color 0.15s ease-out;

                .switch-inner
                {
                    width: 14px;
                    height: 14px;
                    border-radius: 7px;
                    background-color: #8a8f98;
                    transition: margin-left 0.15s ease-out, background-color 0.15s ease-out;
                }
            }

            .switch-label
            {
                margin-left: 9px;
                font-size: 12px;
            }

            &.active
            {
                .switch-frame { background-color: #3273eb; }
                .switch-inner { margin-left: 14px; background-color: #ffffff; }
            }
        }
        """;

    private Label? _labelPanel;
    private bool _value;

    /// <summary>Makes a switch that starts off.</summary>
    public SwitchControl()
    {
        AddClass("switchcontrol");
        var frame = Add.Panel("switch-frame");
        frame.Add.Panel("switch-inner");
        UpdateState();
    }

    /// <summary>Called with the new state when the user flips the switch.</summary>
    [Parameter]
    public Action<bool>? OnValueChanged { get; set; }

    /// <summary>Text shown next to the switch. Set it to null or empty to take it away.</summary>
    [Parameter]
    public string? Label
    {
        get => _labelPanel?.Text;
        set
        {
            if (string.IsNullOrEmpty(value))
            {
                _labelPanel?.Delete(true);
                _labelPanel = null;
                return;
            }

            _labelPanel ??= Add.Label("", "switch-label");
            _labelPanel.Text = value;
        }
    }

    /// <summary>Whether the switch is on.</summary>
    [Parameter]
    public bool Value
    {
        get => _value;
        set
        {
            if (_value == value)
            {
                return;
            }

            _value = value;
            UpdateState();
        }
    }

    /// <summary>Flips the switch, calls <see cref="OnValueChanged"/> and sends <c>onchange</c>.</summary>
    protected override void OnMouseDown(MousePanelEvent e)
    {
        base.OnMouseDown(e);
        Value = !Value;
        OnValueChanged?.Invoke(Value);
        CreateEvent("onchange", Value);
        CreateValueEvent("value", Value);
        e.StopPropagation();
    }

    private void UpdateState()
    {
        SetClass("active", _value);
        SetClass("inactive", !_value);
    }
}
