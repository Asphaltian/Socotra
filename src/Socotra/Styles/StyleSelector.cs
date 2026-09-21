namespace Socotra;

internal sealed class StyleSelector
{
    public StyleBlock? Block;
    public string AsString = "";
    public string[]? Classes;
    public string? Element;
    public string? Id;
    public PseudoClass Flags;
    public StyleSelector? Parent;
    public StyleSelector? Not;
    public bool ImmediateParent;
    public bool AdjacentSibling;
    public bool GeneralSibling;
    public bool IsScope;
    public StyleSelector[]? AnyOf;
    public StyleSelector[]? DescendantOf;
    public StyleSelector[]? Has;
    public Func<IStyleTarget, bool>? NthChild;
    public int SelfScore;

    public static readonly Comparison<StyleSelector> ByScore = static (a, b) => a.Score.CompareTo(b.Score);

    public int Score => Block is null ? SelfScore : Block.LoadOrder + (SelfScore * 100000);

    public void Finalize(StyleBlock block)
    {
        Block = block;
        UpdateScore();
    }

    private int UpdateScore()
    {
        SelfScore = 0;
        if (Id is not null)
        {
            SelfScore += 1000;
        }

        if (Element is not null)
        {
            SelfScore += 1;
        }

        SelfScore += ((Classes?.Length ?? 0) + System.Numerics.BitOperations.PopCount((uint)Flags) + (NthChild is null ? 0 : 1)) * 10;
        SelfScore += Not?.UpdateScore() ?? 0;
        SelfScore += AnyOf is { Length: > 0 } ? AnyOf.Max(s => s.UpdateScore()) : 0;
        SelfScore += DescendantOf is { Length: > 0 } ? DescendantOf.Max(s => s.UpdateScore()) : 0;
        SelfScore += Parent?.UpdateScore() ?? 0;
        return SelfScore;
    }

    public bool TestBroadphase(IStyleTarget target) =>
        (Element is null || target.ElementName == Element)
        && (Id is null || string.Equals(target.Id, Id, StringComparison.OrdinalIgnoreCase))
        && (Classes is null || target.HasClasses(Classes));

    public bool Test(IStyleTarget target, PseudoClass forceFlag = PseudoClass.None, IStyleTarget? scope = null)
    {
        if (IsScope)
        {
            return target == scope;
        }

        var pseudo = target.PseudoClass | forceFlag;
        if (((pseudo & PseudoClass.Before) != 0 && (Flags & PseudoClass.Before) == 0) || ((pseudo & PseudoClass.After) != 0 && (Flags & PseudoClass.After) == 0))
        {
            return false;
        }

        if (Flags != PseudoClass.None && (pseudo & Flags) != Flags)
        {
            return false;
        }

        if (NthChild is not null && !NthChild(target))
        {
            return false;
        }

        if (!TestBroadphase(target))
        {
            return false;
        }

        if (Parent is not null)
        {
            if (AdjacentSibling)
            {
                if (PreviousSibling(target) is not { } previous || !Parent.Test(previous, scope: scope))
                {
                    return false;
                }
            }
            else if (GeneralSibling)
            {
                var sibling = PreviousSibling(target);
                while (sibling is not null && !Parent.Test(sibling, scope: scope))
                {
                    sibling = PreviousSibling(sibling);
                }

                if (sibling is null)
                {
                    return false;
                }
            }
            else if (!Parent.TestAncestor(target.Parent, !ImmediateParent, scope))
            {
                return false;
            }
        }

        if (Has is { Length: > 0 } && !Has.Any(h => TestHas(target, h)))
        {
            return false;
        }

        if (DescendantOf is not null && !DescendantOf.Any(s => s.TestAncestor(target.Parent, !ImmediateParent, null)))
        {
            return false;
        }

        if (AnyOf is not null && !AnyOf.Any(s => s.Test(target)))
        {
            return false;
        }

        return Not is null || !Not.Test(target);
    }

    private bool TestAncestor(IStyleTarget? target, bool recursive, IStyleTarget? scope)
    {
        for (; target is not null; target = recursive ? target.Parent : null)
        {
            if (Test(target, scope: scope))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TestHas(IStyleTarget target, StyleSelector selector) =>
        HasCandidates(target, selector).Any(candidate => selector.Test(candidate, scope: target));

    private static IEnumerable<IStyleTarget> HasCandidates(IStyleTarget target, StyleSelector selector)
    {
        var first = selector;
        var deep = false;
        while (first.Parent is { IsScope: false } parent)
        {
            deep |= !first.AdjacentSibling && !first.GeneralSibling;
            first = parent;
        }

        var single = first == selector;
        if (!first.AdjacentSibling && !first.GeneralSibling)
        {
            return single && first.ImmediateParent ? target.Children : Descendants(target);
        }

        IEnumerable<IStyleTarget> siblings = single && first.AdjacentSibling
            ? NextSibling(target) is { } next ? [next] : []
            : FollowingSiblings(target);
        return deep ? siblings.SelectMany(sibling => Descendants(sibling).Prepend(sibling)) : siblings;
    }

    private static IEnumerable<IStyleTarget> Descendants(IStyleTarget target) =>
        target.Children.SelectMany(child => Descendants(child).Prepend(child));

    private static IEnumerable<IStyleTarget> FollowingSiblings(IStyleTarget target)
    {
        for (var sibling = NextSibling(target); sibling is not null; sibling = NextSibling(sibling))
        {
            yield return sibling;
        }
    }

    private static IStyleTarget? PreviousSibling(IStyleTarget target) =>
        target.Parent is { } parent && target.SiblingIndex > 0 ? parent.Children[target.SiblingIndex - 1] : null;

    private static IStyleTarget? NextSibling(IStyleTarget target) =>
        target.Parent is { } parent && target.SiblingIndex + 1 < parent.Children.Count ? parent.Children[target.SiblingIndex + 1] : null;
}
