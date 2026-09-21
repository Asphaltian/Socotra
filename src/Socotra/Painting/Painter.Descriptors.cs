namespace Socotra;

public readonly ref partial struct Painter
{
    internal record struct BoxDescriptor(Rect Rect, Color Color)
    {
        internal BorderRadii Radii;

        internal BoxStroke Stroke;
        internal NineSliceImage BorderImage;

        internal BackgroundClip BackgroundClip;

        internal Vector4 BackgroundClipInset;

        internal Texture? TextMask;
        internal Vector4 TextMaskRect;

        internal Texture? BackgroundImage;
        internal Vector4 BackgroundRect;
        internal Color BackgroundTint;
        internal float BackgroundAngle;
        internal BackgroundRepeat BackgroundRepeat;
        internal FilterMode FilterMode;

        internal BlendMode BackgroundBlendMode;
        internal BlendMode OverrideBlendMode;

        internal GradientInfo BackgroundGradient;

        internal ShapeInstance BorderShapeData;
        internal Painter.Path.Data? PathData;

        internal readonly bool HasImage => BackgroundImage is not null;
        internal readonly bool HasGradient => !BackgroundGradient.IsEmpty;
        internal readonly bool HasBorderImage => BorderImage.Texture is not null;
        internal readonly bool HasTextMask => TextMask is not null;

        internal void SetPolygon(ReadOnlySpan<Vector2> points)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(points.Length, 3);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(points.Length, BorderShape.MaxPoints);

            Span<Vector2> padded = stackalloc Vector2[BorderShape.MaxPoints];
            padded.Clear();
            points.CopyTo(padded);

            BorderShapeData = new ShapeInstance
            {
                Kind = ShapeKind.Polygon,
                Polygon01 = Pack(padded, 0),
                Polygon23 = Pack(padded, 2),
                Polygon45 = Pack(padded, 4),
                Polygon67 = Pack(padded, 6),
                PolygonCount = points.Length,
            };
        }

        static Vector4 Pack(ReadOnlySpan<Vector2> points, int i) => new(points[i].X, points[i].Y, points[i + 1].X, points[i + 1].Y);
    }

    internal record struct BoxStroke
    {
        internal Vector4 Size;
        internal BorderStyle Style;
        internal Color ColorL;
        internal Color ColorT;
        internal Color ColorR;
        internal Color ColorB;

        internal readonly bool HasInk => Style is not (BorderStyle.None or BorderStyle.Hidden)
            && (Size.X > 0 && ColorL.A != 0 || Size.Y > 0 && ColorT.A != 0
                || Size.Z > 0 && ColorR.A != 0 || Size.W > 0 && ColorB.A != 0);

        internal readonly BoxStroke WithAlphaMultiplied(float opacity) => this with
        {
            ColorL = ColorL.WithAlphaMultiplied(opacity),
            ColorT = ColorT.WithAlphaMultiplied(opacity),
            ColorR = ColorR.WithAlphaMultiplied(opacity),
            ColorB = ColorB.WithAlphaMultiplied(opacity),
        };
    }

    internal static void SetBorderShape(ref Painter.BoxDescriptor desc, BorderShape? shape)
    {
        desc.BorderShapeData = default;
        if (shape?.IsNone != false)
        {
            return;
        }

        desc.BorderShapeData.Kind = (int)shape.Kind;

        if (shape.Kind == BorderShapeKind.Circle)
        {
            var circle = shape.ResolveCircle(new Rect(Vector2.Zero, desc.Rect.Size));
            desc.BorderShapeData.Circle = new Vector4(circle.Center.X, circle.Center.Y, circle.Radius, 0);
            return;
        }

        Span<Vector2> points = stackalloc Vector2[BorderShape.MaxPoints];

        for (int i = 0; i < shape.Points.Count; i++)
        {
            points[i] = new Vector2(
                shape.Points[i].X.GetPixels(desc.Rect.Width),
                shape.Points[i].Y.GetPixels(desc.Rect.Height));
        }

        desc.SetPolygon(points[..shape.Points.Count]);
    }

    internal record struct ShadowDescriptor(Rect Rect, Color Color)
    {
        internal BorderRadii Radii;

        internal Vector2 Offset;
        internal float Blur;
        internal float Spread;
        internal bool Inset;

        internal BlendMode OverrideBlendMode;
    }

    internal record struct OutlineDescriptor(Rect Rect, Color Color, float Width)
    {
        internal BorderRadii Radii;

        internal float Offset;

        internal BlendMode OverrideBlendMode;
    }
}
