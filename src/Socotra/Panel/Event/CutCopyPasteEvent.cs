namespace Socotra;

/// <summary>Sent to the focused panel when Ctrl+C is pressed. Whatever <see cref="Panel.GetClipboardValue"/> returns goes on the clipboard.</summary>
public class CopyEvent : PanelEvent
{
    internal CopyEvent()
        : base("copy")
    {
    }
}

/// <summary>Sent to the focused panel when Ctrl+X is pressed. Whatever <see cref="Panel.GetClipboardValue"/> returns goes on the clipboard.</summary>
public class CutEvent : PanelEvent
{
    internal CutEvent()
        : base("cut")
    {
    }
}

/// <summary>Sent to the focused panel when Ctrl+V is pressed while there's text on the clipboard. You get the text in <see cref="Panel.OnPaste"/>.</summary>
public class PasteEvent : PanelEvent
{
    internal PasteEvent(string value)
        : base("paste")
    {
        ClipboardValue = value;
    }

    /// <summary>The text being pasted.</summary>
    public string ClipboardValue { get; set; }
}
