namespace Socotra;

/// <summary>A mouse button, for <see cref="RootPanel.SetMouseButton"/>.</summary>
[Flags]
public enum MouseButtons
{
    /// <summary>No button.</summary>
    None = 0,

    /// <summary>The left button.</summary>
    Left = 1 << 0,

    /// <summary>The right button.</summary>
    Right = 1 << 1,

    /// <summary>The middle button, pressing the wheel in.</summary>
    Middle = 1 << 2,

    /// <summary>The back button on the side of the mouse, often called mouse 4.</summary>
    Back = 1 << 3,

    /// <summary>The forward button on the side of the mouse, often called mouse 5.</summary>
    Forward = 1 << 4,
}
