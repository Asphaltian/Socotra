namespace Socotra;

public partial class Panel
{
    private Panel? _parent;
    private List<Panel>? _children;
    private bool _parentHasChanged;
    private bool _indexesDirty;

    /// <summary>Quick ways to add common panels as children, like <c>Add.Panel("header")</c>.</summary>
    public PanelCreator Add => new(this);

    /// <summary>
    /// The panel this one is inside. Setting it moves the panel. Labels and images can't have children, so
    /// setting one of them as the parent puts this panel just after it instead.
    /// </summary>
    public Panel? Parent
    {
        get => _parent;
        set
        {
            if (value == this)
            {
                throw new InvalidOperationException("A panel can't be its own parent.");
            }

            if (this is RootPanel && value is not null)
            {
                throw new InvalidOperationException("A root panel can't have a parent.");
            }

            if (_parent == value)
            {
                return;
            }

            if (value is Label or Image)
            {
                Parent = value.Parent;
                Parent?.SetChildIndex(this, Parent.GetChildIndex(value) + 1);
                return;
            }

            if (FindRootPanel() is { } root && root != value?.FindRootPanel())
            {
                root.Input.ReleaseSubtree(this);
            }

            _parent?.RemoveChild(this);
            _parent = value;
            _parent?.InsertChild(this);
            _parentHasChanged = true;
            if (ComputedStyle is not null)
            {
                UpdateVisibility();
            }
        }
    }

    /// <summary>The panels directly inside this one, in order.</summary>
    public IReadOnlyList<Panel> Children => _children ?? [];

    IStyleTarget? IStyleTarget.Parent => StyleParent;

    internal virtual Panel? StyleParent => Parent;

    IReadOnlyList<IStyleTarget> IStyleTarget.Children => Children;

    /// <summary>Whether this panel has any children.</summary>
    public bool HasChildren => _children is { Count: > 0 };

    /// <summary>How many children this panel has.</summary>
    public int ChildrenCount => _children?.Count ?? 0;

    /// <summary>This panel's position among its siblings.</summary>
    public int SiblingIndex { get; private set; } = -1;

    /// <summary>This panel, its parent, its parent's parent, and so on up.</summary>
    public IEnumerable<Panel> AncestorsAndSelf
    {
        get
        {
            for (Panel? p = this; p is not null; p = p.Parent)
            {
                yield return p;
            }
        }
    }

    /// <summary>This panel's parent, its parent, and so on up.</summary>
    public IEnumerable<Panel> Ancestors
    {
        get
        {
            for (var p = Parent; p is not null; p = p.Parent)
            {
                yield return p;
            }
        }
    }

    /// <summary>Everything inside this panel, depth first.</summary>
    public IEnumerable<Panel> Descendants
    {
        get
        {
            foreach (var child in Children)
            {
                yield return child;
                foreach (var descendant in child.Descendants)
                {
                    yield return descendant;
                }
            }
        }
    }

    /// <summary>The root panel this one is in, if any.</summary>
    public RootPanel? FindRootPanel() => this as RootPanel ?? Parent?.FindRootPanel();

    /// <summary>The top-most ancestor, the one with no parent.</summary>
    public virtual Panel FindPopupPanel() => Parent?.FindPopupPanel() ?? this;

    /// <summary>Whether <paramref name="panel"/> is this panel or one of its ancestors.</summary>
    public bool IsAncestor(Panel? panel)
    {
        for (Panel? p = this; p is not null; p = p.Parent)
        {
            if (p == panel)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Where <paramref name="panel"/> is among this panel's children, or -1 if it isn't one of them.</summary>
    public int GetChildIndex(Panel? panel) => panel is not null && panel.Parent == this && _children is not null ? _children.IndexOf(panel) : -1;

    /// <summary>The child at <paramref name="index"/>, or null. With <paramref name="loop"/>, indexes wrap around, so -1 is the last child.</summary>
    public Panel? GetChild(int index, bool loop = false)
    {
        if (_children is not { Count: > 0 })
        {
            return null;
        }

        if (loop)
        {
            index = ((index % _children.Count) + _children.Count) % _children.Count;
        }

        return index >= 0 && index < _children.Count ? _children[index] : null;
    }

    /// <summary>The children that are a <typeparamref name="T"/>, last first.</summary>
    public IEnumerable<T> ChildrenOfType<T>()
        where T : Panel
    {
        for (int i = ChildrenCount - 1; i >= 0; i--)
        {
            if (_children![i] is T t)
            {
                yield return t;
            }
        }
    }

    /// <summary>Puts <paramref name="p"/> inside this panel and returns it.</summary>
    public T AddChild<T>(T p)
        where T : Panel
    {
        p.Parent = this;
        return p;
    }

    /// <summary>Makes a <typeparamref name="T"/> inside this panel, with the given classes.</summary>
    public T AddChild<T>(string? classnames = null)
        where T : Panel, new()
    {
        var child = new T { Parent = this };
        child.AddClass(classnames);
        return child;
    }

    /// <summary>Makes a <typeparamref name="T"/> inside this panel, with the given classes, and hands it back through <paramref name="outPanel"/>. Always returns true.</summary>
    public bool AddChild<T>(out T outPanel, string? classnames = null)
        where T : Panel, new()
    {
        outPanel = AddChild<T>(classnames);
        return true;
    }

    /// <summary>Deletes every child.</summary>
    public void DeleteChildren(bool immediate = false)
    {
        foreach (var child in Children.ToArray())
        {
            child.Delete(immediate);
        }
    }

    /// <summary>Moves <paramref name="child"/> to <paramref name="index"/> among its siblings.</summary>
    public void SetChildIndex(Panel child, int index)
    {
        if (_children is null || child.Parent != this)
        {
            return;
        }

        index = Math.Clamp(index, 0, _children.Count - 1);
        int current = _children.IndexOf(child);
        if (current == index)
        {
            return;
        }

        _children.RemoveAt(current);
        _children.Insert(index, child);
        LayoutTree.RemoveChild(child.LayoutTree);
        LayoutTree.InsertChild(index, child.LayoutTree);
        ChildrenMoved();
        _indexesDirty = true;
        SetNeedsPreLayout();
    }

    /// <summary>Sorts the children with <paramref name="sorter"/>.</summary>
    public void SortChildren(Comparison<Panel> sorter)
    {
        if (_children is not { Count: > 0 })
        {
            return;
        }

        _children.Sort(sorter);
        ReorderLayoutChildren();
    }

    /// <summary>Sorts the children by the number <paramref name="sorter"/> gives each one that's a <typeparamref name="TargetType"/>. Others count as 0.</summary>
    public void SortChildren<TargetType>(Func<TargetType, int> sorter)
    {
        if (_children is not { Count: > 0 })
        {
            return;
        }

        var sorted = _children.OrderBy(child => child is TargetType target ? sorter(target) : 0).ToArray();
        _children.Clear();
        _children.AddRange(sorted);
        ReorderLayoutChildren();
    }

    /// <summary>Sorts the children by the number <paramref name="sorter"/> gives each one.</summary>
    public void SortChildren(Func<Panel, int> sorter) => SortChildren<Panel>(sorter);

    /// <summary>Called after a child is added.</summary>
    protected virtual void OnChildAdded(Panel child)
    {
    }

    /// <summary>Called after a child is removed.</summary>
    protected virtual void OnChildRemoved(Panel child)
    {
    }

    /// <summary>Whether the panel counts as empty for <c>:empty</c>. By default, when it has no children. Call <see cref="EmptyStateChanged"/> when an override's answer changes.</summary>
    protected virtual bool IsPanelEmpty() => ChildrenCount == 0;

    /// <summary>Updates <c>:empty</c>. Call it when an override of <see cref="IsPanelEmpty"/> would answer differently.</summary>
    protected void EmptyStateChanged() => UpdateChildrenIndexes();

    private void InsertChild(Panel child)
    {
        if (LayoutTree.HasMeasure)
        {
            throw new InvalidOperationException($"{this} can't have children.");
        }

        _children ??= [];
        _children.Add(child);
        LayoutTree.AddChild(child.LayoutTree);
        _orderedChildren += child._order != 0 ? 1 : 0;
        ChildrenMoved();

        var count = _children.Count;
        if (count >= 2)
        {
            _children[count - 2].UpdateSiblingIndex(count - 2, count);
        }

        child.UpdateSiblingIndex(count - 1, count);
        Switch(PseudoClass.Empty, IsPanelEmpty());
        OnChildAdded(child);
        SetNeedsPreLayout();
        _indexesDirty = true;
    }

    private void RemoveChild(Panel child)
    {
        if (IsDeleted)
        {
            return;
        }

        if (_children is null || !_children.Remove(child))
        {
            throw new InvalidOperationException($"{child} isn't a child of {this}.");
        }

        LayoutTree.RemoveChild(child.LayoutTree);
        _orderedChildren -= child._order != 0 ? 1 : 0;
        OnChildRemoved(child);
        UpdateChildrenIndexes();
        SetNeedsPreLayout();
    }

    private void ReorderLayoutChildren()
    {
        foreach (var child in _children!)
        {
            LayoutTree.RemoveChild(child.LayoutTree);
            LayoutTree.AddChild(child.LayoutTree);
        }

        ChildrenMoved();
        _indexesDirty = true;
        SetNeedsPreLayout();
    }

    private void UpdateChildrenIndexes()
    {
        _indexesDirty = false;
        DirtyRenderChildren();
        Switch(PseudoClass.Empty, IsPanelEmpty());
        var count = ChildrenCount;
        var siblings = count - ScrollbarCount;
        for (int i = 0; i < count; i++)
        {
            _children![i].UpdateSiblingIndex(i, siblings);
        }
    }

    private void UpdateSiblingIndex(int index, int siblings)
    {
        SiblingIndex = index;
        if (this is ScrollBar)
        {
            return;
        }

        Switch(PseudoClass.FirstChild, index == 0);
        Switch(PseudoClass.LastChild, index == siblings - 1);
        Switch(PseudoClass.OnlyChild, siblings == 1);
    }
}
