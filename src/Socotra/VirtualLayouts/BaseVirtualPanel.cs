using System.Collections;
using Microsoft.AspNetCore.Components;

namespace Socotra;

/// <summary>
/// The base of <see cref="VirtualList"/> and <see cref="VirtualGrid"/>. Inherit from it to lay long lists
/// out your own way. Only the items in view have cells, so fill cells in through <see cref="Item"/> or <see cref="OnCreateCell"/>.
/// </summary>
public abstract class BaseVirtualPanel : Panel
{
    private readonly Dictionary<int, object> _cellData = [];
    private readonly Dictionary<int, Panel> _created = [];
    private readonly List<object> _items = [];
    private IList? _sourceList;
    private int _sourceListCount;
    private bool _lastCellCreated;

    /// <summary>Sets the panel up to scroll.</summary>
    protected BaseVirtualPanel()
    {
        Style.Position = PositionMode.Relative;
        Style.OverflowX = OverflowMode.Scroll;
        Style.OverflowY = OverflowMode.Scroll;
    }

    /// <summary>Set to true to lay the cells out again.</summary>
    public bool NeedsRebuild { get; set; }

    /// <summary>The markup for each item's cell. In Razor, write it inside <c>&lt;Item&gt;</c> and use <c>@context</c> for the item.</summary>
    [Parameter]
    public RenderFragment<object>? Item { get; set; }

    /// <summary>Fills in an item's cell from code. You get the cell and its item.</summary>
    [Parameter]
    public Action<Panel, object>? OnCreateCell { get; set; }

    /// <summary>Called when the last item's cell is made, so you can load more.</summary>
    [Parameter]
    public Action? OnLastCell { get; set; }

    /// <summary>
    /// The items to show. Pass a list, like a <see cref="List{T}"/>, and items you add to it or remove from it
    /// later show up too. If you swap an item without changing the count, set this again.
    /// </summary>
    [Parameter]
    public IEnumerable<object>? Items
    {
        set
        {
            if (value is null)
            {
                if (_items.Count > 0)
                {
                    Clear();
                }

                return;
            }

            _sourceList = value as IList;
            var items = _sourceList is null ? value.ToList() : [.. _sourceList.Cast<object>()];
            _sourceListCount = _sourceList is null ? 0 : items.Count;
            if (_items.SequenceEqual(items))
            {
                return;
            }

            _items.Clear();
            _items.AddRange(items);
            NeedsRebuild = true;
            _lastCellCreated = false;
            StateHasChanged();
        }
    }

    /// <summary>How many items there are.</summary>
    public int ItemCount => _items.Count;

    /// <summary>Adds an item.</summary>
    public void AddItem(object item)
    {
        _items.Add(item);
        NeedsRebuild = true;
    }

    /// <summary>Adds several items.</summary>
    public void AddItems(IEnumerable<object> items)
    {
        _items.AddRange(items);
        NeedsRebuild = true;
    }

    /// <summary>Removes the first copy of <paramref name="item"/>. Returns false if it wasn't there.</summary>
    public bool RemoveItem(object item)
    {
        var removed = _items.Remove(item);
        NeedsRebuild |= removed;
        return removed;
    }

    /// <summary>Removes the item at <paramref name="index"/>.</summary>
    public void RemoveAt(int index)
    {
        _items.RemoveAt(index);
        NeedsRebuild = true;
    }

    /// <summary>Puts <paramref name="item"/> in at <paramref name="index"/>.</summary>
    public void InsertItem(int index, object item)
    {
        _items.Insert(index, item);
        NeedsRebuild = true;
    }

    /// <summary>Removes every item.</summary>
    public void Clear()
    {
        _items.Clear();
        _sourceList = null;
        _sourceListCount = 0;
        NeedsRebuild = true;
        foreach (var panel in _created.Values)
        {
            panel.Delete(true);
        }

        _created.Clear();
        _cellData.Clear();
    }

    /// <summary>Whether there's an item at <paramref name="i"/>.</summary>
    public bool HasData(int i) => i >= 0 && i < _items.Count;

    /// <summary>Replaces the items, like setting <see cref="Items"/>.</summary>
    public void SetItems(IEnumerable<object> enumerable) => Items = enumerable;

    /// <summary>Updates the cells as the panel scrolls. Call the base method if you override it.</summary>
    public override void Tick()
    {
        base.Tick();
        if (ComputedStyle is null || !IsVisible)
        {
            return;
        }

        CheckSourceForChanges();
        UpdateLayoutSpacing(new Vector2(ComputedStyle.ColumnGap?.Value ?? 0, ComputedStyle.RowGap?.Value ?? 0));
        if (!UpdateLayout() && !NeedsRebuild)
        {
            return;
        }

        NeedsRebuild = false;
        GetVisibleRange(out var first, out var pastEnd);
        DeleteNotVisible(first, pastEnd - 1);
        for (int i = first; i < pastEnd; i++)
        {
            RefreshCreated(i);
        }
    }

    /// <inheritdoc/>
    protected override void FinalLayoutChildren(Vector2 offset)
    {
        foreach (var panel in _created.Values)
        {
            panel.FinalLayout(offset);
        }

        var size = Box.Rect.Size;
        size.Y = MathF.Max(GetTotalHeight(_items.Count) * ScaleToScreen, size.Y);
        ConstrainScrolling(size);
    }

    /// <summary>Override to use the gaps between cells, set with <c>column-gap</c> and <c>row-gap</c>.</summary>
    protected abstract void UpdateLayoutSpacing(Vector2 spacing);

    /// <summary>Override to work out where the cells go. Return true if anything moved.</summary>
    protected abstract bool UpdateLayout();

    /// <summary>Override to give the items in view, from <paramref name="first"/> up to but not including <paramref name="pastEnd"/>.</summary>
    protected abstract void GetVisibleRange(out int first, out int pastEnd);

    /// <summary>Override to move the cell for the item at <paramref name="index"/> into place.</summary>
    protected abstract void PositionPanel(int index, Panel panel);

    /// <summary>Override to give how tall <paramref name="itemCount"/> items are altogether, in CSS pixels.</summary>
    protected abstract float GetTotalHeight(int itemCount);

    private void CheckSourceForChanges()
    {
        if (_sourceList is null || _sourceList.Count == _sourceListCount)
        {
            return;
        }

        _items.Clear();
        _items.AddRange(_sourceList.Cast<object>());
        _sourceListCount = _sourceList.Count;
        NeedsRebuild = true;
        _lastCellCreated = false;
        StateHasChanged();
    }

    private void DeleteNotVisible(int minInclusive, int maxInclusive)
    {
        foreach (var index in _created.Keys.Where(i => i < minInclusive || i > maxInclusive || !HasData(i)).ToArray())
        {
            if (_created.Remove(index, out var panel))
            {
                panel.Delete(true);
            }

            _cellData.Remove(index);
        }
    }

    private void RefreshCreated(int i)
    {
        if (!HasData(i))
        {
            return;
        }

        var data = _items[i];
        var needsRebuild = !_cellData.TryGetValue(i, out var last) || !Equals(last, data);
        if (!_created.TryGetValue(i, out var panel) || needsRebuild)
        {
            panel?.Delete(true);
            panel = Add.Panel("cell");
            panel.Style.Position = PositionMode.Absolute;
            panel.ChildContent = Item?.Invoke(data);
            _created[i] = panel;
            _cellData[i] = data;
            OnCreateCell?.Invoke(panel, data);
            if (i == _items.Count - 1)
            {
                OnCreatedLastCell();
            }
        }

        PositionPanel(i, panel);
    }

    private void OnCreatedLastCell()
    {
        if (_lastCellCreated)
        {
            return;
        }

        _lastCellCreated = true;
        try
        {
            OnLastCell?.Invoke();
        }
        catch (Exception e)
        {
            Log.Error(e);
        }
    }
}
