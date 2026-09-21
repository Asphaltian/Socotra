namespace Socotra.Layout;

internal static class GridPool<T> where T : class, new()
{
    private const int MaxRetained = 4096;
    private const int MaxTotalCapacity = 32768;
    private const int MaxBufferCapacity = 4096;

    [ThreadStatic] private static Stack<(T Item, int Capacity)>? _items;
    [ThreadStatic] private static int _capacity;

    public static T Rent()
    {
        var items = _items ??= new();
        if (items.Count == 0)
        {
            return new T();
        }

        var entry = items.Pop();
        _capacity -= entry.Capacity;
        return entry.Item;
    }

    public static void Return(T item, int capacity = 0)
    {
        var items = _items ??= new();
        if (items.Count < MaxRetained
            && capacity <= MaxBufferCapacity && _capacity + capacity <= MaxTotalCapacity)
        {
            items.Push((item, capacity));
            _capacity += capacity;
        }
    }
}
