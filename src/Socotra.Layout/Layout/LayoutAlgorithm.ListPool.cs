namespace Socotra.Layout;

internal static partial class LayoutAlgorithm
{
    [ThreadStatic] private static Stack<List<LayoutNode>>? _listPool;
    [ThreadStatic] private static int _pooledListCapacity;
    private const int MaxPooledLists = 128;
    private const int MaxPooledListCapacity = 4096;
    private const int MaxTotalPooledListCapacity = 32768;

    internal static List<LayoutNode> RentList()
    {
        _listPool ??= new Stack<List<LayoutNode>>();
        if (_listPool.Count == 0)
        {
            return new List<LayoutNode>();
        }

        var list = _listPool.Pop();
        _pooledListCapacity -= list.Capacity;
        return list;
    }

    internal static void ReturnList(List<LayoutNode> list)
    {
        _listPool ??= new Stack<List<LayoutNode>>();
        var retain = list.Capacity <= MaxPooledListCapacity;
        list.Clear();
        if (retain && _listPool.Count < MaxPooledLists && _pooledListCapacity + list.Capacity <= MaxTotalPooledListCapacity)
        {
            _listPool.Push(list);
            _pooledListCapacity += list.Capacity;
        }
    }
}
