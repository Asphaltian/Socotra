namespace Socotra;

public partial class Popup
{
    internal static Vector2 AnchorPosition(Rect anchor, Vector2 size, Rect bounds, PositionMode position, float gap)
    {
        if (position != PositionMode.RightTop)
        {
            return AnchorPosition(anchor, size, bounds, position == PositionMode.AboveLeft, gap);
        }

        var rightSpace = bounds.Right - anchor.Right - gap;
        var leftSpace = anchor.Left - bounds.Left - gap;
        var x = rightSpace < size.X && leftSpace > rightSpace ? anchor.Left - gap - size.X : anchor.Right + gap;
        return new Vector2(
            Math.Clamp(x, bounds.Left, Math.Max(bounds.Left, bounds.Right - size.X)),
            Math.Clamp(anchor.Top, bounds.Top, Math.Max(bounds.Top, bounds.Bottom - size.Y)));
    }

    internal static Vector2 AnchorPosition(Rect anchor, Vector2 size, Rect bounds, bool above, float gap)
    {
        var belowSpace = bounds.Bottom - anchor.Bottom - gap;
        var aboveSpace = anchor.Top - bounds.Top - gap;
        if (above ? aboveSpace < size.Y && belowSpace > aboveSpace : belowSpace < size.Y && aboveSpace > belowSpace)
        {
            above = !above;
        }

        return new Vector2(
            Math.Clamp(anchor.Left, bounds.Left, Math.Max(bounds.Left, bounds.Right - size.X)),
            Math.Clamp(above ? anchor.Top - gap - size.Y : anchor.Bottom + gap, bounds.Top, Math.Max(bounds.Top, bounds.Bottom - size.Y)));
    }
}
