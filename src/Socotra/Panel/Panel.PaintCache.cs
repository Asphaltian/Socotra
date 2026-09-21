namespace Socotra;

public partial class Panel
{
    private PaintCache _paintCache;

    internal bool HasBackdropFilter => _paintCache.HasBackdrop;

    internal bool HasFilter => _paintCache.HasFilter;

    internal bool HasBackground => _paintCache.HasBackground;

    private struct PaintCache
    {
        private bool _dirty;
        private Length? _fontSize;
        private float _rootFontSize;
        private Color? _fontColor;
        private string? _mixBlendMode;
        private ImageRendering? _imageRendering;
        private Rect _rect;
        private Rect _outerRect;
        private Matrix4x4? _parentMatrix;
        private FilterMode _sampling;

        internal ImagePlacement Background;
        internal BorderRadii OuterRadii;
        internal BorderRadii InnerRadii;
        internal Vector4 FillInsets;
        internal float OutlineWidth;
        internal float OutlineOffset;
        internal Color OutlineColor;
        internal Matrix4x4 ClipTransform;
        internal Rect Bounds;
        internal Rect ContentClipRect;
        internal float Opacity;
        internal BlendMode? Blend;
        internal bool ClipsChildren;
        internal bool ClipsBackgroundToText;
        internal bool HasBackground;
        internal bool HasBackdrop;
        internal bool HasFilter;
        internal bool HasBorder;
        internal Painter.Filter Backdrop;
        internal LayerPaint? Layer;

        internal readonly bool InheritedStylesChanged(Panel panel)
        {
            var style = panel.ComputedStyle!;
            return _fontSize != style.FontSize || _rootFontSize != Length.RootFontSize || _fontColor != style.FontColor
                || _mixBlendMode != style.MixBlendMode || _imageRendering != style.ImageRendering;
        }

        internal void Invalidate(Panel panel)
        {
            _dirty = true;
            var style = panel.ComputedStyle!;
            HasBackground = style.BackgroundColor!.Value.A > 0 || style.BorderImageSource is not null
                || !style.BackgroundGradient.IsEmpty || style.BackgroundImage is not null
                || (style.BorderLeftColor!.Value.A > 0 && style.UsedBorderLeftWidth!.Value.GetPixels(1) > 0)
                || (style.BorderTopColor!.Value.A > 0 && style.UsedBorderTopWidth!.Value.GetPixels(1) > 0)
                || (style.BorderRightColor!.Value.A > 0 && style.UsedBorderRightWidth!.Value.GetPixels(1) > 0)
                || (style.BorderBottomColor!.Value.A > 0 && style.UsedBorderBottomWidth!.Value.GetPixels(1) > 0);
            HasBackdrop = !style.IsDefault("backdrop-filter-blur") || !style.IsDefault("backdrop-filter-contrast")
                || !style.IsDefault("backdrop-filter-saturate") || !style.IsDefault("backdrop-filter-sepia")
                || !style.IsDefault("backdrop-filter-invert") || !style.IsDefault("backdrop-filter-hue-rotate")
                || !style.IsDefault("backdrop-filter-brightness");
            HasFilter = !style.IsDefault("filter-saturate") || !style.IsDefault("filter-brightness")
                || !style.IsDefault("filter-contrast") || !style.IsDefault("filter-blur") || !style.IsDefault("filter-sepia")
                || !style.IsDefault("filter-hue-rotate") || !style.IsDefault("filter-invert")
                || !style.IsDefault("filter-tint") || !style.IsDefault("filter-border-width");
        }

        internal readonly bool NeedsUpdate(Panel panel) => _dirty || _parentMatrix != panel.VisualParent?.GlobalMatrix;

        internal void Update(Panel panel)
        {
            var style = panel.ComputedStyle!;
            var rect = panel.Box.Rect;
            var insets = panel.GetBackgroundClipInset(style.BackgroundClip ?? BackgroundClip.BorderBox);
            var geometryChanged = _dirty || _rect.Size != rect.Size || FillInsets != insets;
            Length.CurrentFontSize = style.FontSize!.Value.GetPixels(0);

            if (geometryChanged)
            {
                var size = (rect.Width + rect.Height) * 0.5f;
                var widths = style.GetBorderWidths(size);
                OuterRadii = style.GetBorderRadii(rect);
                InnerRadii = OuterRadii.Inner(widths);
                FillInsets = insets;
                OutlineWidth = style.OutlineWidth!.Value.GetPixels(size);
                OutlineOffset = style.OutlineOffset!.Value.GetPixels(size);
                OutlineColor = style.OutlineColor!.Value;
                Bounds = new Rect(Vector2.Zero, rect.Size);
                Opacity = style.Opacity ?? 1;
                Blend = ParseBlendMode(style.MixBlendMode);
                ClipsBackgroundToText = style.BackgroundClip == BackgroundClip.Text;
                ClipsChildren = style.OverflowX is not (OverflowMode.Visible or OverflowMode.ClipWhole)
                    || style.OverflowY is not (OverflowMode.Visible or OverflowMode.ClipWhole);
                _sampling = SamplingFor(style.ImageRendering);

                var border = Painter.ResolveBoxStroke(widths, style.BorderLeftColor!.Value, style.BorderTopColor!.Value,
                    style.BorderRightColor!.Value, style.BorderBottomColor!.Value, style.BorderStyle ?? BorderStyle.Solid);
                NineSliceImage borderImage = default;
                if (style.BorderImageSource is { } image)
                {
                    var slices = new Vector4(style.BorderImageWidthLeft!.Value.GetPixels(size), style.BorderImageWidthTop!.Value.GetPixels(size),
                        style.BorderImageWidthRight!.Value.GetPixels(size), style.BorderImageWidthBottom!.Value.GetPixels(size));
                    borderImage = new NineSliceImage(image, slices, style.BorderImageRepeat ?? BorderImageRepeat.Stretch,
                        style.BorderImageFill ?? BorderImageFill.Unfilled, style.BorderImageTint!.Value);
                }

                Backdrop = HasBackdrop ? new Painter.Filter
                {
                    Brightness = style.BackdropFilterBrightness!.Value.GetPixels(1),
                    Contrast = style.BackdropFilterContrast!.Value.GetPixels(1),
                    Saturation = style.BackdropFilterSaturate!.Value.GetPixels(1),
                    Sepia = style.BackdropFilterSepia!.Value.GetPixels(1),
                    Invert = style.BackdropFilterInvert!.Value.GetPixels(1),
                    HueRotation = style.BackdropFilterHueRotate!.Value.GetPixels(1),
                    Blur = style.BackdropFilterBlur!.Value.GetPixels(1),
                } : default;

                Background.Update(panel, style.BackgroundImage, style.BackgroundGradient, _sampling,
                    style.BackgroundAngle!.Value.GetPixels(1), ParseBlendMode(style.BackgroundBlendMode) ?? BlendMode.Normal, FillInsets);
                Background.Descriptor.Radii = OuterRadii;
                Background.Descriptor.Stroke = border;
                Background.Descriptor.BorderImage = borderImage;
                Painter.SetBorderShape(ref Background.Descriptor, style.BorderShape);
                HasBorder = border.HasInk || Background.Descriptor.HasBorderImage;
                panel.TransformMatrix = style.BuildTransformMatrix(rect.Size);
            }

            Background.Descriptor.Rect = rect;

            var outer = panel.Box.RectOuter;
            if (geometryChanged || _outerRect != outer)
            {
                if (outer.Width > 1 && outer.Height > 1
                    && (HasFilter || style.FilterDropShadow is { Count: > 0 } || style.MaskImage is not null || style.Isolation == Isolation.Isolate))
                {
                    Layer ??= new LayerPaint();
                    Layer.Update(panel, _sampling);
                }
                else
                {
                    Layer = null;
                }
            }

            if (geometryChanged || _rect != rect || _parentMatrix != panel.VisualParent?.GlobalMatrix)
            {
                UpdateTransform(panel);
            }

            ClipTransform = panel.GlobalMatrix ?? Matrix4x4.Identity;
            ContentClipRect = panel.ContentClipRect;
            _fontSize = style.FontSize;
            _rootFontSize = Length.RootFontSize;
            _fontColor = style.FontColor;
            _mixBlendMode = style.MixBlendMode;
            _imageRendering = style.ImageRendering;
            _rect = rect;
            _outerRect = outer;
            _dirty = false;
        }

        private void UpdateTransform(Panel panel)
        {
            var parent = panel.VisualParent;
            var global = parent?.GlobalMatrix;
            _parentMatrix = global;
            var inverse = parent?.GlobalMatrixInverted;
            Matrix4x4? local = null;
            var style = panel.ComputedStyle!;
            if (style.Transform is { IsEmpty: false } && panel.TransformMatrix != Matrix4x4.Identity)
            {
                var rect = panel.Box.Rect;
                var origin = new Vector3(
                    rect.Left + style.TransformOriginX!.Value.GetPixels(rect.Width, 0),
                    rect.Top + style.TransformOriginY!.Value.GetPixels(rect.Height, 0),
                    0);
                if (inverse is { } parentInverse)
                {
                    origin = Vector3.Transform(origin, parentInverse);
                }

                var transform = (inverse ?? Matrix4x4.Identity) * Matrix4x4.CreateTranslation(-origin) * panel.TransformMatrix * Matrix4x4.CreateTranslation(origin);
                var matrix = transform.Inverted;
                local = inverse is { } value ? value * matrix : matrix;
                global = matrix;
                inverse = transform;
            }

            if (panel.GlobalMatrix != global)
            {
                panel.SetGlobalMatrix(global, inverse);
            }

            panel.LocalMatrix = local;
        }

        private static BlendMode? ParseBlendMode(string? mode) => mode switch
        {
            "lighten" => BlendMode.Lighten,
            "multiply" => BlendMode.Multiply,
            "normal" => BlendMode.Normal,
            _ => null,
        };

        internal static FilterMode SamplingFor(ImageRendering? rendering) => rendering switch
        {
            ImageRendering.Point => FilterMode.Point,
            ImageRendering.Bilinear => FilterMode.Bilinear,
            ImageRendering.Trilinear => FilterMode.Trilinear,
            _ => FilterMode.Anisotropic,
        };

        internal struct ImagePlacement
        {
            internal bool HasFill;
            internal Texture? Texture;
            internal Painter.BoxDescriptor Descriptor;

            internal void Update(Panel panel, Texture? texture, in GradientInfo gradient, FilterMode sampling, float angle, BlendMode blend, in Vector4 fillInsets)
            {
                var style = panel.ComputedStyle!;
                var tile = texture is not null || !gradient.IsEmpty ? ImageRect.Calculate(new ImageRect.Input
                {
                    ScaleToScreen = panel.ScaleToScreen,
                    Image = texture,
                    PanelRect = panel.Box.Rect,
                    DefaultSize = Length.Auto,
                    ImagePositionX = style.BackgroundPositionX,
                    ImagePositionY = style.BackgroundPositionY,
                    ImageSizeX = style.BackgroundSizeX,
                    ImageSizeY = style.BackgroundSizeY,
                }) : default;
                var fill = new Fill(style.BackgroundColor!.Value, texture, gradient, tile, style.BackgroundTint!.Value,
                    style.BackgroundRepeat ?? BackgroundRepeat.Repeat, sampling, angle, blend);
                Descriptor = fill.CreateDescriptor(panel.Box.Rect, 1, BlendMode.Normal, fillInsets);
                HasFill = !fill.IsTransparent;
                Texture = texture;
            }
        }
    }
}
