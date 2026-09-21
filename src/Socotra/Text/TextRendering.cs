using System.Collections.Concurrent;

namespace Socotra;

/// <summary>
/// The <see cref="Outline"/> and <see cref="Shadow"/> effects you can put on a <see cref="TextStyle"/>.
/// </summary>
public static partial class TextRendering
{
    private const long CleanupInterval = 500;

    private static readonly ConcurrentDictionary<(Scope Scope, Vector2 Clip, TextFlag Flags, int Fonts), TextBlock> _blocks = new();
    private static long _lastCleanup;

    internal static TextBlock GetOrCreateTextBlock(in Scope scope, TextFlag flags, Vector2 clip = default)
    {
        if (clip == default)
        {
            clip = new Vector2(8096);
        }

        Evict();

        var key = (scope, clip, flags, FontManager.Instance.Generation);
        if (!_blocks.TryGetValue(key, out var block))
        {
            block = _blocks.GetOrAdd(key, new TextBlock(scope, flags, clip));
        }

        block.LastUsed = Environment.TickCount64;
        return block;
    }

    private static void Evict()
    {
        var now = Environment.TickCount64;
        var last = Interlocked.Read(ref _lastCleanup);
        if (now - last < CleanupInterval || Interlocked.CompareExchange(ref _lastCleanup, now, last) != last)
        {
            return;
        }

        foreach (var item in _blocks)
        {
            if (item.Value.LastUsed < last)
            {
                _blocks.TryRemove(item);
            }
        }
    }
}
