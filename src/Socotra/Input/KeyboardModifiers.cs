namespace Socotra;

/// <summary>The modifier keys held with a button or mouse event. Combine them with <c>|</c>.</summary>
[Flags]
public enum KeyboardModifiers
{
    /// <summary>No modifier keys.</summary>
    None = 0,

    /// <summary>Alt.</summary>
    Alt = 1 << 0,

    /// <summary>Ctrl.</summary>
    Ctrl = 1 << 1,

    /// <summary>Shift.</summary>
    Shift = 1 << 2,
}
