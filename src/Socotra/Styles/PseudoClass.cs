namespace Socotra;

/// <summary>The states a panel can be in, which selectors like <c>:hover</c> match. A panel can be in several at once.</summary>
[Flags]
public enum PseudoClass
{
    /// <summary>No pseudo-classes.</summary>
    None = 0,

    /// <summary><c>:hover</c>: the mouse is over the panel.</summary>
    Hover = 1 << 0,

    /// <summary><c>:active</c>: the panel is being pressed.</summary>
    Active = 1 << 1,

    /// <summary><c>:focus</c>: the panel has input focus.</summary>
    Focus = 1 << 2,

    /// <summary><c>:intro</c>: the panel was just created. It only lasts the first frame, so style <c>:intro</c> to set where transitions start from.</summary>
    Intro = 1 << 3,

    /// <summary><c>:outro</c>: the panel is being deleted. It goes away once its transitions finish, so style <c>:outro</c> to animate it out.</summary>
    Outro = 1 << 4,

    /// <summary><c>:empty</c>: the panel has no children.</summary>
    Empty = 1 << 5,

    /// <summary><c>:first-child</c>: the panel is its parent's first child.</summary>
    FirstChild = 1 << 6,

    /// <summary><c>:last-child</c>: the panel is its parent's last child.</summary>
    LastChild = 1 << 7,

    /// <summary><c>:only-child</c>: the panel is its parent's only child.</summary>
    OnlyChild = 1 << 8,

    /// <summary><c>:disabled</c>: the panel doesn't take input.</summary>
    Disabled = 1 << 9,

    /// <summary><c>:focus-visible</c>: the panel has input focus and the keyboard or a gamepad is in use, so the focus should show.</summary>
    FocusVisible = 1 << 10,

    /// <summary><c>::before</c>: an extra element drawn inside a panel, before its children, when a rule styles it.</summary>
    Before = 1 << 11,

    /// <summary><c>::after</c>: an extra element drawn inside a panel, after its children, when a rule styles it.</summary>
    After = 1 << 12,
}
