namespace Socotra;

/// <summary>
/// One row of a <see cref="BaseTreeView"/> that's in view. The same panel shows other rows as the tree scrolls, so
/// anything you add to one should be added once and updated every time the row is filled in. A row has the <c>tree-row</c>
/// class, plus <c>has-children</c>, <c>open</c> and <c>selected</c> as they apply.
/// </summary>
public class TreeRow : Panel
{
    private const float ParkedTop = -100000;

    private TextEntry? _rename;
    private float _indent = -1;
    private bool _iconVisible = true;

    /// <summary>Makes an empty row. Trees make their own rows; you don't need to.</summary>
    public TreeRow()
    {
        AddClass("tree-row");
        Style.Position = PositionMode.Absolute;
        Expander = Add.Icon("arrow_right", "tree-expander");
        Icon = Add.Icon(null, "tree-icon");
        Label = Add.Label(null, "tree-label");
        Content = Add.Panel("tree-content");
    }

    /// <summary>The tree this row belongs to.</summary>
    public BaseTreeView? Tree { get; internal set; }

    /// <summary>Which row of the tree this panel shows, or -1 while it shows none.</summary>
    public int RowIndex { get; internal set; } = -1;

    /// <summary>The arrow that opens and closes the row. Style it with the <c>tree-expander</c> class; it's there even on rows that can't open.</summary>
    public IconPanel Expander { get; }

    /// <summary>The icon before the text, set with <see cref="IconName"/>.</summary>
    public IconPanel Icon { get; }

    /// <summary>The label showing <see cref="Text"/>.</summary>
    public Label Label { get; }

    /// <summary>
    /// Where the row's own controls go, like toggles, buttons or badges. Add them once and update them each time the row
    /// is filled. Clicks on them belong to them: they don't select or activate the row.
    /// </summary>
    public Panel Content { get; }

    /// <summary>The row's text.</summary>
    public string? Text
    {
        get => Label.Text;
        set
        {
            if (Label.Text != value)
            {
                Label.Text = value;
            }
        }
    }

    /// <summary>The name of the icon shown before the text, like <c>folder</c>. Null or empty hides it.</summary>
    public string? IconName
    {
        get => Icon.Text;
        set
        {
            if (Icon.Text != value)
            {
                Icon.Text = value;
            }

            var visible = !string.IsNullOrEmpty(value);
            if (visible == _iconVisible)
            {
                return;
            }

            _iconVisible = visible;
            Icon.Style.Display = visible ? DisplayMode.Flex : DisplayMode.None;
        }
    }

    /// <summary>Whether the user is renaming the row right now.</summary>
    public bool IsRenaming => _rename is { IsDeleted: false };

    /// <summary>Rows can be dragged when their tree lets them.</summary>
    public override bool WantsDrag => Tree is not null && RowIndex >= 0 && Tree.CanDragRow(RowIndex);

    internal int BindVersion { get; set; } = -1;

    /// <summary>Swaps the label for a text box so the user can rename the row. Enter keeps the new name; Escape or clicking away cancels.</summary>
    public void BeginRename()
    {
        if (IsRenaming)
        {
            return;
        }

        var rename = new TextEntry { Text = Text };
        rename.AddClass("rename");
        _rename = AddChild(rename);
        SetChildIndex(rename, GetChildIndex(Label) + 1);
        Label.Style.Display = DisplayMode.None;
        AddClass("renaming");

        rename.AddEventListener("onsubmit", e => SubmitRename(e.Value as string));
        rename.AddEventListener("oncancel", EndRename);
        rename.AddEventListener("onblur", EndRename);
        rename.Focus();
        rename.CaretPosition = rename.TextLength;
    }

    internal void SetState(int depth, bool hasChildren, bool isOpen, bool selected, float indentWidth)
    {
        SetClass("has-children", hasChildren);
        SetClass("open", isOpen);
        SetClass("selected", selected);

        var indent = depth * indentWidth;
        if (indent != _indent)
        {
            _indent = indent;
            Style.PaddingLeft = indent;
        }
    }

    internal void SetRect(float left, float top, float width, float height)
    {
        Style.Left = left;
        Style.Top = top;
        Style.Width = width;
        Style.Height = height;
    }

    internal void Unbind()
    {
        EndRename();
        RowIndex = -1;
        BindVersion = -1;
        RemoveClass("drop-target");
        RemoveClass("dragging");
        Style.Top = ParkedTop;
    }

    /// <summary>Focuses the tree when the row is pressed.</summary>
    protected override void OnMouseDown(MousePanelEvent e)
    {
        if (RowIndex >= 0 && !IsOnContent(e))
        {
            Tree?.Focus();
        }
    }

    /// <summary>Opens or closes the row when the expander is clicked, Shift doing everything inside it too, and otherwise selects the row.</summary>
    protected override void OnClick(MousePanelEvent e)
    {
        if (RowIndex < 0 || Tree is null || IsOnContent(e))
        {
            return;
        }

        if (e.Target == Expander)
        {
            Tree.ToggleRow(RowIndex, e.HasShift);
            e.StopPropagation();
            return;
        }

        Tree.RowClicked(RowIndex, e);
        e.StopPropagation();
    }

    /// <summary>Activates the row.</summary>
    protected override void OnDoubleClick(MousePanelEvent e)
    {
        if (RowIndex < 0 || Tree is null || e.Target == Expander || IsOnContent(e))
        {
            return;
        }

        Tree.RowDoubleClicked(RowIndex);
        e.StopPropagation();
    }

    /// <summary>Selects the row, unless it's already selected, and tells the tree it was right clicked.</summary>
    protected override void OnRightClick(MousePanelEvent e)
    {
        if (RowIndex < 0 || Tree is null)
        {
            return;
        }

        Tree.RowRightClicked(RowIndex);
        e.StopPropagation();
    }

    /// <summary>Marks the row and the tree as dragging.</summary>
    protected override void OnDragStart(DragEvent e)
    {
        AddClass("dragging");
        Tree?.SetDragging(true);
        e.StopPropagation();
    }

    /// <summary>Keeps the drag from scrolling the tree.</summary>
    protected override void OnDrag(DragEvent e) => e.StopPropagation();

    /// <summary>Clears the dragging marks.</summary>
    protected override void OnDragEnd(DragEvent e)
    {
        RemoveClass("dragging");
        Tree?.SetDragging(false);
        e.StopPropagation();
    }

    /// <summary>Adds the <c>drop-target</c> class while another row of the same tree is dragged over this one.</summary>
    protected override void OnDragEnter(PanelEvent e)
    {
        if (IsSiblingDrag(e))
        {
            AddClass("drop-target");
        }
    }

    /// <summary>Takes the <c>drop-target</c> class away.</summary>
    protected override void OnDragLeave(PanelEvent e) => RemoveClass("drop-target");

    /// <summary>Tells the tree another of its rows was dropped on this one.</summary>
    protected override void OnDrop(PanelEvent e)
    {
        RemoveClass("drop-target");
        if (!IsSiblingDrag(e))
        {
            return;
        }

        Tree!.RowDropped(RowIndex, ((TreeRow)e.Target!).RowIndex);
        e.StopPropagation();
    }

    private bool IsOnContent(PanelEvent e) => e.Target != Content && e.Target is { IsDeleted: false } target && target.IsAncestor(Content);

    private bool IsSiblingDrag(PanelEvent e) =>
        e is not DropEvent && RowIndex >= 0 && Tree is not null
        && e.Target is TreeRow source && source != this && source.Tree == Tree && source.RowIndex >= 0;

    private void SubmitRename(string? text)
    {
        var row = RowIndex;
        EndRename();
        if (row < 0 || string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        Tree?.RowRenamed(row, text);
    }

    private void EndRename()
    {
        if (_rename is not { IsDeleted: false } entry)
        {
            return;
        }

        _rename = null;
        entry.Delete(true);
        Label.Style.Display = null;
        RemoveClass("renaming");
    }
}
