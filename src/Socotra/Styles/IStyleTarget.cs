namespace Socotra;

/// <summary>Anything stylesheets can style, like a panel or an element inside rich text.</summary>
public interface IStyleTarget
{
    /// <summary>The element name that selectors like <c>button</c> match.</summary>
    string ElementName { get; }

    /// <summary>The id that selectors like <c>#title</c> match.</summary>
    string? Id { get; }

    /// <summary>The pseudo-classes it has right now, like <see cref="Socotra.PseudoClass.Hover"/>.</summary>
    PseudoClass PseudoClass { get; }

    /// <summary>What it sits inside, or null at the top.</summary>
    IStyleTarget? Parent { get; }

    /// <summary>What sits directly inside it, in order.</summary>
    IReadOnlyList<IStyleTarget> Children { get; }

    /// <summary>Its position among its siblings, starting at 0.</summary>
    int SiblingIndex { get; }

    /// <summary>Whether it has every one of <paramref name="classes"/>.</summary>
    bool HasClasses(string[] classes);

    internal bool IsBeforeOrAfter => (PseudoClass & (PseudoClass.Before | PseudoClass.After)) != 0;
}
