namespace Socotra;

internal sealed class InputEventQueue
{
    private readonly Queue<Action<Panel>> _focusedInput = new();
    private readonly Queue<string> _doubleClicks = new();
    private readonly Queue<string> _tripleClicks = new();
    private KeyboardModifiers _keyboardModifiers;
    private bool _mouseMoved;

    public static string NormalizeButtonName(string button)
    {
        button = button.ToLowerInvariant();
        return button.StartsWith("key_", StringComparison.Ordinal) ? button[4..] : button;
    }

    public void TickFocused(Func<Panel> focused, Action settleFocus)
    {
        while (_focusedInput.TryDequeue(out var send))
        {
            send(focused());
            settleFocus();
        }
    }

    public void Tick(Panel? hovered, Panel? active)
    {
        if (_mouseMoved && (active ?? hovered) is { } moved)
        {
            moved.CreateEvent(new MousePanelEvent("onmousemove", moved, "none"));
        }

        _mouseMoved = false;
        while (_doubleClicks.TryDequeue(out var button))
        {
            hovered?.CreateEvent(new MousePanelEvent("ondoubleclick", hovered, button));
        }

        while (_tripleClicks.TryDequeue(out var button))
        {
            hovered?.CreateEvent(new MousePanelEvent("ontripleclick", hovered, button));
        }
    }

    public void AddDoubleClick(string button) => _doubleClicks.Enqueue(NormalizeButtonName(button));

    public void AddTripleClick(string button) => _tripleClicks.Enqueue(NormalizeButtonName(button));

    public void AddButtonEvent(ButtonEvent e)
    {
        _keyboardModifiers = e.KeyboardModifiers;
        _focusedInput.Enqueue(panel => panel.OnButtonEvent(e));
    }

    public void AddButtonTyped(ButtonEvent e)
    {
        _keyboardModifiers = e.KeyboardModifiers;
        if (!AddClipboardShortcut(e))
        {
            _focusedInput.Enqueue(panel => panel.OnButtonTyped(e));
        }
    }

    public void AddKeyTyped(char c)
    {
        var modifiers = _keyboardModifiers;
        _focusedInput.Enqueue(panel => panel.OnKeyTyped(c, modifiers));
    }

    public void AddEscape() => AddFocusedEvent(new PanelEvent("onescape"));

    public void MouseMoved() => _mouseMoved = true;

    private bool AddClipboardShortcut(ButtonEvent e)
    {
        if (e.KeyboardModifiers != KeyboardModifiers.Ctrl)
        {
            return false;
        }

        switch (e.Button)
        {
            case "c":
                AddFocusedEvent(new CopyEvent());
                return true;
            case "x":
                AddFocusedEvent(new CutEvent());
                return true;
            case "v":
                if (Clipboard.GetText() is { Length: > 0 } text)
                {
                    AddFocusedEvent(new PasteEvent(text));
                }

                return true;
            default:
                return false;
        }
    }

    private void AddFocusedEvent(PanelEvent e) => _focusedInput.Enqueue(panel =>
    {
        e.Target = panel;
        panel.DispatchEventImmediate(e);
    });
}
