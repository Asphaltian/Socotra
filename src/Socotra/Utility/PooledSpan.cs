using System.Buffers;

namespace Socotra;

internal readonly ref struct PooledSpan<T>
{
    private readonly int _length;
    private readonly T[]? _array;

    public PooledSpan(int length)
    {
        _length = length;
        _array = ArrayPool<T>.Shared.Rent(length);
    }

    public Span<T> Span => _array.AsSpan(0, _length);

    public void Dispose()
    {
        if (_array is not null)
        {
            ArrayPool<T>.Shared.Return(_array);
        }
    }
}
