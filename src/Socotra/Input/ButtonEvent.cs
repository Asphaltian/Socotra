namespace Socotra;

/// <summary>
/// A key or button going down or up. Pass one to <see cref="RootPanel.AddButtonEvent"/> and the focused panel
/// gets it. Buttons have lower case names, like <c>up</c>, <c>enter</c>, <c>escape</c>, <c>tab</c>, <c>a</c> or <c>mouseleft</c>.
/// </summary>
public record ButtonEvent
{
    /// <summary>A button going down when <paramref name="pressed"/> is true, or up when it's false. Case doesn't matter in the name, and a <c>key_</c> prefix is dropped, so <c>KEY_A</c> becomes <c>a</c>.</summary>
    public ButtonEvent(string button, bool pressed, KeyboardModifiers keyboardModifiers = KeyboardModifiers.None, int virtualKey = 0)
    {
        Button = InputEventQueue.NormalizeButtonName(button);
        Pressed = pressed;
        KeyboardModifiers = keyboardModifiers;
        VirtualKey = virtualKey;
    }

    /// <summary>The button's name.</summary>
    public string Button { get; }

    /// <summary>True when it went down or repeated, false when it came back up.</summary>
    public bool Pressed { get; }

    /// <summary>The platform's key code for the button, or 0 if you didn't pass one.</summary>
    public int VirtualKey { get; }

    /// <summary>The modifier keys held at the time.</summary>
    public KeyboardModifiers KeyboardModifiers { get; }

    /// <summary>Whether Shift was held.</summary>
    public bool HasShift => (KeyboardModifiers & KeyboardModifiers.Shift) != 0;

    /// <summary>Whether Ctrl was held.</summary>
    public bool HasCtrl => (KeyboardModifiers & KeyboardModifiers.Ctrl) != 0;

    /// <summary>Whether Alt was held.</summary>
    public bool HasAlt => (KeyboardModifiers & KeyboardModifiers.Alt) != 0;

    /// <summary>Set to true once you've handled the button, so the panel's parents leave it alone.</summary>
    public bool StopPropagation { get; set; }

    /// <inheritdoc/>
    public override string ToString() => $"{Button} {(Pressed ? "pressed" : "released")}";
}
