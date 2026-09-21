using Microsoft.AspNetCore.Components;

namespace Socotra;

/// <summary>
/// The scrolling, selection and keyboard handling behind <see cref="TreeView{T}"/>, working on row numbers. Use
/// <see cref="TreeView{T}"/> unless your data doesn't fit it: then subclass this and fill in the row methods.
/// </summary>
/// <remarks>
/// Up and Down move the cursor, Home, End, Page Up and Page Down jump, Left closes a row or goes to its parent, Right opens
/// a row or goes into it, Space opens or closes, Enter activates and F2 renames. Shift extends the selection and Ctrl
/// toggles single rows when clicking.
/// </remarks>
public abstract class BaseTreeView : Panel
{
    private readonly List<RowLayout> _rows = [];
    private readonly List<TreeRow> _active = [];
    private readonly List<TreeRow> _free = [];
    private int _activeFirst;
    private int _bindVersion;
    private int _placementHash;
    private float _totalHeight;
    private int _pendingRename = -1;
    private int _pendingScroll = -1;
    private int _highlightRow = -1;
    private double _highlightUntil;
    private int _highlightPhase;

    /// <summary>Sets up the tree. It takes focus and scrolls.</summary>
    protected BaseTreeView()
    {
        AddClass("treeview");
        Style.Position = PositionMode.Relative;
        Style.Overflow = OverflowMode.Scroll;
        AcceptsFocus = true;
    }

    /// <summary>How tall a row is, in pixels, when the tree doesn't give rows their own height.</summary>
    [Parameter]
    public float RowHeight { get; set; } = 24;

    /// <summary>Opens a closed row when it's clicked without Ctrl or Shift, before selecting it. Open rows stay open.</summary>
    [Parameter]
    public bool ExpandOnClick { get; set; }

    /// <summary>Opens a closed row when it's double clicked, before activating it. On by default; turn it off if you open rows yourself when they're activated.</summary>
    [Parameter]
    public bool ExpandOnDoubleClick { get; set; } = true;

    /// <summary>How far each level is indented, in pixels.</summary>
    [Parameter]
    public float IndentWidth { get; set; } = 16;

    /// <summary>How many rows above and below the view are also filled in, so scrolling doesn't show empty space.</summary>
    [Parameter]
    public int OverscanRows { get; set; } = 1;

    /// <summary>How long <see cref="HighlightRow"/> keeps a row highlighted before it fades, in seconds.</summary>
    [Parameter]
    public float HighlightTime { get; set; } = 1;

    /// <summary>How long a highlight takes to fade, in seconds. Style the fade with the <c>highlight-fade</c> class.</summary>
    [Parameter]
    public float HighlightFadeTime { get; set; } = 1;

    /// <summary>Whether one of the tree's rows is being dragged. The tree has the <c>dragging</c> class meanwhile.</summary>
    public bool IsDragging { get; private set; }

    /// <summary>Set it to true to rebuild the rows on the next update, after changing the data in a way the tree can't see.</summary>
    public bool NeedsRebuild { get; set; } = true;

    /// <summary>How many rows there are, counting only those inside open rows.</summary>
    public int RowCount => _rows.Count;

    internal int ActiveRowCount => _active.Count;

    internal int PooledRowCount => _active.Count + _free.Count;

    internal int BindCount { get; private set; }

    /// <summary>The row the keyboard moves from, or -1.</summary>
    public int CursorRow { get; private set; } = -1;

    /// <summary>The row a Shift selection reaches from, or -1.</summary>
    public int AnchorRow { get; private set; } = -1;

    /// <summary>Where the row after <paramref name="row"/>'s last descendant is. Rows between the two are inside it.</summary>
    public int GetSubtreeEnd(int row)
    {
        var depth = _rows[row].Depth;
        int i = row + 1;
        while (i < _rows.Count && _rows[i].Depth > depth)
        {
            i++;
        }

        return i;
    }

    /// <summary>How deep <paramref name="row"/> is. Top level rows are 0.</summary>
    public int GetRowDepth(int row) => _rows[row].Depth;

    /// <summary>The row <paramref name="row"/> is inside, or -1 for a top level row.</summary>
    public int GetParentRow(int row)
    {
        if (row < 0 || row >= _rows.Count)
        {
            return -1;
        }

        var depth = _rows[row].Depth - 1;
        if (depth < 0)
        {
            return -1;
        }

        for (int i = row - 1; i >= 0; i--)
        {
            if (_rows[i].Depth == depth)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Rebuilds the rows right away instead of on the next update.</summary>
    public void Rebuild()
    {
        NeedsRebuild = false;
        foreach (var panel in _active)
        {
            panel.Unbind();
        }

        _rows.Clear();
        _totalHeight = 0;
        BuildRows();

        if (CursorRow >= _rows.Count)
        {
            CursorRow = _rows.Count - 1;
        }

        if (AnchorRow >= _rows.Count)
        {
            AnchorRow = -1;
        }

        if (_highlightRow >= _rows.Count)
        {
            _highlightRow = -1;
        }

        _bindVersion++;
        SetNeedsFinalLayout();
    }

    /// <summary>Fills every row panel in view again, without rebuilding the rows. Call it when the rows' contents change but not which rows there are.</summary>
    public void RebindRows() => _bindVersion++;

    /// <inheritdoc/>
    public override void Tick()
    {
        base.Tick();
        if (ComputedStyle is null || !IsVisible)
        {
            return;
        }

        CheckForChanges();
        if (NeedsRebuild)
        {
            Rebuild();
        }

        UpdateWindow();
        UpdateHighlight();

        if (_pendingScroll >= 0)
        {
            var row = _pendingScroll;
            _pendingScroll = -1;
            ScrollToRow(row);
        }

        if (_pendingRename >= 0)
        {
            var row = _pendingRename;
            _pendingRename = -1;
            BeginRename(row);
        }
    }

    /// <summary>Highlights <paramref name="row"/> so the eye finds it: the <c>highlight</c> class for <see cref="HighlightTime"/>, then <c>highlight-fade</c> for <see cref="HighlightFadeTime"/>.</summary>
    public void HighlightRow(int row)
    {
        if (row < 0 || row >= _rows.Count)
        {
            return;
        }

        _highlightRow = row;
        _highlightUntil = TimeNow + HighlightTime;
        _highlightPhase = 1;
        RefreshRowStates();
    }

    /// <summary>The panel showing <paramref name="row"/>, or null if the row isn't in view.</summary>
    public TreeRow? GetRowPanel(int row)
    {
        var i = row - _activeFirst;
        return i < 0 || i >= _active.Count ? null : _active[i];
    }

    /// <summary>Scrolls the least amount that brings <paramref name="row"/> fully into view.</summary>
    public void ScrollToRow(int row)
    {
        if (row < 0 || row >= _rows.Count)
        {
            return;
        }

        if (ComputedStyle is null || Box.Rect.Height <= 0)
        {
            _pendingScroll = row;
            return;
        }

        GetContentRect(out _, out var contentTop, out _, out var viewportHeight);
        var layout = _rows[row];
        var scrollY = ScrollOffset.Y * ScaleFromScreen;
        var top = contentTop + layout.Top;
        var bottom = top + layout.Height;

        if (top < scrollY)
        {
            scrollY = top;
        }
        else if (bottom > scrollY + viewportHeight)
        {
            scrollY = bottom - viewportHeight;
        }
        else
        {
            return;
        }

        StopScrollVelocity();
        ScrollOffset = ScrollOffset with { Y = MathF.Max(0, scrollY) * ScaleToScreen };
        SetNeedsFinalLayout();
    }

    /// <summary>Opens <paramref name="row"/> if it's closed or closes it if it's open. Pass <paramref name="recursive"/> to do the same to everything inside it.</summary>
    public void ToggleRow(int row, bool recursive = false)
    {
        if (row < 0 || row >= _rows.Count)
        {
            return;
        }

        SetRowOpen(row, !_rows[row].IsOpen, recursive);
    }

    /// <summary>Lets the user rename <paramref name="row"/> in place, scrolling it into view first. Enter keeps the new name, Escape or clicking away cancels.</summary>
    public void BeginRename(int row)
    {
        if (row < 0 || row >= _rows.Count || !CanRenameRow(row))
        {
            return;
        }

        if (GetRowPanel(row) is not { } panel)
        {
            ScrollToRow(row);
            _pendingRename = row;
            return;
        }

        panel.BeginRename();
    }

    /// <summary>
    /// Selects <paramref name="row"/> the way a click does: on its own, or with <paramref name="toggle"/> (Ctrl) added to or
    /// taken out of the selection, or with <paramref name="range"/> (Shift) along with every row back to <see cref="AnchorRow"/>.
    /// </summary>
    public void SelectRow(int row, bool toggle = false, bool range = false)
    {
        if (row < 0 || row >= _rows.Count)
        {
            return;
        }

        if (range)
        {
            if (AnchorRow < 0)
            {
                AnchorRow = CursorRow >= 0 ? CursorRow : row;
            }

            if (!toggle)
            {
                ClearSelectionInternal();
            }

            var a = Math.Min(AnchorRow, row);
            var b = Math.Max(AnchorRow, row);
            for (int i = a; i <= b; i++)
            {
                SetRowSelected(i, true);
            }
        }
        else if (toggle)
        {
            SetRowSelected(row, !IsRowSelected(row));
            AnchorRow = row;
        }
        else
        {
            ClearSelectionInternal();
            SetRowSelected(row, true);
            AnchorRow = row;
        }

        CursorRow = row;
        RefreshRowStates();
        OnSelectionFinished();
    }

    /// <summary>Deselects everything. The keyboard cursor stays where it is.</summary>
    public void ClearSelection()
    {
        ClearSelectionInternal();
        AnchorRow = -1;
        RefreshRowStates();
        OnSelectionFinished();
    }

    /// <summary>Moves the keyboard cursor by <paramref name="delta"/> rows and selects the row it lands on, along with the rows back to the anchor when <paramref name="range"/> is true.</summary>
    public void MoveCursor(int delta, bool range = false)
    {
        if (_rows.Count == 0)
        {
            return;
        }

        var row = CursorRow < 0 ? 0 : Math.Clamp(CursorRow + delta, 0, _rows.Count - 1);
        SelectRow(row, false, range);
        ScrollToRow(row);
    }

    /// <summary>Handles the navigation keys while the tree has focus.</summary>
    public override void OnButtonTyped(ButtonEvent e)
    {
        if (e.Pressed && HandleKey(e))
        {
            e.StopPropagation = true;
            return;
        }

        base.OnButtonTyped(e);
    }

    internal void SetDragging(bool dragging)
    {
        if (IsDragging == dragging)
        {
            return;
        }

        IsDragging = dragging;
        SetClass("dragging", dragging);
    }

    internal void RowRenamed(int row, string text) => OnRowRenamed(row, text);

    internal void RowClicked(int row, MousePanelEvent e)
    {
        if (row < 0 || row >= _rows.Count)
        {
            return;
        }

        if (ExpandOnClick && !e.HasCtrl && !e.HasShift && _rows[row].HasChildren && !_rows[row].IsOpen)
        {
            SetRowOpen(row, true, false);
        }

        SelectRow(row, e.HasCtrl, e.HasShift);
    }

    internal void RowDoubleClicked(int row)
    {
        if (row < 0 || row >= _rows.Count)
        {
            return;
        }

        if (ExpandOnDoubleClick && _rows[row].HasChildren && !_rows[row].IsOpen)
        {
            SetRowOpen(row, true, false);
        }

        OnRowActivated(row);
    }

    internal void RowRightClicked(int row)
    {
        if (!IsRowSelected(row))
        {
            SelectRow(row, false, false);
        }

        OnRowContextMenu(row);
    }

    internal void RowDropped(int targetRow, int sourceRow)
    {
        if (sourceRow < 0 || sourceRow >= _rows.Count || targetRow >= _rows.Count)
        {
            return;
        }

        OnRowDropped(targetRow, sourceRow);
    }

    /// <summary>Goes through your data and calls <see cref="AddRow"/> for every row inside open rows, in order.</summary>
    protected abstract void BuildRows();

    /// <summary>Fills in <paramref name="panel"/> with what <paramref name="row"/> shows. A panel shows other rows as the tree scrolls, so set everything you change.</summary>
    protected abstract void BindRow(int row, TreeRow panel);

    /// <summary>Called every update. Set <see cref="NeedsRebuild"/> here if your data changed.</summary>
    protected virtual void CheckForChanges()
    {
    }

    /// <summary>Whether <paramref name="row"/> is selected.</summary>
    protected abstract bool IsRowSelected(int row);

    /// <summary>Selects or deselects <paramref name="row"/>.</summary>
    protected abstract void SetRowSelected(int row, bool selected);

    /// <summary>Deselects every row.</summary>
    protected abstract void ClearSelectionInternal();

    /// <summary>Called once after the selection changes.</summary>
    protected abstract void OnSelectionFinished();

    /// <summary>Opens or closes <paramref name="row"/>, and everything inside it when <paramref name="recursive"/> is true.</summary>
    protected abstract void SetRowOpen(int row, bool open, bool recursive);

    /// <summary>Called when <paramref name="row"/> is double clicked or Enter is pressed on it.</summary>
    protected abstract void OnRowActivated(int row);

    /// <summary>Called when <paramref name="row"/> is right clicked.</summary>
    protected abstract void OnRowContextMenu(int row);

    /// <summary>Whether the user can rename <paramref name="row"/>.</summary>
    protected abstract bool CanRenameRow(int row);

    /// <summary>Called when the user has renamed <paramref name="row"/> to <paramref name="text"/>.</summary>
    protected abstract void OnRowRenamed(int row, string text);

    /// <summary>Whether the user can drag <paramref name="row"/>.</summary>
    protected internal abstract bool CanDragRow(int row);

    /// <summary>Called when the row <paramref name="sourceRow"/> is dropped on <paramref name="targetRow"/>, or on empty space when <paramref name="targetRow"/> is -1.</summary>
    protected abstract void OnRowDropped(int targetRow, int sourceRow);

    /// <summary>Adds the next row while <see cref="BuildRows"/> runs.</summary>
    /// <param name="height">How tall the row is, in pixels.</param>
    /// <param name="depth">How deep the row is. Top level rows are 0.</param>
    /// <param name="hasChildren">Whether the row can be opened.</param>
    /// <param name="isOpen">Whether the row is open.</param>
    protected void AddRow(float height, int depth, bool hasChildren, bool isOpen)
    {
        _rows.Add(new RowLayout
        {
            Top = _totalHeight,
            Height = height,
            Depth = depth,
            HasChildren = hasChildren,
            IsOpen = isOpen,
        });

        _totalHeight += height;
    }

    /// <summary>Where <paramref name="row"/> is and how it's shown.</summary>
    protected RowLayout GetRow(int row) => _rows[row];

    /// <summary>Updates the selected and open look of every row in view. Call it after changing which rows are selected yourself.</summary>
    protected void RefreshRowStates()
    {
        for (int i = 0; i < _active.Count; i++)
        {
            ApplyState(_activeFirst + i, _active[i]);
        }
    }

    /// <inheritdoc/>
    protected override void FinalLayoutChildren(Vector2 offset)
    {
        foreach (var panel in _active)
        {
            panel.FinalLayout(offset);
        }

        foreach (var panel in _free)
        {
            panel.FinalLayout(offset);
        }

        var rect = Box.Rect;
        rect.Position -= ScrollOffset;
        var extent = Box.Padding.Top + (_totalHeight * ScaleToScreen) + Box.Padding.Bottom;
        rect.Height = MathF.Max(extent, rect.Height);
        ConstrainScrolling(rect.Size);
    }

    /// <summary>Deselects everything when the empty space below the rows is clicked without Ctrl or Shift.</summary>
    protected override void OnClick(MousePanelEvent e)
    {
        if (e.Target != this || e.HasCtrl || e.HasShift)
        {
            return;
        }

        ClearSelection();
    }

    /// <summary>Handles one of the tree's rows being dropped on the empty space below the rows.</summary>
    protected override void OnDrop(PanelEvent e)
    {
        if (e is DropEvent || e.Target is not TreeRow source || source.Tree != this)
        {
            return;
        }

        RowDropped(-1, source.RowIndex);
        e.StopPropagation();
    }

    private void GetContentRect(out float left, out float top, out float width, out float height)
    {
        var scale = ScaleFromScreen;
        left = (Box.RectInner.Left - Box.Rect.Left) * scale;
        top = (Box.RectInner.Top - Box.Rect.Top) * scale;
        width = Box.RectInner.Width * scale;
        height = Box.Rect.Height * scale;
    }

    private int FindRowAt(float y)
    {
        int lo = 0, hi = _rows.Count;
        while (lo < hi)
        {
            int mid = (lo + hi) >> 1;
            if (_rows[mid].Top + _rows[mid].Height > y)
            {
                hi = mid;
            }
            else
            {
                lo = mid + 1;
            }
        }

        return lo;
    }

    private int FindRowStartingAt(float y)
    {
        int lo = 0, hi = _rows.Count;
        while (lo < hi)
        {
            int mid = (lo + hi) >> 1;
            if (_rows[mid].Top >= y)
            {
                hi = mid;
            }
            else
            {
                lo = mid + 1;
            }
        }

        return lo;
    }

    private void UpdateWindow()
    {
        GetContentRect(out var contentLeft, out var contentTop, out var contentWidth, out var viewportHeight);
        var scrollY = (ScrollOffset.Y * ScaleFromScreen) - contentTop;

        int first = 0, pastEnd = 0;
        if (_rows.Count > 0)
        {
            first = Math.Max(0, FindRowAt(scrollY) - OverscanRows);
            pastEnd = Math.Min(_rows.Count, FindRowStartingAt(scrollY + viewportHeight) + OverscanRows);
        }

        var placementHash = HashCode.Combine(contentLeft, contentTop, contentWidth, IndentWidth);
        var replace = placementHash != _placementHash;
        _placementHash = placementHash;

        if (_active.Count > 0 && (pastEnd <= _activeFirst || first >= _activeFirst + _active.Count))
        {
            foreach (var panel in _active)
            {
                Release(panel);
            }

            _active.Clear();
        }

        if (_active.Count == 0)
        {
            _activeFirst = first;
        }

        while (_active.Count > 0 && _activeFirst < first)
        {
            Release(_active[0]);
            _active.RemoveAt(0);
            _activeFirst++;
        }

        while (_active.Count > 0 && _activeFirst + _active.Count > pastEnd)
        {
            Release(_active[^1]);
            _active.RemoveAt(_active.Count - 1);
        }

        while (_activeFirst > first)
        {
            _activeFirst--;
            var panel = Acquire();
            _active.Insert(0, panel);
            Bind(_activeFirst, panel);
        }

        while (_activeFirst + _active.Count < pastEnd)
        {
            var panel = Acquire();
            _active.Add(panel);
            Bind(_activeFirst + _active.Count - 1, panel);
        }

        for (int i = 0; i < _active.Count; i++)
        {
            var panel = _active[i];
            if (panel.BindVersion != _bindVersion)
            {
                Bind(_activeFirst + i, panel);
            }
            else if (replace)
            {
                Place(_activeFirst + i, panel);
            }
        }

        while (_free.Count > _active.Count + 4)
        {
            var panel = _free[^1];
            _free.RemoveAt(_free.Count - 1);
            panel.Delete(true);
        }
    }

    private TreeRow Acquire()
    {
        if (_free.Count > 0)
        {
            var panel = _free[^1];
            _free.RemoveAt(_free.Count - 1);
            return panel;
        }

        return AddChild(new TreeRow { Tree = this });
    }

    private void Release(TreeRow panel)
    {
        panel.Unbind();
        _free.Add(panel);
    }

    private void Bind(int row, TreeRow panel)
    {
        panel.RowIndex = row;
        panel.BindVersion = _bindVersion;
        BindCount++;
        ApplyState(row, panel);
        Place(row, panel);
        BindRow(row, panel);
    }

    private void ApplyState(int row, TreeRow panel)
    {
        var layout = _rows[row];
        panel.SetState(layout.Depth, layout.HasChildren, layout.IsOpen, IsRowSelected(row), IndentWidth);
        var highlight = row == _highlightRow ? _highlightPhase : 0;
        panel.SetClass("highlight", highlight == 1);
        panel.SetClass("highlight-fade", highlight == 2);
    }

    private void UpdateHighlight()
    {
        if (_highlightRow < 0)
        {
            return;
        }

        var now = TimeNow;
        var phase = now < _highlightUntil ? 1 : now < _highlightUntil + HighlightFadeTime ? 2 : 0;
        if (phase == _highlightPhase)
        {
            return;
        }

        _highlightPhase = phase;
        if (phase == 0)
        {
            _highlightRow = -1;
        }

        RefreshRowStates();
    }

    private void Place(int row, TreeRow panel)
    {
        GetContentRect(out var left, out var top, out var width, out _);
        var layout = _rows[row];
        panel.SetRect(left, top + layout.Top, width, layout.Height);
    }

    private bool HandleKey(ButtonEvent e)
    {
        switch (e.Button)
        {
            case "up":
                MoveCursor(-1, e.HasShift);
                return true;
            case "down":
                MoveCursor(1, e.HasShift);
                return true;
            case "home":
                MoveCursor(-_rows.Count, e.HasShift);
                return true;
            case "end":
                MoveCursor(_rows.Count, e.HasShift);
                return true;
            case "pageup":
                MoveCursor(-PageRows(), e.HasShift);
                return true;
            case "pagedown":
                MoveCursor(PageRows(), e.HasShift);
                return true;
            case "left":
                CursorLeft();
                return true;
            case "right":
                CursorRight();
                return true;
            case "space":
                ToggleRow(CursorRow);
                return true;
            case "enter":
                if (CursorRow >= 0)
                {
                    OnRowActivated(CursorRow);
                }

                return true;
            case "f2":
                BeginRename(CursorRow);
                return true;
        }

        return false;
    }

    private int PageRows()
    {
        GetContentRect(out _, out _, out _, out var viewportHeight);
        return Math.Max(1, (int)(viewportHeight / RowHeight) - 1);
    }

    private void CursorLeft()
    {
        if (CursorRow < 0)
        {
            MoveCursor(0);
            return;
        }

        var layout = _rows[CursorRow];
        if (layout.HasChildren && layout.IsOpen)
        {
            SetRowOpen(CursorRow, false, false);
            return;
        }

        var parent = GetParentRow(CursorRow);
        if (parent >= 0)
        {
            MoveCursor(parent - CursorRow);
        }
    }

    private void CursorRight()
    {
        if (CursorRow < 0)
        {
            MoveCursor(0);
            return;
        }

        var layout = _rows[CursorRow];
        if (!layout.HasChildren)
        {
            return;
        }

        if (!layout.IsOpen)
        {
            SetRowOpen(CursorRow, true, false);
            return;
        }

        MoveCursor(1);
    }

    /// <summary>Where one row sits and how it's shown, in pixels from the top left of the tree's content.</summary>
    protected struct RowLayout
    {
        /// <summary>How far down the row starts.</summary>
        public float Top;

        /// <summary>How tall the row is.</summary>
        public float Height;

        /// <summary>How deep the row is. Top level rows are 0.</summary>
        public int Depth;

        /// <summary>Whether the row can be opened.</summary>
        public bool HasChildren;

        /// <summary>Whether the row is open.</summary>
        public bool IsOpen;
    }
}
