namespace Socotra;

public partial class Styles
{
    /// <summary>Whether any side has a border wider than zero.</summary>
    public bool HasBorder => UsedBorderLeftWidth?.Value > 0 || UsedBorderTopWidth?.Value > 0 || UsedBorderRightWidth?.Value > 0 || UsedBorderBottomWidth?.Value > 0;

    /// <summary>Whether any corner is rounded.</summary>
    public bool HasBorderRadius =>
        _borderTopLeftRadius?.Value > 0 || _borderTopRightRadius?.Value > 0 || _borderBottomLeftRadius?.Value > 0 || _borderBottomRightRadius?.Value > 0;

    internal Length? UsedBorderLeftWidth => UsedBorderWidth(_borderLeftWidth);

    internal Length? UsedBorderTopWidth => UsedBorderWidth(_borderTopWidth);

    internal Length? UsedBorderRightWidth => UsedBorderWidth(_borderRightWidth);

    internal Length? UsedBorderBottomWidth => UsedBorderWidth(_borderBottomWidth);

    /// <summary>How far in the content sits from each edge: the border plus the padding. Percentages are of <paramref name="size"/>.</summary>
    public Margin GetInset(Vector2 size) =>
        Socotra.Margin.GetEdges(size, UsedBorderLeftWidth, UsedBorderTopWidth, UsedBorderRightWidth, UsedBorderBottomWidth)
        + Socotra.Margin.GetEdges(size, _paddingLeft, _paddingTop, _paddingRight, _paddingBottom);

    /// <summary>The margin on each edge. Percentages are of <paramref name="size"/>.</summary>
    public Margin GetOutset(Vector2 size) => Socotra.Margin.GetEdges(size, _marginLeft, _marginTop, _marginRight, _marginBottom);

    internal Vector4 GetBorderWidths(float size) => new(
        UsedBorderLeftWidth!.Value.GetPixels(size),
        UsedBorderTopWidth!.Value.GetPixels(size),
        UsedBorderRightWidth!.Value.GetPixels(size),
        UsedBorderBottomWidth!.Value.GetPixels(size));

    internal BorderRadii GetBorderRadii(in Rect rect) => new BorderRadii
    {
        TopLeft = ResolveRadius(_borderTopLeftRadius, _borderTopLeftRadiusV, rect),
        TopRight = ResolveRadius(_borderTopRightRadius, _borderTopRightRadiusV, rect),
        BottomLeft = ResolveRadius(_borderBottomLeftRadius, _borderBottomLeftRadiusV, rect),
        BottomRight = ResolveRadius(_borderBottomRightRadius, _borderBottomRightRadiusV, rect),
    }.Clamped(rect.Width, rect.Height);

    private static Vector2 ResolveRadius(Length? horizontal, Length? vertical, in Rect rect) => horizontal is { } h
        ? new Vector2(MathF.Max(0, h.GetPixels(rect.Width)), MathF.Max(0, (vertical ?? h).GetPixels(rect.Height)))
        : Vector2.Zero;

    private Length? UsedBorderWidth(Length? width) => _borderStyle is Socotra.BorderStyle.None or Socotra.BorderStyle.Hidden ? Length.Pixels(0) : width;
}
