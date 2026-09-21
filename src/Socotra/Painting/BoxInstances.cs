using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Socotra;

[StructLayout(LayoutKind.Sequential)]
internal struct BoxInstance
{
    public const uint TextFlag = 1u << 14;

    public Vector4 Rect;
    public Color Color;
    public Vector4 BorderRadius;
    public Vector4 BorderRadiusV;
    public Vector4 BorderSize;
    public Color BorderColorL;
    public Color BorderColorT;
    public Color BorderColorR;
    public Color BorderColorB;
    public int TextureIndex;
    public int SamplerIndex;
    public float BackgroundAngle;
    public Vector4 BackgroundRect;
    public Color BackgroundTint;
    public int BorderImageIndex;
    public int BorderImageSamplerIndex;
    public Vector4 BorderImageSlice;
    public Color BorderImageTint;
    public uint Flags;
    public int ScissorIndex;
    public int TransformIndex;
    public int InverseScissorIndex;
    public int TextMaskIndex;
    public int TextMaskSamplerIndex;
    public Vector4 BackgroundClipRect;
    public int ShapeIndex;

    public int Mode
    {
        readonly get => (int)(Flags & 0x3u);
        set => SetFlags(value, 0, 0x3u);
    }

    public int BorderImageMode
    {
        readonly get => (int)((Flags >> 2) & 0x3u);
        set => SetFlags(value, 2, 0x3u);
    }

    public int BorderStyle
    {
        readonly get => (int)((Flags >> 4) & 0xFu);
        set => SetFlags(value, 4, 0xFu);
    }

    public int BorderImageFill
    {
        readonly get => (int)((Flags >> 8) & 0x1u);
        set => SetFlags(value, 8, 0x1u);
    }

    public int BackgroundRepeat
    {
        readonly get => (int)((Flags >> 9) & 0x7u);
        set => SetFlags(value, 9, 0x7u);
    }

    public int BackgroundClip
    {
        readonly get => (int)((Flags >> 12) & 0x3u);
        set => SetFlags(value, 12, 0x3u);
    }

    public int BackgroundBlend
    {
        readonly get => (int)((Flags >> 15) & 0x3u);
        set => SetFlags(value, 15, 0x3u);
    }

    public static BoxInstance FromShadow(in Painter.ShadowDescriptor desc)
    {
        var spread = desc.Inset ? -desc.Spread : desc.Spread;
        var shape = (desc.Rect + desc.Offset).Grow(spread);
        var radii = desc.Radii.Grow(spread);
        var quad = desc.Inset ? desc.Rect : shape.Grow(MathF.Ceiling(desc.Blur * 1.5f));
        return new BoxInstance
        {
            Rect = new Vector4(quad.Left, quad.Top, quad.Width, quad.Height),
            Color = desc.Color,
            BorderRadius = radii.Horizontal,
            BorderRadiusV = radii.Vertical,
            BackgroundAngle = desc.Blur,
            BackgroundRect = new Vector4(shape.Left - quad.Left, shape.Top - quad.Top, shape.Width, shape.Height),
            Mode = desc.Inset ? 2 : 1,
            InverseScissorIndex = -1,
            ShapeIndex = -1,
        };
    }

    public static BoxInstance FromOutline(in Painter.OutlineDescriptor desc)
    {
        var bloat = MathF.Max(desc.Offset + desc.Width, 0f) + 1.0f;
        var quad = desc.Rect.Grow(bloat);
        var radii = desc.Radii.Clamped(desc.Rect.Width, desc.Rect.Height);
        return new BoxInstance
        {
            Rect = new Vector4(quad.Left, quad.Top, quad.Width, quad.Height),
            Color = desc.Color,
            BorderRadius = radii.Horizontal,
            BorderRadiusV = radii.Vertical,
            BackgroundRect = new Vector4(desc.Rect.Width, desc.Rect.Height, desc.Width, desc.Offset),
            BackgroundAngle = bloat,
            Mode = 3,
            InverseScissorIndex = -1,
            ShapeIndex = -1,
        };
    }

    public static BoxInstance From(in Painter.BoxDescriptor desc)
    {
        var hasImage = desc.HasImage;
        var hasBorderImage = desc.HasBorderImage;
        ref readonly var border = ref desc.Stroke;
        ref readonly var image = ref desc.BorderImage;
        var hasBackground = hasImage || desc.HasGradient;
        var backgroundRect = hasBackground
            ? desc.BackgroundRect.Z > 0 || desc.BackgroundRect.W > 0 ? desc.BackgroundRect : new Vector4(0, 0, desc.Rect.Width, desc.Rect.Height)
            : Vector4.Zero;
        var radii = desc.Radii.Clamped(desc.Rect.Width, desc.Rect.Height);
        return new BoxInstance
        {
            Rect = new Vector4(desc.Rect.Left, desc.Rect.Top, desc.Rect.Width, desc.Rect.Height),
            Color = desc.Color,
            BorderRadius = radii.Horizontal,
            BorderRadiusV = radii.Vertical,
            BorderSize = border.Size,
            BorderColorL = border.ColorL,
            BorderColorT = border.ColorT,
            BorderColorR = border.ColorR,
            BorderColorB = border.ColorB,
            SamplerIndex = hasImage ? BackgroundSampler(desc.BackgroundRepeat, desc.FilterMode) : 0,
            BackgroundRepeat = (int)desc.BackgroundRepeat,
            BackgroundAngle = desc.BackgroundAngle,
            BackgroundRect = backgroundRect,
            BackgroundTint = hasBackground ? desc.BackgroundTint : Color.Transparent,
            BorderImageSamplerIndex = hasBorderImage ? ClampSampler(desc.FilterMode) : 0,
            BorderImageMode = hasBorderImage ? image.Repeat == BorderImageRepeat.Stretch ? 2 : 1 : 0,
            BorderImageFill = hasBorderImage && image.Fill == Socotra.BorderImageFill.Filled ? 1 : 0,
            BorderImageSlice = image.Slices,
            BorderImageTint = hasBorderImage ? image.Tint : default,
            BorderStyle = (int)border.Style,
            InverseScissorIndex = -1,
            BackgroundClip = (int)desc.BackgroundClip,
            BackgroundBlend = desc.BackgroundBlendMode switch
            {
                BlendMode.Multiply => 1,
                BlendMode.Lighten => 2,
                _ => 0,
            },
            BackgroundClipRect = desc.BackgroundClip == Socotra.BackgroundClip.Text ? desc.TextMaskRect : desc.BackgroundClipInset,
            TextMaskSamplerIndex = desc.HasTextMask ? ClampSampler(FilterMode.Bilinear) : 0,
            ShapeIndex = -1,
        };
    }

    public void ApplyOpacity(float opacity)
    {
        if (opacity == 1)
        {
            return;
        }

        Color = Color.WithAlphaMultiplied(opacity);
        BackgroundTint = BackgroundTint.WithAlphaMultiplied(opacity);
        BorderColorL = BorderColorL.WithAlphaMultiplied(opacity);
        BorderColorT = BorderColorT.WithAlphaMultiplied(opacity);
        BorderColorR = BorderColorR.WithAlphaMultiplied(opacity);
        BorderColorB = BorderColorB.WithAlphaMultiplied(opacity);
        BorderImageTint = BorderImageTint.WithAlphaMultiplied(opacity);
    }

    public static int BackgroundSampler(Socotra.BackgroundRepeat repeat, FilterMode filter) => repeat switch
    {
        Socotra.BackgroundRepeat.RepeatX => Samplers.Index(filter, TextureAddress.Wrap, TextureAddress.Clamp),
        Socotra.BackgroundRepeat.RepeatY => Samplers.Index(filter, TextureAddress.Clamp, TextureAddress.Wrap),
        Socotra.BackgroundRepeat.NoRepeat => Samplers.Index(filter, TextureAddress.Border, TextureAddress.Border),
        Socotra.BackgroundRepeat.Clamp => Samplers.Index(filter, TextureAddress.Clamp, TextureAddress.Clamp),
        _ => Samplers.Index(filter, TextureAddress.Wrap, TextureAddress.Wrap),
    };

    private static int ClampSampler(FilterMode filter) => Samplers.Index(filter, TextureAddress.Clamp, TextureAddress.Clamp);

    private void SetFlags(int value, int shift, uint mask) => Flags = (Flags & ~(mask << shift)) | (((uint)value & mask) << shift);
}

[StructLayout(LayoutKind.Sequential)]
internal struct TextInstance
{
    public Vector4 Rect;
    public Color Color;
    public Vector4 BorderRadius;
    public Vector4 BorderRadiusV;
    public Vector4 BorderSize;
    public Color BorderColorL;
    public Color BorderColorT;
    public Color BorderColorR;
    public Color BorderColorB;
    public int TextureIndex;
    public int SamplerIndex;
    public int BackgroundRepeat;
    public float BackgroundAngle;
    public Vector4 BackgroundRect;
    public Color BackgroundTint;
    public int BorderImageIndex;
    public int BorderImageSamplerIndex;
    public int BorderImageMode;
    public int BorderImageFill;
    public Vector4 BorderImageSlice;
    public Color BorderImageTint;
    public int Flags;
    public int ScissorIndex;
    public int Mode;
    public int TransformIndex;
    public int InverseScissorIndex;
    public int TextMaskIndex;
    public int TextMaskSamplerIndex;
    public int BackgroundClip;
    public Vector4 BackgroundClipRect;
    public int ShapeIndex;
}

[InlineArray(GradientInfo.MaxStops)]
internal struct GradientStopColors
{
    private Color _element;
}

[InlineArray(GradientInfo.MaxStops)]
internal struct GradientStopOffsets
{
    private float _element;
}

[StructLayout(LayoutKind.Sequential)]
internal struct GradientInstance
{
    private const int CenterXIsFraction = 1;
    private const int CenterYIsFraction = 2;

    public GradientStopColors StopColors;
    public GradientStopOffsets StopOffsets;
    public int Count;
    public float Angle;
    public int Type;
    public int SizeMode;
    public Vector2 Center;
    public int CenterUnits;
    public int Circle;
    public int StopUnits;
    public int Corner;

    public static GradientInstance From(in GradientInfo gradient)
    {
        var count = Math.Min(gradient.Stops.Count, GradientInfo.MaxStops);
        var instance = new GradientInstance
        {
            Count = count,
            Angle = gradient.Angle,
            Type = (int)gradient.Type,
            SizeMode = (int)gradient.Size,
            Circle = gradient.Circle ? 1 : 0,
            Corner = (int)gradient.Corner,
            Center = new Vector2(gradient.CenterX.GetPixels(1f), gradient.CenterY.GetPixels(1f)),
        };

        if (gradient.CenterX.Unit != LengthUnit.Pixels)
        {
            instance.CenterUnits |= CenterXIsFraction;
        }

        if (gradient.CenterY.Unit != LengthUnit.Pixels)
        {
            instance.CenterUnits |= CenterYIsFraction;
        }

        for (int i = 0; i < count; i++)
        {
            var stop = gradient.Stops[i];
            instance.StopColors[i] = stop.Color;
            instance.StopOffsets[i] = stop.Offset ?? 0f;
            if (stop.OffsetIsPixels)
            {
                instance.StopUnits |= 1 << i;
            }
        }

        return instance;
    }
}

internal static class ShapeKind
{
    public const int None = 0;
    public const int Polygon = 1;
    public const int Circle = 2;
    public const int PolygonPath = 3;
    public const int StrokePath = 4;
    public const int Capsule = 5;
    public const int Crescent = 6;
    public const int Heart = 7;
    public const int FirstAnalytic = Capsule;
}

[StructLayout(LayoutKind.Sequential)]
internal record struct ShapeInstance
{
    public Vector4 Polygon01;
    public Vector4 Polygon23;
    public Vector4 Polygon45;
    public Vector4 Polygon67;
    public int PolygonCount;
    public Vector4 Circle;
    public int PathOffset;
    public int PathCount;
    public int PathNodeOffset;
    public int PathNodeCount;
    public int Kind;
}

[StructLayout(LayoutKind.Sequential)]
internal struct PathNode
{
    public Vector4 Bounds;
    public int Next;
    public int Primitive;
}

internal static class PathPrimitiveKind
{
    public const int Segment = 0;
    public const int Disc = 1;
    public const int Join = 2;
    public const int Arc = 3;
    public const int RoundJoin = 4;
}

internal static class PathCap
{
    public const int Ring = -1;
    public const int SquareStart = 1;
    public const int SquareEnd = 2;
}

[StructLayout(LayoutKind.Sequential)]
internal struct PathPrimitive
{
    public Vector4 A;
    public Vector4 B;
    public Vector4 C;
    public int Kind;
    public int Count;
}

[StructLayout(LayoutKind.Sequential)]
internal struct ScissorInstance
{
    public int Count;
    public int Invert;
    public int Next;
    public int Padding;
    public ClipShapes Clips;

    public static ScissorInstance From(in Painter.Scissoring scissor, int next = -1)
    {
        var instance = new ScissorInstance { Count = scissor.Count, Invert = scissor.Invert ? 1 : 0, Next = next };
        for (int i = 0; i < scissor.Count; i++)
        {
            var clip = scissor.Clips[i].ForShader();
            instance.Clips[i] = new ClipShapeInstance
            {
                Rect = clip.Rect.ToVector4(),
                RadiiH = clip.Radii.Horizontal,
                RadiiV = clip.Radii.Vertical,
                Transform = clip.Transform,
            };
        }

        return instance;
    }
}

[StructLayout(LayoutKind.Sequential)]
internal struct ClipShapeInstance
{
    public Vector4 Rect;
    public Vector4 RadiiH;
    public Vector4 RadiiV;
    public Matrix4x4 Transform;
}

[InlineArray(Painter.Scissoring.MaxClips)]
internal struct ClipShapes
{
    private ClipShapeInstance _element;
}
