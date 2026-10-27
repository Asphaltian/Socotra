#pragma once

static const int SamplerTrilinearBorder = 0;
static const int SamplerTrilinearClamp = 1;

static const int UI_SHAPE_CIRCLE = 2;
static const int UI_SHAPE_POLYGON_PATH = 3;
static const int UI_SHAPE_STROKE_PATH = 4;
static const int UI_SHAPE_CAPSULE = 5;
static const int UI_SHAPE_CRESCENT = 6;
static const int UI_SHAPE_HEART = 7;
static const int UI_SHAPE_SIMPLE_LINE = 8;
static const int UI_SHAPE_POLYGON_STROKE = 9;
static const int UI_SHAPE_FIRST_ANALYTIC = UI_SHAPE_CAPSULE;

static const int UI_PATH_SEGMENT = 0;
static const int UI_PATH_DISC = 1;
static const int UI_PATH_JOIN = 2;
static const int UI_PATH_ARC = 3;
static const int UI_PATH_ROUND_JOIN = 4;

static const int UI_CAP_SQUARE = 1;
static const int UI_CAP_ROUND = 2;
static const int UI_CAP_TRIANGLE = 3;
static const int UI_CAP_ARROW = 4;
static const int UI_CAP_RING = -1;
static const int UI_CAP_SQUARE_START = 1;
static const int UI_CAP_SQUARE_END = 2;

static const int MAX_CLIPS = 4;

static const float EPSILON = 0.000001;

float2 QuadPosition(uint i)
{
    return float2(float(i & 1), float(i >> 1));
}

struct BoxInstanceData
{
    float4 Rect;
    float4 Color;
    float4 BorderRadius;
    float4 BorderRadiusV;
    float4 BorderSize;
    float4 BorderColorL;
    float4 BorderColorT;
    float4 BorderColorR;
    float4 BorderColorB;
    int TextureIndex;
    int SamplerIndex;
    float BackgroundAngle;
    float4 BackgroundRect;
    float4 BackgroundTint;
    int BorderImageIndex;
    int BorderImageSamplerIndex;
    float4 BorderImageSlice;
    float4 BorderImageTint;
    uint Flags;
    int ScissorIndex;
    int TransformIndex;
    int InverseScissorIndex;
    int TextMaskIndex;
    int TextMaskSamplerIndex;
    float4 BackgroundClipRect;
    int ShapeIndex;

    int GetMode() { return int((Flags >> 0) & 0x3u); }
    int GetBorderImageMode() { return int((Flags >> 2) & 0x3u); }
    int GetBorderStyle() { return int((Flags >> 4) & 0xFu); }
    int GetBorderImageFill() { return int((Flags >> 8) & 0x1u); }
    int GetBackgroundRepeat() { return int((Flags >> 9) & 0x7u); }
    int GetBackgroundClip() { return int((Flags >> 12) & 0x3u); }
    int GetBackgroundBlend() { return int((Flags >> 15) & 0x3u); }
};

struct TextInstanceData
{
    float4 Rect;
    float4 Color;
    float4 BorderRadius;
    float4 BorderRadiusV;
    float4 BorderSize;
    float4 BorderColorL;
    float4 BorderColorT;
    float4 BorderColorR;
    float4 BorderColorB;
    int TextureIndex;
    int SamplerIndex;
    int BackgroundRepeat;
    float BackgroundAngle;
    float4 BackgroundRect;
    float4 BackgroundTint;
    int BorderImageIndex;
    int BorderImageSamplerIndex;
    int BorderImageMode;
    int BorderImageFill;
    float4 BorderImageSlice;
    float4 BorderImageTint;
    int Flags;
    int ScissorIndex;
    int Mode;
    int TransformIndex;
    int InverseScissorIndex;
    int TextMaskIndex;
    int TextMaskSamplerIndex;
    int BackgroundClip;
    float4 BackgroundClipRect;
    int ShapeIndex;
};

struct GradientData
{
    float4 StopColors[8];
    float StopOffsets[8];
    int Count;
    float Angle;
    int Type;
    int SizeMode;
    float2 Center;
    int CenterUnits;
    int Circle;
    int StopUnits;
    int Corner;
};

struct BorderShapeData
{
    float4 Polygon01;
    float4 Polygon23;
    float4 Polygon45;
    float4 Polygon67;
    int PolygonCount;
    float4 Circle;
    int PathOffset;
    int PathCount;
    int PathNodeOffset;
    int PathNodeCount;
    int Kind;
};

struct PathPrimitiveData
{
    float4 A;
    float4 B;
    float4 C;
    int Kind;
    int Count;
};

struct PathNodeData
{
    float4 Bounds;
    int Next;
    int Primitive;
};

struct TransformData
{
    float4x4 Mat;
};

struct ClipShape
{
    float4 Rect;
    float4 RadiiH;
    float4 RadiiV;
    float4x4 TransformMat;
};

struct ScissorData
{
    int Count;
    int Invert;
    int Next;
    int Pad1;
    ClipShape Clips[MAX_CLIPS];
};

struct GpuFontGlyph
{
    float4 Bounds;
    int BandOffset;
    int BandCount;
    int CurveStart;
    int CurveCount;
    int Index;
};

[[vk::binding(0, 0)]] StructuredBuffer<BoxInstanceData> BoxInstances;
[[vk::binding(1, 0)]] StructuredBuffer<TextInstanceData> PainterTextInstances;
[[vk::binding(2, 0)]] StructuredBuffer<ScissorData> ScissorBuffer;
[[vk::binding(3, 0)]] StructuredBuffer<TransformData> TransformBuffer;
[[vk::binding(4, 0)]] StructuredBuffer<GradientData> GradientBuffer;
[[vk::binding(5, 0)]] StructuredBuffer<BorderShapeData> BorderShapeBuffer;
[[vk::binding(6, 0)]] StructuredBuffer<PathPrimitiveData> PathBuffer;
[[vk::binding(7, 0)]] StructuredBuffer<PathNodeData> PathNodeBuffer;
[[vk::binding(8, 0)]] StructuredBuffer<float2> PolygonPointBuffer;
[[vk::binding(9, 0)]] StructuredBuffer<float2> GlyphCurves;
[[vk::binding(10, 0)]] StructuredBuffer<uint> GlyphBands;
[[vk::binding(11, 0)]] StructuredBuffer<GpuFontGlyph> GlyphTable;
[[vk::binding(12, 0)]] StructuredBuffer<uint> TextTiles;

[[vk::binding(0, 1)]] Texture2D Textures[];
[[vk::binding(1, 1)]] SamplerState Samplers[];

Texture2D GetTexture2D(int index)
{
    return Textures[NonUniformResourceIndex(index)];
}

SamplerState GetSamplerNonUniform(int index)
{
    return Samplers[NonUniformResourceIndex(index)];
}

SamplerState GetSampler(int index)
{
    return Samplers[index];
}

float2 TextureDimensions2D(Texture2D tex, uint mip)
{
    uint width, height, levels;
    tex.GetDimensions(mip, width, height, levels);
    return float2(width, height);
}

float2 RotateTexCoord(float2 vTexCoord, float angle, float2 offset = 0.5)
{
    float2x2 m = float2x2(cos(angle), -sin(angle), sin(angle), cos(angle));
    return mul(m, vTexCoord - offset) + offset;
}

float3 SrgbGammaToLinear(float3 c)
{
    return select(c <= 0.04045, c / 12.92, pow((c + 0.055) / 1.055, 2.4));
}

float3 SrgbLinearToGamma(float3 c)
{
    return select(c <= 0.0031308, c * 12.92, 1.055 * pow(c, 1.0 / 2.4) - 0.055);
}

float2 ShapePoint(float4 p01, float4 p23, float4 p45, float4 p67, int i)
{
    if (i < 2) return i == 0 ? p01.xy : p01.zw;
    if (i < 4) return i == 2 ? p23.xy : p23.zw;
    if (i < 6) return i == 4 ? p45.xy : p45.zw;
    return i == 6 ? p67.xy : p67.zw;
}

float ShapeSdf(float2 p, float4 p01, float4 p23, float4 p45, float4 p67, int count, float4 circle, int kind)
{
    if (kind == UI_SHAPE_CIRCLE) return length(p - circle.xy) - circle.z;
    if (kind == UI_SHAPE_CAPSULE)
    {
        float2 delta = p01.xy - circle.xy;
        float len = length(delta);
        float dr = circle.w - circle.z;
        float2 axis = delta / max(len, 0.000001);
        float2 q = p - circle.xy;
        float x = dot(q, axis), y = abs(dot(q, float2(-axis.y, axis.x)));
        float t = saturate((x + dr * y / sqrt(max(len * len - dr * dr, 0.000001))) / max(len, 0.000001));
        return length(q - delta * t) - lerp(circle.z, circle.w, t);
    }
    if (kind == UI_SHAPE_CRESCENT)
        return max(length(p - circle.xy) - circle.z, circle.w - length(p - p01.xy));
    if (kind == UI_SHAPE_HEART)
    {
        float2 q = (p - circle.xy) / circle.z;
        float diamond = (abs(q.x) + abs(q.y) - p01.z) * 0.70710678;
        float lobes = length(float2(abs(q.x) - p01.x, q.y + p01.y)) - circle.w;
        return min(diamond, lobes) * circle.z;
    }
    float2 first = ShapePoint(p01, p23, p45, p67, 0);
    float d = dot(p - first, p - first), s = 1.0; int j = count - 1;
    for (int i = 0; i < 8; i++)
    {
        if (i >= count) break;
        float2 vi = ShapePoint(p01, p23, p45, p67, i), vj = ShapePoint(p01, p23, p45, p67, j);
        float2 e = vj - vi, w = p - vi;
        float2 b = w - e * clamp(dot(w, e) / max(dot(e, e), 0.000001), 0.0, 1.0);
        d = min(d, dot(b, b));
        bool3 c = bool3(p.y >= vi.y, p.y < vj.y, e.x * w.y > e.y * w.x);
        if (all(c) || all(!c)) s *= -1.0; j = i;
    }
    return s * sqrt(d);
}

float ShapeSdf(float2 p, BorderShapeData shape)
{
    return ShapeSdf(p, shape.Polygon01, shape.Polygon23, shape.Polygon45, shape.Polygon67, shape.PolygonCount, shape.Circle, shape.Kind);
}
