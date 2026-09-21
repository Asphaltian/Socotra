namespace Socotra;

public partial class Panel
{
    private int _order;
    private int _orderedChildren;
    private bool _needsOrderSort;

    private void UpdateOrder()
    {
        var order = ComputedStyle!.Order ?? 0;
        if (order == _order)
        {
            return;
        }

        if (Parent is { } parent)
        {
            parent._orderedChildren += (order != 0 ? 1 : 0) - (_order != 0 ? 1 : 0);
            parent._needsOrderSort = true;
        }

        _order = order;
    }

    private void ChildrenMoved() => _needsOrderSort |= _orderedChildren > 0;

    private void SortChildrenOrder()
    {
        if (!_needsOrderSort || _children is null)
        {
            return;
        }

        _needsOrderSort = false;
        foreach (var child in _children.OrderBy(static child => child._order))
        {
            LayoutTree.RemoveChild(child.LayoutTree);
            LayoutTree.AddChild(child.LayoutTree);
        }
    }
}
