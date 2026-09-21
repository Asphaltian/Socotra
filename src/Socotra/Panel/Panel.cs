using Microsoft.AspNetCore.Components;

namespace Socotra;

/// <summary>A piece of UI, styled with CSS. Everything on screen is a panel, and panels hold other panels.</summary>
public partial class Panel : IComponent, IStyleTarget
{
    private PseudoClass _pseudoClass = PseudoClass.Intro;
    private List<PendingInvoke>? _invokes;

    /// <summary>Makes a panel with no parent.</summary>
    public Panel()
    {
        InitializeEvents();
        UpdateDrawCallbacks();
        LayoutTree = new PanelLayout(this);
        Style = new PanelStyle(this);
        StyleSheet = new StyleSheetCollection(this);
        Transitions = new Transitions(this);
        ElementName = GetType().Name.ToLowerInvariant();
        Switch(PseudoClass.Empty, true);
        LoadStyleSheets();
    }

    /// <summary>Makes a panel inside <paramref name="parent"/>, with the given classes.</summary>
    public Panel(Panel? parent, string? classes = null)
        : this()
    {
        Parent = parent;
        AddClass(classes);
    }

    /// <summary>The element name selectors match, like <c>button</c>. From razor it's the tag name, otherwise the class name in lower case.</summary>
    public string ElementName { get; set; }

    /// <summary>The Razor file the panel's tag is in, if Razor made it.</summary>
    public string? SourceFile { get; set; }

    /// <summary>The line of the Razor file the panel's tag is on, if Razor made it.</summary>
    public int SourceLine { get; set; }

    /// <summary>The id selectors match with <c>#id</c>.</summary>
    public string? Id { get; set; }

    /// <summary>The stylesheets that apply to this panel and everything inside it.</summary>
    public StyleSheetCollection StyleSheet { get; }

    /// <summary>The pseudo-classes this panel has right now, like <see cref="PseudoClass.Hover"/>.</summary>
    public PseudoClass PseudoClass
    {
        get => _pseudoClass;
        set
        {
            if (_pseudoClass == value)
            {
                return;
            }

            _pseudoClass = value;
            StyleSelectorsChanged(true, true);
        }
    }

    /// <summary>Whether this has <c>:hover</c>.</summary>
    public bool HasHovered => (PseudoClass & PseudoClass.Hover) != 0;

    /// <summary>Whether this has <c>:active</c>.</summary>
    public bool HasActive => (PseudoClass & PseudoClass.Active) != 0;

    /// <summary>Whether this has <c>:focus</c>: it has focus, or a panel inside it does.</summary>
    public bool HasFocus => (PseudoClass & PseudoClass.Focus) != 0;

    /// <summary>Whether this has <c>:intro</c>, meaning it was created this frame.</summary>
    public bool HasIntro => (PseudoClass & PseudoClass.Intro) != 0;

    /// <summary>Whether this has <c>:outro</c>, meaning it's being deleted.</summary>
    public bool HasOutro => (PseudoClass & PseudoClass.Outro) != 0;

    /// <summary>Whether this panel is shown, taking its ancestors into account.</summary>
    public bool IsVisible { get; private set; } = true;

    /// <summary>Whether this panel would be shown if its ancestors were.</summary>
    public bool IsVisibleSelf { get; private set; } = true;

    /// <summary>Turns a pseudo-class on or off. Returns false if it already was.</summary>
    public bool Switch(PseudoClass pseudoClass, bool state)
    {
        if (state == ((PseudoClass & pseudoClass) != 0))
        {
            return false;
        }

        PseudoClass = state ? PseudoClass | pseudoClass : PseudoClass & ~pseudoClass;
        return true;
    }

    /// <summary>Called every frame while the panel is shown. Override it for anything that changes over time, like a timer or an animation you drive yourself.</summary>
    public virtual void Tick()
    {
    }

    /// <summary>Called when the panel moves to a different parent.</summary>
    public virtual void OnParentChanged()
    {
    }

    /// <summary>Whether this panel, or anything inside it, is shown and takes the mouse with <c>pointer-events</c>. Hosts use it to decide whether to show the cursor.</summary>
    public virtual bool WantsMouseInput()
    {
        if (ComputedStyle is null || !IsVisibleSelf)
        {
            return false;
        }

        return ComputedStyle.PointerEvents == PointerEvents.All || Children.Any(child => child.WantsMouseInput());
    }

    /// <summary>Converts a point on screen to a position on this panel, where the top left corner is 0, 0 and the bottom right is 1, 1.</summary>
    public Vector2 ScreenPositionToPanelDelta(Vector2 pos)
    {
        pos = ScreenPositionToPanelPosition(pos);
        return new Vector2(pos.X / Box.Rect.Width, pos.Y / Box.Rect.Height);
    }

    /// <summary>Converts a point on screen to this panel's space, where the top left corner is 0, 0.</summary>
    public Vector2 ScreenPositionToPanelPosition(Vector2 pos)
    {
        if (GlobalMatrix is { } matrix)
        {
            pos = matrix.Transform(pos);
        }

        return pos - Box.Rect.Position;
    }

    /// <summary>Converts a point in this panel's space, where the top left corner is 0, 0, to a point on screen.</summary>
    public Vector2 PanelPositionToScreenPosition(Vector2 pos)
    {
        pos += Box.Rect.Position;
        return GlobalMatrixInverted is { } matrix ? matrix.Transform(pos) : pos;
    }

    /// <summary>Returns this panel and everything inside it that's shown and inside <paramref name="box"/> on screen, or only overlapping it if <paramref name="fullyInside"/> is false.</summary>
    public IEnumerable<Panel> FindInRect(Rect box, bool fullyInside)
    {
        if (!IsVisible || !IsInside(box, fullyInside))
        {
            yield break;
        }

        yield return this;
        foreach (var child in Children)
        {
            foreach (var found in child.FindInRect(box, fullyInside))
            {
                yield return found;
            }
        }
    }

    /// <summary>
    /// Calls <paramref name="action"/> after <paramref name="seconds"/>, unless the panel is deleted first. If the panel is
    /// hidden when the time comes, the call waits until it's shown again.
    /// </summary>
    public void Invoke(float seconds, Action action)
    {
        _invokes ??= [];
        _invokes.Add(new PendingInvoke(TimeNow + seconds, null, action));
    }

    /// <summary>
    /// Calls <paramref name="action"/> after <paramref name="seconds"/>, unless the panel is deleted first. Calling it
    /// again with the same <paramref name="name"/> replaces the earlier call, which is handy for things like a search
    /// box that should only react once typing stops. If the panel is hidden when the time comes, the call waits until
    /// it's shown again.
    /// </summary>
    public void InvokeOnce(string name, float seconds, Action action)
    {
        CancelInvoke(name);
        _invokes ??= [];
        _invokes.Add(new PendingInvoke(TimeNow + seconds, name, action));
    }

    /// <summary>Cancels a call waiting from <see cref="InvokeOnce"/>.</summary>
    public void CancelInvoke(string name) => _invokes?.RemoveAll(i => i.Name == name);

    /// <summary>Called when the panel is shown or hidden.</summary>
    protected virtual void OnVisibilityChanged()
    {
    }

    internal void TickInternal()
    {
        if (IsDeleting)
        {
            SetNeedsPreLayout();
            return;
        }

        try
        {
            if (_parentHasChanged)
            {
                _parentHasChanged = false;
                OnParentChanged();
                Style.InvalidateBroadphase();
                StyleSelectorsChanged(true, true);
            }

            bool firstRender = _renderTree is null;
            bool renderedTree = TickRenderTree();
            UpdateBeforeAfterElements();
            UpdateScrollbars();

            if (Style.IsDirty || HasActiveTransitions || ComputedStyle?.IsAnimationActive == true)
            {
                SetNeedsPreLayout();
            }

            if (IsVisible && _children is { Count: > 0 })
            {
                for (int i = _children.Count - 1; _children is not null && i >= 0; i--)
                {
                    if (i < _children.Count)
                    {
                        _children[i].TickInternal();
                    }
                }
            }

            if (renderedTree)
            {
                OnAfterTreeRender(firstRender);
            }

            RunPendingEvents();
            if (IsDeleted)
            {
                return;
            }

            Tick();
            RunPendingEvents();
            AddScrollVelocity();
            RunInvokes();
            RunClassBinds();
        }
        catch (Exception e)
        {
            Log.Error(e);
        }
    }

    internal void UpdateVisibility()
    {
        bool wasVisible = IsVisible;
        IsVisibleSelf = (ComputedStyle?.IsVisible ?? false) || HasActiveTransitions;
        IsVisible = IsVisibleSelf && (Parent?.IsVisible ?? true);
        if (wasVisible == IsVisible)
        {
            return;
        }

        foreach (var child in Children)
        {
            child.UpdateVisibility();
        }

        OnVisibilityChanged();
    }

    private void RunInvokes()
    {
        if (_invokes is not { Count: > 0 })
        {
            return;
        }

        foreach (var invoke in _invokes.Where(i => i.Time <= TimeNow).ToArray())
        {
            if (IsDeleted)
            {
                return;
            }

            if (!_invokes.Remove(invoke))
            {
                continue;
            }

            try
            {
                invoke.Action();
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }
    }

    /// <inheritdoc/>
    public override string ToString() => $"{ElementName}{(Id is null ? "" : $"#{Id}")}{string.Concat(Class.Select(c => $".{c}"))}";

    private sealed record PendingInvoke(double Time, string? Name, Action Action);
}
