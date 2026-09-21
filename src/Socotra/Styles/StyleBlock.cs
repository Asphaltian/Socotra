namespace Socotra;

internal sealed class StyleBlock(string fileName, int fileLine)
{
    public StyleSelector[] Selectors { get; private set; } = [];

    public Styles Styles { get; set; } = new();

    public string FileName => fileName;

    public int FileLine => fileLine;

    public int LoadOrder { get; set; }

    public bool HasBefore { get; private set; }

    public bool HasAfter { get; private set; }

    public void SetSelector(string selector, StyleBlock? parent)
    {
        Selectors = [.. StyleParser.Selector(selector, parent)];
        foreach (var s in Selectors)
        {
            s.Finalize(this);
            HasBefore |= (s.Flags & PseudoClass.Before) != 0;
            HasAfter |= (s.Flags & PseudoClass.After) != 0;
        }
    }

    public StyleSelector? Test(IStyleTarget target, PseudoClass forceFlag = PseudoClass.None)
    {
        if (target.IsBeforeOrAfter)
        {
            forceFlag = target.PseudoClass & (PseudoClass.Before | PseudoClass.After);
            if (target.Parent is not { } owner)
            {
                return null;
            }

            target = owner;
        }

        foreach (var selector in Selectors)
        {
            if (selector.Test(target, forceFlag))
            {
                return selector;
            }
        }

        return null;
    }

    public bool TestBroadphase(IStyleTarget target) =>
        (target.IsBeforeOrAfter ? target.Parent : target) is { } subject && Selectors.Any(s => s.TestBroadphase(subject));
}
