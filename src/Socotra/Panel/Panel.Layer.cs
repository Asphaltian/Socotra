namespace Socotra;

public partial class Panel
{
    internal bool HasPanelLayer => _paintCache.Layer is not null;

    internal Rect PanelLayerBounds => _paintCache.Layer!.Bounds;

    private void DrawLayer(Painter painter, int target)
    {
        var layer = _paintCache.Layer!;
        painter.Composite(target, layer.Bounds, layer.Filter, layer.Mask, layer.MaskScope, layer.DropShadows, layer.BorderWidth, layer.BorderColor);
    }

    private sealed class LayerPaint
    {
        internal Rect Bounds;
        internal Painter.Filter Filter;
        internal Painter.Mask? Mask;
        internal MaskScope MaskScope;
        internal IReadOnlyList<Shadow> DropShadows = [];
        internal float BorderWidth;
        internal Color BorderColor;

        internal void Update(Panel panel, FilterMode sampling)
        {
            var style = panel.ComputedStyle!;
            Bounds = CalculateBounds(panel);
            Filter = new Painter.Filter
            {
                Blur = style.FilterBlur!.Value.GetPixels(1),
                Saturation = style.FilterSaturate!.Value.GetFraction(),
                Sepia = style.FilterSepia!.Value.GetFraction(),
                Brightness = style.FilterBrightness!.Value.GetPixels(1),
                Contrast = style.FilterContrast!.Value.GetPixels(1),
                Invert = style.FilterInvert!.Value.GetPixels(1),
                HueRotation = style.FilterHueRotate!.Value.GetPixels(1),
                Tint = style.FilterTint ?? Color.White,
            };

            Mask = null;
            if (style.MaskImage is { } image)
            {
                var outer = panel.Box.RectOuter;
                var tile = ImageRect.Calculate(new ImageRect.Input
                {
                    ScaleToScreen = panel.ScaleToScreen,
                    Image = image,
                    PanelRect = outer,
                    DefaultSize = Length.Auto,
                    ImagePositionX = style.MaskPositionX,
                    ImagePositionY = style.MaskPositionY,
                    ImageSizeX = style.MaskSizeX,
                    ImageSizeY = style.MaskSizeY,
                });
                var rect = new Rect(outer.Left + tile.X, outer.Top + tile.Y, tile.Z, tile.W);
                Mask = new Painter.Mask(image, rect, style.MaskMode ?? MaskMode.MatchSource, style.MaskRepeat ?? BackgroundRepeat.Repeat,
                    float.RadiansToDegrees(style.MaskAngle?.GetPixels(1) ?? 0), sampling);
            }

            MaskScope = style.MaskScope ?? MaskScope.Default;
            DropShadows = style.FilterDropShadow ?? [];
            BorderWidth = style.FilterBorderWidth!.Value.GetPixels(1) * panel.ScaleToScreen;
            BorderColor = style.FilterBorderColor!.Value;
        }

        private static Rect CalculateBounds(Panel panel)
        {
            var bounds = panel.Box.RectOuter;
            foreach (var shadow in panel.ComputedStyle!.BoxShadow ?? [])
            {
                if (shadow.Inset || shadow.Color.A <= 0)
                {
                    continue;
                }

                var shape = (panel.Box.Rect + new Vector2(shadow.OffsetX, shadow.OffsetY)).Grow(shadow.Spread);
                bounds.Add(shape.Grow(MathF.Ceiling(shadow.Blur * 1.5f)));
            }

            return new Rect
            {
                Left = MathF.Floor(bounds.Left),
                Top = MathF.Floor(bounds.Top),
                Right = MathF.Ceiling(bounds.Right),
                Bottom = MathF.Ceiling(bounds.Bottom),
            };
        }
    }
}
