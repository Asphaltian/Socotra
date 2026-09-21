#include "UI.Common.slang"
#include "UI.RoundedRect.slang"
#include "UI.Scissor.slang"
#include "UI.ShapePath.slang"
#include "UI.Text.slang"

[SpecializationConstant] const int BlendMode = 0;

struct BoxDrawConstants
{
    float4 Viewport;
    float4x4 LayerMat;
    int InstanceOffset;
};

[[vk::push_constant]] ConstantBuffer<BoxDrawConstants> Draw;

static const float BOX_BLOAT = 1.0;

struct PixelInput
{
    float4 vColor : COLOR0;
    float4 vTexCoord : TEXCOORD0;
    float4 vPositionPanelSpace : TEXCOORD2;
    float3 vPathPosition : TEXCOORD4;
    nointerpolation uint iInstanceID : TEXCOORD3;
    float4 vPositionPs : SV_Position;
};

#include "UI.PathQuad.slang"

[shader("vertex")]
PixelInput VertexMain(uint nVertexID : SV_VulkanVertexID, uint nInstanceID : SV_VulkanInstanceID)
{
    PixelInput o;

    uint instanceIndex = nInstanceID + Draw.InstanceOffset;
    float2 corner = QuadPositions[nVertexID];
    BoxInstanceData inst = BoxInstances[instanceIndex];
    float4x4 instTransform = TransformBuffer[inst.TransformIndex].Mat;
    if (inst.ShapeIndex >= 0)
    {
        int kind = BorderShapeBuffer[inst.ShapeIndex].Kind;
        if (kind == UI_SHAPE_POLYGON_PATH || kind == UI_SHAPE_STROKE_PATH)
            return PathQuad(instanceIndex, corner, inst, instTransform);
    }
    o.vPathPosition = float3(0, 0, 1);

    float2 vLocal = inst.Rect.xy - BOX_BLOAT + corner * (inst.Rect.zw + BOX_BLOAT * 2.0);
    float2 vPositionSs = vLocal;

    float4 vViewport = Draw.Viewport;
    float4 vMatrix = mul(Draw.LayerMat, mul(instTransform, float4(vPositionSs, 0, 1)));

    float safeW = abs(vMatrix.w) > EPSILON ? vMatrix.w : (vMatrix.w < 0.0 ? -EPSILON : EPSILON);
    vPositionSs = vMatrix.xy / safeW;

    o.vPositionPs.xy = 2.0 * (vMatrix.xy - vViewport.xy * vMatrix.w) / vViewport.zw - vMatrix.w;
    o.vPositionPs.y *= -1.0;
    o.vPositionPs.z = vMatrix.w;
    o.vPositionPs.w = vMatrix.w * (1.0 + EPSILON);

    o.vPositionPanelSpace = mul(instTransform, float4(vLocal, 0, 1));

    o.vTexCoord.xy = (vLocal - inst.Rect.xy) / max(inst.Rect.zw, 0.0001);
    o.vTexCoord.zw = vPositionSs / vViewport.zw;

    o.vColor.rgb = inst.Color.rgb;
    o.vColor.a = saturate(inst.Color.a);

    o.iInstanceID = instanceIndex;

    return o;
}

float InsetBoxSdf(float2 p, float2 boxSize, float4 radiiH, float4 radiiV, float4 inset)
{
    float2 innerSize = max(boxSize - float2(inset.x + inset.z, inset.y + inset.w), 0.0);
    if (any(innerSize <= 0.0)) return 1e20;
    float2 innerCenter = float2(inset.x - inset.z, inset.y - inset.w) * 0.5;

    float4 innerH = radiiH - inset.xzxz;
    float4 innerV = radiiV - inset.yyww;
    float4 keep = step(0.0001, innerH) * step(0.0001, innerV);

    return RoundedRectSdf(p - innerCenter, innerSize * 0.5, innerH * keep, innerV * keep);
}

float BackgroundClipCoverage(BoxInstanceData inst, float2 pos, float2 boxSize, float2 texCoord)
{
    if (inst.GetBackgroundClip() == 0)
        return 1.0;

    if (inst.GetBackgroundClip() != 3)
        return SdfCoverage(InsetBoxSdf(pos, boxSize, inst.BorderRadius, inst.BorderRadiusV, inst.BackgroundClipRect));

    if (inst.TextMaskIndex == 0)
        return 0.0;

    float2 uv = (texCoord * boxSize - inst.BackgroundClipRect.xy) / inst.BackgroundClipRect.zw;
    if (any(uv < 0.0) || any(uv > 1.0))
        return 0.0;

    Texture2D maskTex = GetTexture2D(inst.TextMaskIndex);
    return saturate(maskTex.Sample(GetSamplerNonUniform(inst.TextMaskSamplerIndex), uv).a);
}

float4 BorderSideColor(float2 pos, float2 boxSize, float4 borderWidth, float4 cL, float4 cT, float4 cR, float4 cB)
{
    float4 has = step(0.0001, borderWidth);
    float4 depth = float4(pos.x, pos.y, boxSize.x - pos.x, boxSize.y - pos.y) / max(borderWidth, 0.0001);
    depth = depth * has + (1.0 - has) * 1e9;

    float4 c1 = cL, c2 = cT;
    float d1 = depth.x, d2 = depth.y;
    if (d2 < d1) { c1 = cT; c2 = cL; d1 = depth.y; d2 = depth.x; }
    if (depth.z < d1) { c2 = c1; d2 = d1; c1 = cR; d1 = depth.z; } else if (depth.z < d2) { c2 = cR; d2 = depth.z; }
    if (depth.w < d1) { c2 = c1; d2 = d1; c1 = cB; d1 = depth.w; } else if (depth.w < d2) { c2 = cB; d2 = depth.w; }

    float diff = d2 - d1;
    float t = saturate(0.5 - diff / max(fwidth(diff), 0.0001));
    return lerp(c1, c2, t);
}

#include "UI.BorderStyle.slang"

float4 AddImageBorder(float2 texCoord, float2 boxSize, float4 borderWidth, int borderImageIndex, int borderImageSamplerIndex, int borderImageMode, int borderImageFill, float4 borderImageSlice)
{
    float4 BorderImageWidth = borderWidth;
    Texture2D borderTex = GetTexture2D(borderImageIndex);
    float2 vBorderImageSize = TextureDimensions2D(borderTex, 0);
    float4 vBorderPixelSize = borderImageSlice;
    float4 vBorderPixelRatio = vBorderPixelSize / float4(vBorderImageSize.x, vBorderImageSize.y, vBorderImageSize.x, vBorderImageSize.y);
    float2 vBoxTexCoord = texCoord * boxSize;
    float2 uv = 0.0;

    if (borderImageFill == 0 &&
        vBoxTexCoord.x > BorderImageWidth.x && vBoxTexCoord.x < boxSize.x - BorderImageWidth.z &&
        vBoxTexCoord.y > BorderImageWidth.y && vBoxTexCoord.y < boxSize.y - BorderImageWidth.w)
        return 0;

    if (vBorderPixelSize.x < vBorderImageSize.x * 0.5)
    {
        if (borderImageMode == 1)
        {
            float2 vMiddleSize = 1.0 - (vBorderPixelRatio.xy + vBorderPixelRatio.zw);
            float2 vRepeatAmount = floor((boxSize * vMiddleSize) / BorderImageWidth.xy);
            uv.x = (vBoxTexCoord.x - BorderImageWidth.x) / (boxSize.x - (BorderImageWidth.x + BorderImageWidth.z)) * vRepeatAmount.x;
            uv.x = fmod(uv.x, vMiddleSize.x) + vBorderPixelRatio.x;
            uv.y = (vBoxTexCoord.y - BorderImageWidth.y) / (boxSize.y - (BorderImageWidth.y + BorderImageWidth.w)) * vRepeatAmount.y;
            uv.y = fmod(uv.y, vMiddleSize.y) + vBorderPixelRatio.y;
        }
        else
        {
            uv.x = (vBoxTexCoord.x - BorderImageWidth.x) / (boxSize.x - (BorderImageWidth.x + BorderImageWidth.z));
            uv.x = uv.x * (1.0 - (vBorderPixelRatio.x + vBorderPixelRatio.z)) + vBorderPixelRatio.x;
            uv.y = (vBoxTexCoord.y - BorderImageWidth.y) / (boxSize.y - (BorderImageWidth.y + BorderImageWidth.w));
            uv.y = uv.y * (1.0 - (vBorderPixelRatio.y + vBorderPixelRatio.w)) + vBorderPixelRatio.y;
        }
    }

    if (vBoxTexCoord.x < BorderImageWidth.x)
        uv.x = (vBoxTexCoord.x / BorderImageWidth.x) * vBorderPixelRatio.x;
    else if (vBoxTexCoord.x > boxSize.x - BorderImageWidth.z)
        uv.x = ((vBoxTexCoord.x - (boxSize.x - BorderImageWidth.z)) / BorderImageWidth.z) * vBorderPixelRatio.z + (1.0 - vBorderPixelRatio.z);

    if (vBoxTexCoord.y < BorderImageWidth.y)
        uv.y = (vBoxTexCoord.y / BorderImageWidth.y) * vBorderPixelRatio.y;
    else if (vBoxTexCoord.y > boxSize.y - BorderImageWidth.w)
        uv.y = ((vBoxTexCoord.y - (boxSize.y - BorderImageWidth.w)) / BorderImageWidth.w) * vBorderPixelRatio.w + (1.0 - vBorderPixelRatio.w);

    return borderTex.Sample(GetSamplerNonUniform(borderImageSamplerIndex), uv);
}

float4 AlphaBlend(float4 src, float4 dest)
{
    float4 result;
    result.a = src.a + (1 - src.a) * dest.a;
    result.rgb = (src.a * src.rgb + (1 - src.a) * dest.a * dest.rgb) / max(result.a, 0.0001);
    return result;
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

float4 RenderShadow(BoxInstanceData inst, PixelInput i, bool inset, out float flCoverage)
{
    float2 boxSize = inst.Rect.zw;
    float2 p = i.vTexCoord.xy * boxSize;

    float4 shape = inst.BackgroundRect;
    float2 half = shape.zw * 0.5;
    float2 q = p - (shape.xy + half);
    float sigma = inst.BackgroundAngle * 0.5;

    float a;
    if (sigma > 0.01)
        a = RoundedRectShadow(q, half, inst.BorderRadius, inst.BorderRadiusV, sigma);
    else
        a = SdfCoverage(RoundedRectSdf(q, half, inst.BorderRadius, inst.BorderRadiusV));

    if (inset) a = 1.0 - a;

    flCoverage = saturate(a);

    float4 col = i.vColor;
    col.a *= flCoverage;
    return col;
}

float4 RenderOutline(BoxInstanceData inst, PixelInput i, out float flCoverage)
{
    float2 panelSize = inst.BackgroundRect.xy;
    float outlineWidth = inst.BackgroundRect.z;
    float outlineOffset = inst.BackgroundRect.w;
    float bloat = inst.BackgroundAngle;

    float2 p = (panelSize + bloat * 2.0) * i.vTexCoord.xy - bloat;
    float d = RoundedRectSdf(p - panelSize * 0.5, panelSize * 0.5, inst.BorderRadius, inst.BorderRadiusV);

    flCoverage = SdfBandCoverage(d - (outlineOffset + outlineWidth), d - outlineOffset);

    float4 col = i.vColor;
    col.a *= flCoverage;
    return col;
}

float GradientPosition(GradientData g, float2 texCoord, float2 boxSize, out float gradLength)
{
    gradLength = 1.0;

    if (g.Type == 0)
    {
        float2 dir = float2(sin(g.Angle), cos(g.Angle));

        if (g.Corner != 0)
        {
            float2 towards = float2((g.Corner == 2 || g.Corner == 4) ? 1 : -1,
                                    (g.Corner == 3 || g.Corner == 4) ? 1 : -1);

            dir = normalize(float2(towards.x * boxSize.y, towards.y * boxSize.x));
        }

        float2 rel = (texCoord - 0.5) * boxSize;
        gradLength = abs(boxSize.x * dir.x) + abs(boxSize.y * dir.y);
        return dot(rel, dir) / max(gradLength, 0.0001) + 0.5;
    }

    float2 p = texCoord * boxSize;

    float2 c;
    c.x = (g.CenterUnits & 1) != 0 ? g.Center.x * boxSize.x : g.Center.x;
    c.y = (g.CenterUnits & 2) != 0 ? g.Center.y * boxSize.y : g.Center.y;

    float2 d = p - c;

    if (g.Type == 2)
    {
        float a = atan2(d.x, -d.y);
        return frac((a - g.Angle) / 6.28318530718);
    }

    float2 nearSide = float2(min(c.x, boxSize.x - c.x), min(c.y, boxSize.y - c.y));
    float2 farSide = float2(max(c.x, boxSize.x - c.x), max(c.y, boxSize.y - c.y));

    float2 radius;

    if (g.Circle != 0)
    {
        float r = (g.SizeMode == 0) ? max(farSide.x, farSide.y) :
                  (g.SizeMode == 1) ? length(farSide) :
                  (g.SizeMode == 2) ? min(nearSide.x, nearSide.y) :
                  length(nearSide);

        radius = float2(r, r);
    }
    else
    {
        radius = (g.SizeMode == 0) ? farSide :
                 (g.SizeMode == 1) ? farSide * 1.41421356 :
                 (g.SizeMode == 2) ? nearSide :
                 nearSide * 1.41421356;
    }

    gradLength = radius.x;

    return length(d / max(radius, 0.0001));
}

float GradientStop(GradientData g, int i, float gradLength)
{
    if ((g.StopUnits & (1 << i)) != 0)
        return g.StopOffsets[i] / max(gradLength, 0.0001);

    return g.StopOffsets[i];
}

float4 EvaluateGradient(GradientData g, float2 texCoord, float2 boxSize)
{
    float gradLength;
    float t = GradientPosition(g, texCoord, boxSize, gradLength);

    int last = g.Count - 1;

    if (t <= GradientStop(g, 0, gradLength))
        return g.StopColors[0];

    if (t >= GradientStop(g, last, gradLength))
        return g.StopColors[last];

    float4 col = g.StopColors[last];

    [loop]
    for (int s = 0; s < last; s++)
    {
        float o1 = GradientStop(g, s + 1, gradLength);
        if (t <= o1)
        {
            float o0 = GradientStop(g, s, gradLength);
            float f = saturate((t - o0) / max(o1 - o0, 0.0001));

            float4 A = g.StopColors[s];
            float4 B = g.StopColors[s + 1];

            float a = lerp(A.a, B.a, f);
            float3 rgb = lerp(A.rgb * A.a, B.rgb * B.a, f);
            if (a > 0.0001)
                rgb /= a;

            col = float4(rgb, a);
            break;
        }
    }

    return col;
}

float InstanceClipCoverage(BoxInstanceData inst, PixelInput i)
{
    float flCoverage = 1.0;

    if (inst.ScissorIndex >= 0)
        flCoverage *= ScissorCoverage(inst.ScissorIndex, i.vPositionPanelSpace.xy / i.vPositionPanelSpace.w);

    if (inst.InverseScissorIndex >= 0)
        flCoverage *= ScissorCoverage(inst.InverseScissorIndex, i.vPositionPanelSpace.xy / i.vPositionPanelSpace.w);

    return flCoverage;
}

float4 RenderInstance(PixelInput i, out float flCoverage)
{
    BoxInstanceData inst = BoxInstances[i.iInstanceID];
    if ((inst.Flags & (1u << 14)) != 0)
    {
        TextInstanceData text = PainterTextInstances[inst.TextureIndex];
        float2 p = text.Rect.xy + i.vTexCoord.xy * text.Rect.zw;
        float4 color = i.vColor;
        if (text.TextureIndex < 0)
        {
            float4 gradient = EvaluateTextGradient(GradientBuffer[-text.TextureIndex - 1], text.BorderImageSlice, p);
            color.rgb = gradient.rgb;
            color.a *= saturate(gradient.a);
        }
        flCoverage = text.Mode == 0 ? 1.0 : TextCoverage(text, p, TextFootprint(p));
        color.a *= flCoverage;
        return color;
    }

    if (inst.GetMode() == 1) return RenderShadow(inst, i, false, flCoverage);
    if (inst.GetMode() == 2) return RenderShadow(inst, i, true, flCoverage);
    if (inst.GetMode() == 3) return RenderOutline(inst, i, flCoverage);

    float2 boxSize = inst.Rect.zw;
    float4 borderWidth = inst.BorderSize;

    float2 pos = (i.vTexCoord.xy - 0.5) * boxSize;
    float dOuter;
    float pathCoverage = -1.0;
    bool isPath = false;
    [branch] if (inst.ShapeIndex >= 0)
    {
        BorderShapeData shape = BorderShapeBuffer[inst.ShapeIndex];
        isPath = shape.Kind == UI_SHAPE_POLYGON_PATH || shape.Kind == UI_SHAPE_STROKE_PATH;
        if (shape.Kind == UI_SHAPE_STROKE_PATH)
        {
            float2 local = i.vTexCoord.xy * boxSize;
            pathCoverage = PathStrokeCoverage(shape, local + shape.Circle.xy, ddx(local), ddy(local));
            if (shape.PolygonCount > 0)
            {
                BorderShapeData mask = BorderShapeBuffer[shape.PolygonCount - 1];
                float2 p = local + shape.Circle.xy;
                float distance;
                if (mask.Kind == UI_SHAPE_CIRCLE)
                {
                    float radius = length(p - mask.Circle.xy);
                    distance = radius - mask.Circle.z;
                    if (mask.Circle.w > 0.0) distance = max(distance, mask.Circle.w - radius);
                }
                else if (mask.Kind >= UI_SHAPE_FIRST_ANALYTIC) distance = ShapeSdf(p, mask.Polygon01, mask.Polygon23, mask.Polygon45, mask.Polygon67, mask.PolygonCount, mask.Circle, mask.Kind);
                else distance = PathPolygonSdf(p, mask);
                pathCoverage = min(pathCoverage, SdfCoverage(distance * shape.Circle.w));
            }
            dOuter = -1.0;
        }
        else if (shape.Kind == UI_SHAPE_POLYGON_PATH)
            dOuter = PathPolygonSdf(i.vTexCoord.xy * boxSize, shape);
        else
            dOuter = ShapeSdf(i.vTexCoord.xy * boxSize, shape.Polygon01, shape.Polygon23, shape.Polygon45, shape.Polygon67, shape.PolygonCount, shape.Circle, shape.Kind);
    }
    else
    {
        dOuter = RoundedRectSdf(pos, boxSize * 0.5, inst.BorderRadius, inst.BorderRadiusV);
    }

    float4 col = i.vColor;

    float flMask = 1.0;

    if (inst.TextureIndex != 0)
    {
        float2 bgSize = inst.BackgroundRect.zw;
        float4 bgTint = inst.BackgroundTint;
        bgTint.a = saturate(bgTint.a);

        float2 vOffset = inst.BackgroundRect.xy / bgSize;
        float2 vUV = -vOffset + (i.vTexCoord.xy * (boxSize / bgSize));

        float4 vImage;
        int bgRepeat = inst.GetBackgroundRepeat();

        vUV = RotateTexCoord(vUV, inst.BackgroundAngle);
        if (inst.TextureIndex > 0)
        {
            Texture2D tex = GetTexture2D(inst.TextureIndex);
            vImage = tex.SampleBias(GetSamplerNonUniform(inst.SamplerIndex), vUV, -1.5);
        }
        else
        {
            float2 tileUV = vUV;
            if (bgRepeat == 0 || bgRepeat == 1) tileUV.x = frac(tileUV.x);
            if (bgRepeat == 0 || bgRepeat == 2) tileUV.y = frac(tileUV.y);

            GradientData gradient = GradientBuffer[-inst.TextureIndex - 1];
            vImage = EvaluateGradient(gradient, tileUV, bgSize);
        }

        if (bgRepeat != 0 && bgRepeat != 4)
        {
            if (bgRepeat != 1)
                if (vUV.x < 0 || vUV.x > 1) vImage = 0;
            if (bgRepeat != 2)
                if (vUV.y < 0 || vUV.y > 1) vImage = 0;
        }

        float imageCoverage = saturate(vImage.a);
        if (BlendMode == 3)
        {
            vImage.rgb *= bgTint.rgb;
            vImage *= bgTint.a;
        }
        else
        {
            vImage *= bgTint;
        }

        int bgBlend = inst.GetBackgroundBlend();
        if (bgBlend != 0)
        {
            float3 blended = bgBlend == 1 ? col.rgb * vImage.rgb : max(col.rgb, vImage.rgb);
            vImage.rgb = lerp(vImage.rgb, blended, col.a);
        }

        float overAlpha = vImage.a + col.a * (1 - vImage.a);
        col.rgb = lerp(col.rgb, vImage.rgb, overAlpha > 0 ? vImage.a / overAlpha : 0);
        col.a = overAlpha;

        if (inst.TextureIndex > 0)
            flMask = isPath ? imageCoverage : saturate(vImage.a);
    }

    float bgClip = BackgroundClipCoverage(inst, pos, boxSize, i.vTexCoord.xy);
    if (BlendMode == 3)
        col *= bgClip;
    else
        col.a *= bgClip;
    flMask *= bgClip;

    if (inst.GetBorderImageMode() > 0)
    {
        float4 biTint = inst.BorderImageTint;
        biTint.a = saturate(biTint.a);
        float4 vBoxBorder = AddImageBorder(i.vTexCoord.xy, boxSize, borderWidth, inst.BorderImageIndex,
            inst.BorderImageSamplerIndex, inst.GetBorderImageMode(), inst.GetBorderImageFill(), inst.BorderImageSlice) * biTint;
        col = AlphaBlend(vBoxBorder, col);
        flMask = max(flMask, saturate(vBoxBorder.a));
    }
    else if (inst.ShapeIndex < 0)
    {
        bool hasBorder = borderWidth.x != 0 || borderWidth.y != 0 || borderWidth.z != 0 || borderWidth.w != 0;
        if (hasBorder)
        {
            float dInner = InsetBoxSdf(pos, boxSize, inst.BorderRadius, inst.BorderRadiusV, borderWidth);

            float4 vBoxBorder = inst.GetBorderStyle() != 0 ? StyledBoxBorder(i.vTexCoord.xy * boxSize, inst)
                : BorderSideColor(i.vTexCoord.xy * boxSize, boxSize, borderWidth,
                inst.BorderColorL, inst.BorderColorT,
                inst.BorderColorR, inst.BorderColorB);
            vBoxBorder.a = saturate(vBoxBorder.a) * (1.0 - SdfCoverage(dInner));
            col = AlphaBlend(vBoxBorder, col);
            flMask = max(flMask, vBoxBorder.a);
        }
    }

    if (inst.ShapeIndex >= 0)
    {
        float width = max(max(borderWidth.x, borderWidth.y), max(borderWidth.z, borderWidth.w));
        if (width > 0)
        {
            float4 border = inst.GetBorderStyle() != 0
                ? StyledShapeBorder(i.vTexCoord.xy * boxSize, inst, BorderShapeBuffer[inst.ShapeIndex], dOuter) : inst.BorderColorL;
            border.a = saturate(border.a) * (1.0 - SdfCoverage(dOuter + width));
            col = AlphaBlend(border, col); flMask = max(flMask, border.a);
        }
    }

    float edge = pathCoverage >= 0.0 ? pathCoverage : SdfCoverage(dOuter);
    flCoverage = edge * flMask;

    if (BlendMode == 3)
        col *= edge;
    else
        col.a *= edge;

    return col;
}

[shader("fragment")]
float4 FragmentMain(PixelInput i) : SV_Target0
{
    BoxInstanceData inst = BoxInstances[i.iInstanceID];
    bool path = false;
    if (inst.ShapeIndex >= 0)
    {
        int kind = BorderShapeBuffer[inst.ShapeIndex].Kind;
        path = kind == UI_SHAPE_POLYGON_PATH || kind == UI_SHAPE_STROKE_PATH;
        if (path)
        {
            float2 local = i.vPathPosition.xy / (i.vPathPosition.z != 0.0 ? i.vPathPosition.z : 1.0);
            i.vTexCoord.xy = (local - inst.Rect.xy) / inst.Rect.zw;
            i.vPositionPanelSpace = mul(TransformBuffer[inst.TransformIndex].Mat, float4(local, 0, 1));
        }
    }

    float flCoverage;
    float4 col = RenderInstance(i, flCoverage);

    float flClip = InstanceClipCoverage(inst, i);
    if (path && i.vPathPosition.z <= 0.0) clip(-1);
    if (flClip <= 0.0)
        clip(-1);

    if (BlendMode == 3)
        col *= flClip;
    else
        col.a *= flClip;

    return col;
}
