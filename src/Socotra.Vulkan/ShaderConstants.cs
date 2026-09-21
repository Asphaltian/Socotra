using System.Runtime.InteropServices;

namespace Socotra.Vulkan;

[StructLayout(LayoutKind.Sequential, Pack = 4)]
internal struct BoxConstants
{
    public Vector4 Viewport;
    public Matrix4x4 LayerMat;
    public int InstanceOffset;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
internal struct DownsampleConstants
{
    public int SourceTextureIndex;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
internal struct TextRasterConstants
{
    public int InstanceOffset;
    public int TileOffset;
    public int TilesX;
    public int Width;
    public int Height;
    public int MipLevel;
    public Vector3 BaseColor;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
internal struct QuadDraw
{
    public Matrix4x4 TransformMat;
    public Matrix4x4 LayerMat;
    public Vector4 Viewport;
    public Vector4 QuadRect;
    public Color QuadColor;
    public Vector2 BoxSize;
    public int SoftwareScissorIndex;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
internal struct FilterConstants
{
    public QuadDraw Quad;
    public int PainterScissorIndex;
    public int TextureIndex;
    public float FilterBrightness;
    public float FilterHueRotate;
    public float FilterBlur;
    public float FilterSaturate;
    public float FilterSepia;
    public float FilterInvert;
    public float FilterContrast;
    public Color FilterTint;
    public int MaskTextureIndex;
    public int MaskMode;
    public int MaskScope;
    public float MaskAngle;
    public int SamplerIndex;
    public int BorderSamplerIndex;
    public Vector4 MaskPos;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
internal struct BackdropConstants
{
    public QuadDraw Quad;
    public int PainterScissorIndex;
    public Vector4 CornerRadius;
    public Vector4 CornerRadiusV;
    public float Brightness;
    public float Contrast;
    public float Saturate;
    public float Invert;
    public float HueRotate;
    public float Sepia;
    public float BlurScale;
    public int FrameBufferCopyTextureIndex;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
internal struct DropShadowConstants
{
    public QuadDraw Quad;
    public int TextureIndex;
    public Vector2 FilterDropShadowOffset;
    public float FilterDropShadowBlur;
    public Color FilterDropShadowColor;
    public Vector2 FilterDropShadowScale;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
internal struct BorderWrapConstants
{
    public QuadDraw Quad;
    public int TextureIndex;
    public Color FilterBorderWrapColor;
    public Vector2 FilterBorderWrapColorScale;
    public float FilterBorderWrapWidth;
}
