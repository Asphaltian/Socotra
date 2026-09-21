namespace Socotra.Vulkan;

internal sealed class Frame(Func<FramePass> createPass) : IDisposable
{
    private readonly List<FramePass> _passes = [];
    private int _used;

    public void Begin() => _used = 0;

    public FramePass NextPass()
    {
        if (_used == _passes.Count)
        {
            _passes.Add(createPass());
        }

        return _passes[_used++];
    }

    public void Dispose()
    {
        foreach (var pass in _passes)
        {
            pass.Dispose();
        }
    }
}
