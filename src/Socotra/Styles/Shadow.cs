namespace Socotra;

/// <summary>One shadow in <c>box-shadow</c>, <c>text-shadow</c> or <c>filter: drop-shadow()</c>.</summary>
/// <param name="OffsetX">How far right the shadow sits, in pixels.</param>
/// <param name="OffsetY">How far down the shadow sits, in pixels.</param>
/// <param name="Blur">How soft the shadow's edge is. 0 gives a hard edge.</param>
/// <param name="Spread">How much bigger than the box the shadow is. Box shadows only.</param>
/// <param name="Color">The shadow's color.</param>
/// <param name="Inset">Whether the shadow is inside the box. Box shadows only.</param>
public readonly record struct Shadow(float OffsetX, float OffsetY, float Blur, float Spread, Color Color, bool Inset)
{
    /// <summary>This shadow with every size multiplied by <paramref name="scale"/>.</summary>
    public Shadow Scale(float scale) => this with { OffsetX = OffsetX * scale, OffsetY = OffsetY * scale, Blur = Blur * scale, Spread = Spread * scale };

    /// <summary>A blend of this shadow and <paramref name="shadow"/>: 0 gives this one, 1 gives <paramref name="shadow"/>.</summary>
    public Shadow LerpTo(Shadow shadow, float delta) => new(
        OffsetX + ((shadow.OffsetX - OffsetX) * delta),
        OffsetY + ((shadow.OffsetY - OffsetY) * delta),
        Blur + ((shadow.Blur - Blur) * delta),
        Spread + ((shadow.Spread - Spread) * delta),
        Color.Lerp(Color, shadow.Color, delta),
        shadow.Inset);

    internal static IReadOnlyList<Shadow> Lerp(IReadOnlyList<Shadow> a, IReadOnlyList<Shadow> b, float delta)
    {
        var shadows = new Shadow[Math.Max(a.Count, b.Count)];
        for (int i = 0; i < shadows.Length; i++)
        {
            var to = i < b.Count ? b[i] : a[i] with { Color = a[i].Color.WithAlpha(0) };
            var from = i < a.Count ? a[i] : to with { Color = to.Color.WithAlpha(0) };
            shadows[i] = from.LerpTo(to, delta);
        }

        return shadows;
    }

    internal static IReadOnlyList<Shadow>? ParseList(string value)
    {
        var p = new Parse(value).SkipWhitespaceAndNewlines();
        if (p.TrySkip("none", ignoreCase: true))
        {
            return p.SkipWhitespaceAndNewlines().IsEnd ? [] : null;
        }

        var shadows = new List<Shadow>();
        while (!p.IsEnd)
        {
            if (ReadShadow(ref p) is not { } shadow)
            {
                return null;
            }

            shadows.Add(shadow);
            if (!p.SkipWhitespaceAndNewlines().IsEnd && !p.TrySkip(","))
            {
                return null;
            }
        }

        return shadows;
    }

    private static Shadow? ReadShadow(ref Parse p)
    {
        bool inset = p.TryReadShadowInset();
        Color? color = null;
        p.SkipWhitespaceAndNewlines();
        if (!p.IsDigit && p.Current is not ('-' or '.') && p.TryReadColor(out var before))
        {
            color = before;
        }

        inset |= p.TryReadShadowInset();
        if (!p.TryReadLength(out var x) || !p.TryReadLength(out var y))
        {
            return null;
        }

        float blur = 0;
        float spread = 0;
        if (p.TryReadLength(out var blurLength))
        {
            blur = blurLength.Value;
            if (p.TryReadLength(out var spreadLength))
            {
                spread = spreadLength.Value;
            }
        }

        if (color is null && p.TryReadColor(out var after))
        {
            color = after;
        }

        inset |= p.TryReadShadowInset();
        return new Shadow(x.Value, y.Value, blur, spread, color ?? Color.CurrentColor, inset);
    }
}
