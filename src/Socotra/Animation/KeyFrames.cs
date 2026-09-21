namespace Socotra;

/// <summary>A <c>@keyframes</c> rule from a stylesheet: the styles an animation moves through, from start to end.</summary>
public sealed class KeyFrames
{
    internal KeyFrames(string name)
    {
        Name = name;
    }

    /// <summary>The name you give <c>animation-name</c> to play it.</summary>
    public string Name { get; }

    /// <summary>The keyframes, from first to last.</summary>
    public List<Block> Blocks { get; } = [];

    internal void FillStyle(float delta, Styles style)
    {
        var start = Blocks[0];
        var end = start;
        foreach (var block in Blocks)
        {
            end = block;
            if (block.Interval > delta)
            {
                break;
            }

            start = block;
        }

        var t = start.Interval == end.Interval ? 0 : Math.Clamp((delta - start.Interval) / (end.Interval - start.Interval), 0, 1);
        style.From(start.Styles);
        style.FromLerp(start.Styles, end.Styles, t);
    }

    internal static KeyFrames? Parse(ref Parse p, StyleSheet sheet)
    {
        if (!p.TrySkip("@keyframes", ignoreCase: true))
        {
            return null;
        }

        p = p.SkipWhitespaceAndNewlines();
        var name = p.ReadWord(" {", readUntilEnd: true);
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new FormatException($"Expected a name after @keyframes {p.FileAndLine}");
        }

        p = p.SkipWhitespaceAndNewlines();
        if (!p.TrySkip("{"))
        {
            throw new FormatException($"Expected {{ {p.FileAndLine}");
        }

        var frames = new KeyFrames(name);
        while (!(p = p.SkipWhitespaceAndNewlines()).IsEnd && p.Current != '}')
        {
            var intervals = new List<float>();
            while (p.Current != '{')
            {
                if (!p.TryReadLength(out var length) || length.Unit != LengthUnit.Percentage)
                {
                    throw new FormatException($"Expected a percentage, from or to {p.FileAndLine}");
                }

                var interval = length.GetFraction();
                if (frames.Blocks.Any(b => b.Interval == interval) || intervals.Contains(interval))
                {
                    throw new FormatException($"Duplicate keyframe {length} {p.FileAndLine}");
                }

                intervals.Add(interval);
                p = p.SkipWhitespaceAndNewlines(",");
                if (p.IsEnd)
                {
                    throw new FormatException($"Expected {{ {p.FileAndLine}");
                }
            }

            var styles = new Styles();
            StyleParser.ParseDeclarationBlock(ref p, styles, sheet);
            frames.Blocks.AddRange(intervals.Select(interval => new Block(interval, styles)));
        }

        if (!p.TrySkip("}"))
        {
            throw new FormatException($"Expected }} {p.FileAndLine}");
        }

        frames.Blocks.Sort(static (a, b) => a.Interval.CompareTo(b.Interval));
        return frames;
    }

    /// <summary>One keyframe.</summary>
    /// <param name="Interval">How far through the animation it is, from 0 (<c>from</c>) to 1 (<c>to</c>).</param>
    /// <param name="Styles">The styles at that point.</param>
    public sealed record Block(float Interval, Styles Styles);
}
