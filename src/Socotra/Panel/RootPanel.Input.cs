namespace Socotra;

public partial class RootPanel
{
    private List<string>? _dragFiles;
    private string? _dragText;
    private Panel? _dragHoverPanel;

    /// <summary>The panel under the mouse, or null.</summary>
    public Panel? Hovered => Input.Hovered;

    /// <summary>
    /// The mouse cursor the panel under the mouse asks for with its <c>cursor</c> style, like <c>pointer</c> or <c>text</c>.
    /// It's null when the mouse isn't over the UI, so the host can show its own cursor.
    /// </summary>
    public string? Cursor => Input.Hovered?.ComputedStyle?.Cursor;

    /// <summary>The panel with input focus, or null. While a panel has focus, the host should have text input turned on.</summary>
    public Panel? Focused => Input.Focused;

    /// <summary>The panel that has captured the mouse, or null. While it's set, hide the cursor, keep it in place and report movement with <see cref="AddMouseDelta"/>.</summary>
    public Panel? MouseCapture => Input.MouseCapture;

    /// <summary>How far the mouse moved since the last frame, in pixels.</summary>
    public Vector2 MouseDelta => Input.CursorDelta;

    /// <summary>The tooltips of the panels in this UI. Set how long they wait before showing here.</summary>
    public TooltipSystem Tooltips => Input.Tooltips;

    internal PanelInput Input { get; }

    /// <summary>Tells the UI where the mouse is, in the same pixels as <see cref="RootPanel.Bounds"/>. Pass null when the mouse leaves or isn't being used, so nothing stays hovered.</summary>
    public void SetMousePosition(Vector2? position) => Input.SetMousePosition(position);

    /// <summary>Tells the UI the mouse moved by <paramref name="delta"/> without the cursor moving, while a panel has the mouse captured.</summary>
    public void AddMouseDelta(Vector2 delta) => Input.AddMouseDelta(delta);

    /// <summary>
    /// Tells the UI a mouse button went down or up. Two clicks within 0.4 seconds and 5 pixels of each other make a
    /// double click, and a third a triple click.
    /// </summary>
    public void SetMouseButton(MouseButtons button, bool down, KeyboardModifiers modifiers = KeyboardModifiers.None) => Input.SetMouseButton(button, down, modifiers);

    /// <summary>Turns the mouse wheel by <paramref name="delta"/> notches. Positive values scroll down and right. With Shift held, turning it up and down scrolls sideways.</summary>
    public void AddMouseWheel(Vector2 delta, KeyboardModifiers modifiers = KeyboardModifiers.None) => Input.AddMouseWheel(delta, modifiers);

    /// <summary>
    /// Sends a key or gamepad button to the focused panel. Pass key repeats as presses too. Unless a panel handles them,
    /// the arrow keys and Tab move focus, Enter and Space click the focused button, and Ctrl+C, Ctrl+X and Ctrl+V use the
    /// <see cref="Clipboard"/>. Escape doesn't arrive as a button: pressing it sends <c>onescape</c> to the focused panel.
    /// </summary>
    public void AddButtonEvent(ButtonEvent e) => Input.AddButtonEvent(e);

    /// <summary>Types text into the focused panel, one character at a time. Pass the text the platform reports as typed, including text an input method editor commits.</summary>
    public void TypeText(string text) => Input.TypeText(text);

    /// <summary>
    /// Tells the focused panel what an input method editor is composing. Pass null or an empty string when the
    /// composition ends, and send the committed text with <see cref="TypeText"/>. Panels hear about it through the
    /// <c>onimestart</c>, <c>onime</c> and <c>onimeend</c> events.
    /// </summary>
    public void SetImeComposition(string? text) => Input.SetImeComposition(text);

    /// <summary>
    /// Closes the open popups in this UI, like a press outside them does. Pass the panel the user clicked to keep the popup
    /// it's in open, along with the popups that one was opened from.
    /// </summary>
    public void ClosePopups(Panel? exceptThisOne = null) => BasePopup.CloseAll(this, exceptThisOne);

    /// <summary>Takes input focus away from whatever has it.</summary>
    public void ClearFocus() => Input.ClearFocus();

    /// <summary>Tells the UI a drag from outside the app, carrying these files or this text, came in over it.</summary>
    public void DragEnter(IEnumerable<string>? files, string? text)
    {
        _dragFiles = files?.ToList();
        _dragText = text;
    }

    /// <summary>
    /// Tells the UI the drag from outside moved to <paramref name="position"/>. Returns what the panel there would do with
    /// it, so you can show the right cursor.
    /// </summary>
    public DropAction DragOver(Vector2 position) => DispatchDrag(position, isDrop: false);

    /// <summary>Tells the UI the drag from outside left without dropping.</summary>
    public void DragLeave()
    {
        NotifyDragLeave();
        _dragFiles = null;
        _dragText = null;
    }

    /// <summary>Drops the payload of the drag from outside at <paramref name="position"/>. Returns what the panel under it did with it.</summary>
    public DropAction Drop(Vector2 position)
    {
        var action = DispatchDrag(position, isDrop: true);
        DragLeave();
        return action;
    }

    /// <summary>Adds a file to a drop from outside, for platforms like SDL that report dropped files one at a time. Finish with <see cref="DropComplete"/>.</summary>
    public void DropFile(string path)
    {
        _dragFiles ??= [];
        _dragFiles.Add(path);
    }

    /// <summary>Sets the text of a drop from outside, for platforms like SDL that report it separately. Finish with <see cref="DropComplete"/>.</summary>
    public void DropText(string text) => _dragText = text;

    /// <summary>Drops what <see cref="DropFile"/> and <see cref="DropText"/> gathered at <paramref name="position"/>. Returns what the panel there did with it.</summary>
    public DropAction DropComplete(Vector2 position) => Drop(position);

    /// <summary>The deepest shown panel at a point, whatever its <c>pointer-events</c>, or null if the point is outside the UI.</summary>
    public Panel? FindPanelAt(Vector2 position) => FindFixedPanelAt(position, needPointerEvents: false) ?? FindVisualPanelAt(position, needPointerEvents: false);

    /// <summary>The deepest shown panel at a point that <paramref name="match"/> accepts. Panels it doesn't accept are looked through, not stopped at.</summary>
    public Panel? FindPanelAt(Vector2 position, Func<Panel, bool> match) =>
        FindFixedPanelAt(position, needPointerEvents: false, match) ?? FindVisualPanelAt(position, needPointerEvents: false, match);

    /// <inheritdoc/>
    public override void OnButtonTyped(ButtonEvent e)
    {
        var moved = e.Pressed && e.Button switch
        {
            "tab" => Input.MoveFocus(Focused, e.HasShift),
            "up" => Input.Navigate(NavigationDirection.Up),
            "down" => Input.Navigate(NavigationDirection.Down),
            "left" => Input.Navigate(NavigationDirection.Left),
            "right" => Input.Navigate(NavigationDirection.Right),
            _ => false,
        };

        if (!moved)
        {
            base.OnButtonTyped(e);
        }
    }

    private DropAction DispatchDrag(Vector2 position, bool isDrop)
    {
        if (_dragFiles is not { Count: > 0 } && string.IsNullOrEmpty(_dragText))
        {
            return DropAction.None;
        }

        var panel = FindPanelAt(position) ?? this;
        if (panel != _dragHoverPanel)
        {
            NotifyDragLeave();
        }

        _dragHoverPanel = panel;
        var e = new DropEvent(panel)
        {
            Files = _dragFiles ?? [],
            Text = _dragText,
            Position = position,
            IsDrop = isDrop,
        };
        panel.DispatchEventImmediate(e);
        return e.Action;
    }

    private void NotifyDragLeave()
    {
        var panel = _dragHoverPanel;
        _dragHoverPanel = null;
        if (panel is null || panel.IsDeleted)
        {
            return;
        }

        panel.DispatchEventImmediate(new DropEvent(panel, "ondragleave")
        {
            Files = _dragFiles ?? [],
            Text = _dragText,
        });
    }
}
