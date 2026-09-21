using Microsoft.AspNetCore.Components;

namespace Socotra;

/// <summary>
/// A tree of your own objects that the user can open, close, select and scroll through. Give it the top level items in
/// <see cref="Roots"/> and a way to get an item's children in <see cref="GetChildren"/>. Return lists or arrays from
/// <see cref="GetChildren"/> and the tree notices when items are added or removed.
/// </summary>
/// <example>
/// <code>
/// // A folder browser: folders open to show what's inside, double click opens a file
/// var tree = new TreeView&lt;FileSystemInfo&gt; { Parent = sidebar };
/// tree.Roots = [new DirectoryInfo(projectPath)];
/// tree.GetChildren = item => item is DirectoryInfo folder ? folder.GetFileSystemInfos() : [];
/// tree.OnRow = (row, item) =>
/// {
///     row.Text = item.Name;
///     row.IconName = item is DirectoryInfo ? "folder" : "description";
/// };
/// tree.OnActivate = item => OpenFile(item.FullName);
/// </code>
/// </example>
public class TreeView<T> : BaseTreeView
{
    private readonly List<T> _rowItems = [];
    private readonly List<IList<T>?> _rowChildLists = [];
    private readonly List<int> _rowChildCounts = [];
    private readonly List<T> _path = [];
    private IEnumerable<T>? _roots;
    private IList<T>? _rootList;
    private int _rootCount;
    private IEqualityComparer<T> _comparer = EqualityComparer<T>.Default;
    private HashSet<T> _open = [];
    private HashSet<T> _selection = [];

    /// <summary>The top level items. Pass a list or array and the tree notices when items are added or removed.</summary>
    [Parameter]
    public IEnumerable<T>? Roots
    {
        get => _roots;
        set
        {
            if (ReferenceEquals(_roots, value))
            {
                return;
            }

            _roots = value;
            _rootList = value as IList<T>;
            NeedsRebuild = true;
        }
    }

    /// <summary>The children of an item. Return a list or array so the tree notices when children are added or removed.</summary>
    [Parameter]
    public Func<T, IEnumerable<T>?>? GetChildren { get; set; }

    /// <summary>
    /// The parent of an item, or default for a top level item. You don't have to give it; without it, finding an item for
    /// <see cref="Select"/>, <see cref="ScrollTo"/> or <see cref="Highlight"/> looks through the tree from <see cref="Roots"/>.
    /// </summary>
    [Parameter]
    public Func<T, T?>? GetParent { get; set; }

    /// <summary>Whether an item can be opened. You don't have to give it; without it the tree asks <see cref="GetChildren"/>.</summary>
    [Parameter]
    public Func<T, bool>? CanExpand { get; set; }

    /// <summary>How tall an item's row is, in pixels. Leave it null to make every row <see cref="BaseTreeView.RowHeight"/>.</summary>
    [Parameter]
    public Func<T, float>? GetHeight { get; set; }

    /// <summary>The text shown when the mouse rests on an item's row. Return null for no tooltip.</summary>
    [Parameter]
    public Func<T, string?>? GetTooltip { get; set; }

    /// <summary>Whether F2 can rename an item. Without it, every item can be renamed once <see cref="OnRename"/> is set.</summary>
    [Parameter]
    public Func<T, bool>? CanRename { get; set; }

    /// <summary>
    /// Fills a row panel from its item. It's called when a row scrolls into view or the tree refreshes, not every frame.
    /// A panel shows other items as the tree scrolls, so add extra panels once and update them here.
    /// </summary>
    [Parameter]
    public Action<TreeRow, T>? OnRow { get; set; }

    /// <summary>Razor markup for each row's <see cref="TreeRow.Content"/>, given the row's item.</summary>
    [Parameter]
    public RenderFragment<T>? Row { get; set; }

    /// <summary>Called with the item under the keyboard cursor after the selection changes.</summary>
    [Parameter]
    public Action<T>? OnSelect { get; set; }

    /// <summary>Called whenever <see cref="Selection"/> changes.</summary>
    [Parameter]
    public Action? OnSelectionChanged { get; set; }

    /// <summary>Called when an item is double clicked or Enter is pressed on it. A double click opens a closed item first while <see cref="BaseTreeView.ExpandOnDoubleClick"/> is on.</summary>
    [Parameter]
    public Action<T>? OnActivate { get; set; }

    /// <summary>Called when an item is right clicked, to show a menu of your own.</summary>
    [Parameter]
    public Action<T>? OnContextMenu { get; set; }

    /// <summary>Called when the user has renamed an item in place. Apply the new name to your data here.</summary>
    [Parameter]
    public Action<T, string>? OnRename { get; set; }

    /// <summary>Whether an item can be dragged. Setting it lets the user drag rows.</summary>
    [Parameter]
    public Func<T, bool>? CanDrag { get; set; }

    /// <summary>Called with the dragged item and the item it was dropped on, or default when it was dropped on empty space.</summary>
    [Parameter]
    public Action<T, T?>? OnItemDropped { get; set; }

    /// <summary>How items are compared when the tree keeps track of which are open and selected. Set it before opening or selecting anything.</summary>
    public IEqualityComparer<T> Comparer
    {
        get => _comparer;
        set
        {
            value ??= EqualityComparer<T>.Default;
            if (ReferenceEquals(value, _comparer))
            {
                return;
            }

            _comparer = value;
            _open = new HashSet<T>(_open, value);
            _selection = new HashSet<T>(_selection, value);
        }
    }

    /// <summary>The selected items.</summary>
    public IReadOnlyCollection<T> Selection => _selection;

    /// <summary>The open items.</summary>
    public IReadOnlyCollection<T> OpenItems => _open;

    /// <summary>The item under the keyboard cursor, or default.</summary>
    public T? CursorItem => CursorRow >= 0 && CursorRow < _rowItems.Count ? _rowItems[CursorRow] : default;

    /// <summary>The item shown in <paramref name="row"/>.</summary>
    public T GetRowItem(int row) => _rowItems[row];

    /// <summary>Which row shows <paramref name="item"/>, or -1 if it's inside a closed item.</summary>
    public int GetItemRow(T item)
    {
        for (int i = 0; i < _rowItems.Count; i++)
        {
            if (_comparer.Equals(_rowItems[i], item))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Whether <paramref name="item"/> is open.</summary>
    public bool IsOpen(T item) => _open.Contains(item);

    /// <summary>Whether <paramref name="item"/> is selected.</summary>
    public bool IsSelected(T item) => _selection.Contains(item);

    /// <summary>Opens <paramref name="item"/>, and everything inside it when <paramref name="recursive"/> is true.</summary>
    public void Open(T item, bool recursive = false)
    {
        if (_open.Add(item))
        {
            NeedsRebuild = true;
        }

        if (!recursive || GetChildren?.Invoke(item) is not { } children)
        {
            return;
        }

        if (children is IList<T> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                Open(list[i], true);
            }

            return;
        }

        foreach (var child in children)
        {
            Open(child, true);
        }
    }

    /// <summary>Closes <paramref name="item"/>, and everything inside it when <paramref name="recursive"/> is true.</summary>
    public void Close(T item, bool recursive = false)
    {
        var wasOpen = _open.Remove(item);
        if (wasOpen)
        {
            NeedsRebuild = true;
        }

        if (!recursive || !wasOpen || GetChildren?.Invoke(item) is not { } children)
        {
            return;
        }

        if (children is IList<T> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                Close(list[i], true);
            }

            return;
        }

        foreach (var child in children)
        {
            Close(child, true);
        }
    }

    /// <summary>Opens <paramref name="item"/> if it's closed or closes it if it's open, and does the same to everything inside it when <paramref name="recursive"/> is true.</summary>
    public void Toggle(T item, bool recursive = false)
    {
        if (_open.Contains(item))
        {
            Close(item, recursive);
        }
        else
        {
            Open(item, recursive);
        }
    }

    /// <summary>Opens every item above <paramref name="item"/> so it has a row. Returns false if it isn't in the tree.</summary>
    public bool ExpandPathTo(T item)
    {
        _path.Clear();
        if (GetParent is not null)
        {
            var seen = new HashSet<T>(_comparer) { item };
            for (var p = GetParent(item); p is not null && !_comparer.Equals(p, default!) && seen.Add(p); p = GetParent(p))
            {
                _path.Add(p);
            }
        }
        else if (!FindPath(_roots, item))
        {
            return false;
        }

        foreach (var parent in _path)
        {
            Open(parent);
        }

        return true;
    }

    /// <summary>Selects <paramref name="item"/>, opening the way to it and scrolling it into view. Pass <paramref name="add"/> to keep the rest of the selection.</summary>
    public void Select(T item, bool add = false)
    {
        if (RowOf(item) is not (>= 0 and var row))
        {
            return;
        }

        SelectRow(row, add, false);
        ScrollToRow(row);
    }

    /// <summary>Scrolls <paramref name="item"/> into view, opening the way to it.</summary>
    public void ScrollTo(T item)
    {
        if (RowOf(item) is >= 0 and var row)
        {
            ScrollToRow(row);
        }
    }

    /// <summary>Scrolls <paramref name="item"/> into view and highlights it for a moment, opening the way to it.</summary>
    public void Highlight(T item)
    {
        if (RowOf(item) is not (>= 0 and var row))
        {
            return;
        }

        ScrollToRow(row);
        HighlightRow(row);
    }

    /// <summary>Rebuilds and refills every row. Call it when your data changes in a way the tree can't see, like a rename or a reorder.</summary>
    public void Refresh() => NeedsRebuild = true;

    /// <summary>Rebuilds and refills every row after <paramref name="item"/> changed.</summary>
    public void Refresh(T item) => NeedsRebuild = true;

    /// <inheritdoc/>
    protected override void BuildRows()
    {
        _rowItems.Clear();
        _rowChildLists.Clear();
        _rowChildCounts.Clear();
        _rootCount = _rootList?.Count ?? 0;
        AddItems(_roots, 0);
    }

    /// <inheritdoc/>
    protected override void CheckForChanges()
    {
        if (_rootList is not null && _rootList.Count != _rootCount)
        {
            NeedsRebuild = true;
            return;
        }

        for (int i = 0; i < _rowChildLists.Count; i++)
        {
            if (_rowChildLists[i] is { } list && list.Count != _rowChildCounts[i])
            {
                NeedsRebuild = true;
                return;
            }
        }
    }

    /// <inheritdoc/>
    protected override void BindRow(int row, TreeRow panel)
    {
        var item = _rowItems[row];
        if (OnRow is not null)
        {
            OnRow(panel, item);
        }
        else if (Row is not null)
        {
            panel.Content.ChildContent = Row(item);
            panel.Content.StateHasChanged();
        }
        else
        {
            panel.Text = item?.ToString();
        }

        if (GetTooltip is not null)
        {
            var tooltip = GetTooltip(item);
            if (panel.Tooltip != tooltip)
            {
                panel.Tooltip = tooltip;
            }
        }
    }

    /// <inheritdoc/>
    protected override bool IsRowSelected(int row) => _selection.Contains(_rowItems[row]);

    /// <inheritdoc/>
    protected override void SetRowSelected(int row, bool selected)
    {
        if (selected)
        {
            _selection.Add(_rowItems[row]);
        }
        else
        {
            _selection.Remove(_rowItems[row]);
        }
    }

    /// <inheritdoc/>
    protected override void ClearSelectionInternal() => _selection.Clear();

    /// <inheritdoc/>
    protected override void OnSelectionFinished()
    {
        OnSelectionChanged?.Invoke();
        if (OnSelect is not null && CursorRow >= 0 && CursorRow < _rowItems.Count)
        {
            OnSelect(_rowItems[CursorRow]);
        }
    }

    /// <inheritdoc/>
    protected override void SetRowOpen(int row, bool open, bool recursive)
    {
        if (open)
        {
            Open(_rowItems[row], recursive);
        }
        else
        {
            Close(_rowItems[row], recursive);
        }
    }

    /// <inheritdoc/>
    protected override void OnRowActivated(int row) => OnActivate?.Invoke(_rowItems[row]);

    /// <inheritdoc/>
    protected override void OnRowContextMenu(int row) => OnContextMenu?.Invoke(_rowItems[row]);

    /// <inheritdoc/>
    protected override bool CanRenameRow(int row) => OnRename is not null && (CanRename?.Invoke(_rowItems[row]) ?? true);

    /// <inheritdoc/>
    protected override void OnRowRenamed(int row, string text)
    {
        OnRename?.Invoke(_rowItems[row], text);
        NeedsRebuild = true;
    }

    /// <inheritdoc/>
    protected internal override bool CanDragRow(int row) => CanDrag?.Invoke(_rowItems[row]) ?? false;

    /// <inheritdoc/>
    protected override void OnRowDropped(int targetRow, int sourceRow)
    {
        if (OnItemDropped is null)
        {
            return;
        }

        var target = targetRow >= 0 ? _rowItems[targetRow] : default;
        OnItemDropped(_rowItems[sourceRow], target);
        NeedsRebuild = true;
    }

    private int RowOf(T item)
    {
        ExpandPathTo(item);
        if (NeedsRebuild)
        {
            Rebuild();
        }

        return GetItemRow(item);
    }

    private bool FindPath(IEnumerable<T>? items, T target)
    {
        if (items is null)
        {
            return false;
        }

        if (items is IList<T> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (FindPathThrough(list[i], target))
                {
                    return true;
                }
            }

            return false;
        }

        foreach (var item in items)
        {
            if (FindPathThrough(item, target))
            {
                return true;
            }
        }

        return false;
    }

    private bool FindPathThrough(T item, T target)
    {
        if (_comparer.Equals(item, target))
        {
            return true;
        }

        if (!ItemHasChildren(item, out var children))
        {
            return false;
        }

        _path.Add(item);
        if (FindPath(children ?? GetChildren?.Invoke(item), target))
        {
            return true;
        }

        _path.RemoveAt(_path.Count - 1);
        return false;
    }

    private void AddItems(IEnumerable<T>? items, int depth)
    {
        if (items is null)
        {
            return;
        }

        if (items is IList<T> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                AddItem(list[i], depth);
            }

            return;
        }

        foreach (var item in items)
        {
            AddItem(item, depth);
        }
    }

    private void AddItem(T item, int depth)
    {
        var hasChildren = ItemHasChildren(item, out var children);
        var open = hasChildren && _open.Contains(item);
        IList<T>? childList = null;
        if (open)
        {
            children ??= GetChildren?.Invoke(item);
            childList = children as IList<T>;
        }

        AddRow(GetHeight?.Invoke(item) ?? RowHeight, depth, hasChildren, open);
        _rowItems.Add(item);
        _rowChildLists.Add(childList);
        _rowChildCounts.Add(childList?.Count ?? 0);
        if (open)
        {
            AddItems(children, depth + 1);
        }
    }

    private bool ItemHasChildren(T item, out IEnumerable<T>? children)
    {
        children = null;
        if (CanExpand is not null)
        {
            return CanExpand(item);
        }

        children = GetChildren?.Invoke(item);
        if (children is null)
        {
            return false;
        }

        if (children is IList<T> list)
        {
            return list.Count > 0;
        }

        using var e = children.GetEnumerator();
        return e.MoveNext();
    }
}

/// <summary>A <see cref="TreeView{T}"/> over plain objects, for Razor markup that doesn't want to name a type.</summary>
public sealed class TreeView : TreeView<object>
{
}
