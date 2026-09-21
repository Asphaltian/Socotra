namespace Socotra;

internal sealed partial class PanelInput
{
    private Panel? _nextFocus;
    private bool _focusChanging;

    public Panel? Focused { get; private set; }

    public bool SetFocus(Panel? panel)
    {
        for (; panel is not null; panel = panel.Parent)
        {
            if (panel.AcceptsFocus)
            {
                _nextFocus = panel;
                _focusChanging = true;
                return true;
            }
        }

        return false;
    }

    public bool ClearFocus(Panel panel)
    {
        _nextFocus = null;
        _focusChanging = true;
        SetFocus(panel.Parent);
        return true;
    }

    public void ClearFocus()
    {
        _nextFocus = null;
        _focusChanging = Focused is not null;
    }

    public bool MoveFocus(Panel? from, bool backwards)
    {
        var order = new List<Panel>();
        CollectTabOrder(root, from, order);
        order = [.. order.OrderBy(p => p.TabIndex > 0 ? p.TabIndex : int.MaxValue)];

        int index = from is null ? -1 : order.IndexOf(from);
        var target = index < 0
            ? backwards ? order.LastOrDefault() : order.FirstOrDefault()
            : order[(index + (backwards ? -1 : 1) + order.Count) % order.Count];

        return MoveFocusTo(target);
    }

    public bool Navigate(NavigationDirection direction)
    {
        if (Focused is null)
        {
            var autofocus = root.Descendants.FirstOrDefault(p => p.GetAttribute("autofocus") is not null && IsNavigable(p));
            return autofocus is not null ? MoveFocusTo(autofocus) : MoveFocus(null, backwards: direction is NavigationDirection.Up or NavigationDirection.Left);
        }

        var candidates = new List<Panel>();
        CollectTabOrder(root, null, candidates);

        var from = NavigationSpan.Of(Focused.Box.Rect, direction);
        Panel? best = null;
        var bestScore = (Distance: float.PositiveInfinity, Offset: float.PositiveInfinity);
        foreach (var candidate in candidates)
        {
            if (candidate == Focused || candidate.Box.Rect.Width <= 0 || candidate.Box.Rect.Height <= 0)
            {
                continue;
            }

            var to = NavigationSpan.Of(candidate.Box.Rect, direction);
            if (to.Center <= from.Center || to.End <= from.End)
            {
                continue;
            }

            var along = MathF.Max(0, to.Start - from.End);
            var across = MathF.Max(0, MathF.Max(to.CrossStart - from.CrossEnd, from.CrossStart - to.CrossEnd));
            var score = (Distance: (along * along) + (4 * across * across), Offset: MathF.Abs(to.CrossCenter - from.CrossCenter));
            if (score.Distance < bestScore.Distance || (score.Distance == bestScore.Distance && score.Offset < bestScore.Offset))
            {
                best = candidate;
                bestScore = score;
            }
        }

        return MoveFocusTo(best);
    }

    private static void CollectTabOrder(Panel panel, Panel? always, List<Panel> order)
    {
        if (panel == always || IsNavigable(panel))
        {
            order.Add(panel);
        }
        else if (!panel.IsVisible || panel.Disabled)
        {
            return;
        }

        foreach (var child in panel.Children)
        {
            CollectTabOrder(child, always, order);
        }
    }

    private static bool IsNavigable(Panel panel) => CanFocus(panel) && panel.TabIndex >= 0;

    private static bool CanFocus(Panel panel) =>
        panel.AcceptsFocus && panel.IsVisible && !panel.IsDeleting && !panel.IsDeleted && !IsDisabled(panel);

    private bool MoveFocusTo(Panel? target)
    {
        if (target is null || target == Focused)
        {
            return false;
        }

        SetFocus(target);
        target.ScrollAncestorsIntoView();
        return true;
    }

    private void ReleaseFocus(Panel subtree)
    {
        if (_nextFocus?.IsAncestor(subtree) == true)
        {
            _nextFocus = null;
            _focusChanging = false;
        }

        if (Focused is not { } focused || !focused.IsAncestor(subtree))
        {
            return;
        }

        Focused = null;
        SwitchWithAncestors(PseudoClass.Focus, false, focused);
        focused.Switch(PseudoClass.FocusVisible, false);
        focused.CreateEvent("onblur");
    }

    private void TickFocus()
    {
        if (Focused is not null && !CanFocus(Focused) && (!_focusChanging || _nextFocus == Focused))
        {
            _nextFocus = null;
            _focusChanging = true;
        }

        if (_focusChanging && _nextFocus is not null && !CanFocus(_nextFocus))
        {
            _nextFocus = null;
            _focusChanging = false;
        }

        if (_focusChanging && Focused != _nextFocus)
        {
            if (Focused is { IsDeleted: false } previous)
            {
                SwitchWithAncestors(PseudoClass.Focus, false, previous, _nextFocus);
                previous.Switch(PseudoClass.FocusVisible, false);
                previous.CreateEvent("onblur");
            }

            Focused = _nextFocus;
            if (Focused is { } next)
            {
                SwitchWithAncestors(PseudoClass.Focus, true, next);
                next.CreateEvent("onfocus");
            }
        }

        _nextFocus = null;
        _focusChanging = false;
    }
}

internal readonly record struct NavigationSpan(float Start, float End, float CrossStart, float CrossEnd)
{
    public float Center => (Start + End) * 0.5f;

    public float CrossCenter => (CrossStart + CrossEnd) * 0.5f;

    public static NavigationSpan Of(Rect rect, NavigationDirection direction) => direction switch
    {
        NavigationDirection.Up => new(-rect.Bottom, -rect.Top, rect.Left, rect.Right),
        NavigationDirection.Down => new(rect.Top, rect.Bottom, rect.Left, rect.Right),
        NavigationDirection.Left => new(-rect.Right, -rect.Left, rect.Top, rect.Bottom),
        _ => new(rect.Left, rect.Right, rect.Top, rect.Bottom),
    };
}

internal enum NavigationDirection
{
    Up,
    Down,
    Left,
    Right,
}
