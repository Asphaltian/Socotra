namespace Socotra;

public partial class Panel
{
    private RootPanel? _pendingStyleRoot;
    private RootPanel? _pendingStyleAncestorsRoot;
    private RootPanel? _pendingStyleDescendantsRoot;

    /// <summary>Styles set on this panel alone. They win over every stylesheet.</summary>
    public PanelStyle Style { get; }

    /// <summary>The styles that apply to this panel after the cascade, including transitions and animations in progress. Null until the first layout.</summary>
    public Styles? ComputedStyle { get; private set; }

    internal double TimeNow => FindRootPanel()?.Time ?? 0;

    internal float TimeDelta => FindRootPanel()?.DeltaTime ?? 0;

    /// <summary>Finds the <c>@keyframes</c> rule called <paramref name="name"/> in the stylesheets on this panel and its ancestors.</summary>
    public bool TryFindKeyframe(string name, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out KeyFrames? keyframes)
    {
        foreach (var sheet in AllStyleSheets)
        {
            if (sheet.KeyFrames.TryGetValue(name, out keyframes))
            {
                return true;
            }
        }

        keyframes = null;
        return false;
    }

    /// <summary>
    /// Call it when something your selectors depend on changes, other than classes and pseudo-classes, so the panel's
    /// styles are worked out again. Pass which relatives could be affected too.
    /// </summary>
    protected internal void StyleSelectorsChanged(bool ancestors, bool descendants)
    {
        if (FindRootPanel() is not { } root)
        {
            return;
        }

        if (ancestors && _pendingStyleAncestorsRoot != root)
        {
            _pendingStyleAncestorsRoot = root;
            Parent?.StyleSelectorsChanged(true, false);
        }

        if (descendants && _pendingStyleDescendantsRoot != root)
        {
            _pendingStyleDescendantsRoot = root;
            foreach (var child in Children)
            {
                child.StyleSelectorsChanged(false, true);
            }
        }

        if (_pendingStyleRoot != root)
        {
            _pendingStyleRoot = root;
            root.QueueStyleRules(this);
        }
    }

    internal void MarkStylesRebuilt()
    {
        _pendingStyleRoot = null;
        _pendingStyleAncestorsRoot = null;
        _pendingStyleDescendantsRoot = null;
    }
}
