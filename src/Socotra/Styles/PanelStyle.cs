namespace Socotra;

/// <summary>Styles you set on one panel from code, through <see cref="Panel.Style"/>. They win over any stylesheet.</summary>
public sealed class PanelStyle : Styles
{
    private readonly Panel _panel;
    private readonly Styles _cached = new();
    private readonly Styles _final = new();
    private readonly List<StyleSelector> _activeRules = [];
    private readonly HashSet<StyleBlock> _seen = [];
    private List<StyleBlock>? _candidates;
    private int _broadphaseHash;
    private int _rulesHash;
    private bool _rulesChanged = true;

    internal PanelStyle(Panel panel)
    {
        _panel = panel;
    }

    /// <summary>Whether a stylesheet rule gives this panel a <c>::before</c> element.</summary>
    public bool HasBeforeElement { get; private set; }

    /// <summary>Whether a stylesheet rule gives this panel an <c>::after</c> element.</summary>
    public bool HasAfterElement { get; private set; }

    internal bool IsDirty { get; private set; } = true;

    internal bool SkipTransitions { get; set; } = true;

    /// <inheritdoc/>
    public override void Dirty()
    {
        IsDirty = true;
        _panel.SetNeedsPreLayout();
    }

    internal void InvalidateBroadphase()
    {
        _candidates = null;
        _panel.StyleSelectorsChanged(false, false);
        foreach (var child in _panel.Children)
        {
            child.Style.InvalidateBroadphase();
        }
    }

    internal bool BuildRules()
    {
        bool isBeforeOrAfter = ((IStyleTarget)_panel).IsBeforeOrAfter;
        var subject = isBeforeOrAfter ? _panel.Parent : _panel;
        var hash = subject is null ? 0 : HashCode.Combine(subject.Id, subject.ElementName, subject.ClassHash);
        if (_candidates is null || hash != _broadphaseHash)
        {
            GatherCandidates(subject);
            _broadphaseHash = hash;
        }

        HasBeforeElement = false;
        HasAfterElement = false;
        _activeRules.Clear();
        foreach (var block in _candidates!)
        {
            if (!isBeforeOrAfter)
            {
                HasBeforeElement |= block.HasBefore && block.Test(_panel, PseudoClass.Before) is not null;
                HasAfterElement |= block.HasAfter && block.Test(_panel, PseudoClass.After) is not null;
            }

            if (block.Test(_panel) is { } selector)
            {
                _activeRules.Add(selector);
            }
        }

        _activeRules.Sort(StyleSelector.ByScore);
        var rulesHash = 0;
        foreach (var rule in _activeRules)
        {
            rulesHash = HashCode.Combine(rulesHash, rule);
        }

        _rulesChanged |= rulesHash != _rulesHash;
        _rulesHash = rulesHash;
        return _rulesChanged;
    }

    internal Styles BuildFinal(ref LayoutCascade cascade, out bool changed)
    {
        var now = _panel.TimeNow;
        cascade.SkipTransitions |= SkipTransitions;
        changed = BuildCached(ref cascade);

        if (cascade.SkipTransitions)
        {
            _panel.Transitions.Kill();
            SkipTransitions = false;
        }
        else if (changed)
        {
            _panel.Transitions.Kill(_final);
            _panel.Transitions.Add(_final, _cached, now - _panel.TimeDelta);
        }

        _final.From(_cached);
        _final.ResolveCssWide(cascade.ParentStyles);
        if (cascade.ParentStyles is { } parentStyles)
        {
            _final.Inherit(parentStyles);
        }

        _final.FillDefaults();
        _final.ResolveCurrentColor(cascade.ParentStyles);
        changed |= _panel.Transitions.Run(_final, now);
        changed |= _final.ApplyAnimation(_panel);
        return _final;
    }

    private bool BuildCached(ref LayoutCascade cascade)
    {
        if (!IsDirty && !cascade.SelectorChanged && !_rulesChanged)
        {
            return false;
        }

        IsDirty = false;
        if (_rulesChanged)
        {
            _rulesChanged = false;
            cascade.SelectorChanged = true;
        }

        _cached.From(Default);
        foreach (var rule in _activeRules)
        {
            _cached.Add(rule.Block!.Styles);
        }

        _cached.Add(this);
        _cached.ApplyScale(cascade.Scale);
        return true;
    }

    private void GatherCandidates(Panel? subject)
    {
        _candidates ??= [];
        _candidates.Clear();
        _seen.Clear();
        if (subject is not null)
        {
            foreach (var sheet in _panel.AllStyleSheets)
            {
                sheet.GatherCandidates(subject.Class, _panel, _seen, _candidates);
            }
        }

        _seen.Clear();
    }
}

internal struct LayoutCascade
{
    public bool SelectorChanged;
    public bool ParentChanged;
    public bool SkipTransitions;
    public Styles? ParentStyles;
    public float Scale;
    public bool ClipBackgroundToText;
}
