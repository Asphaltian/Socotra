#pragma once

#include "UI.Common.slang"
#include "UI.RoundedRect.slang"

static const int TEXT_ALIASED = 1;
static const int TEXT_TILE_SIZE = 16;

uint GpuFontRootCode(float y1, float y2, float y3)
{
    uint i1 = asuint(y1) >> 31U;
    uint i2 = asuint(y2) >> 30U;
    uint i3 = asuint(y3) >> 29U;
    uint shift = (i2 & 2U) | (i1 & ~2U);
    shift = (i3 & 4U) | (shift & ~4U);
    return (0x2E74U >> shift) & 0x0101U;
}

float GaussCdf(float x)
{
    return 0.5 + 0.5 * GaussErf(float2(x * 0.70710678, 0)).x;
}

float2 GpuFontSolveHoriz(float2 p1, float2 p2, float2 p3)
{
    float2 a = p1 - p2 * 2.0 + p3;
    float2 b = p1 - p2;
    float d = sqrt(max(b.y * b.y - a.y * p1.y, 0.0));
    float ra = 1.0 / a.y;
    float t1 = (b.y - d) * ra;
    float t2 = (b.y + d) * ra;

    if (abs(a.y) < 1.0 / 65536.0)
        t1 = t2 = p1.y / (2.0 * b.y);

    return float2((a.x * t1 - b.x * 2.0) * t1 + p1.x, (a.x * t2 - b.x * 2.0) * t2 + p1.x);
}

void GpuFontRay(GpuFontGlyph glyph, float2 em, float pixelsPerEm, bool vertical, float sigma, out float cov, out float wgt, out int winding)
{
    cov = 0.0;
    wgt = 0.0;
    winding = 0;
    if (glyph.BandCount <= 0) return;

    float2 bmin = glyph.Bounds.xy;
    float2 bsize = max(glyph.Bounds.zw - bmin, 1e-6);

    float across = vertical ? em.x : em.y;
    float bandMin = vertical ? bmin.x : bmin.y;
    float bandSize = vertical ? bsize.x : bsize.y;

    int band = clamp(int((across - bandMin) / bandSize * glyph.BandCount), 0, glyph.BandCount - 1);
    int header = glyph.BandOffset + (vertical ? glyph.BandCount * 2 : 0) + band * 2;
    uint offset = GlyphBands[header] * 3;
    uint count = min(GlyphBands[header + 1], 4096u);
    float2 origin = vertical ? em.yx : em;

    float xcut = -max(0.5, sigma * 3.0) / pixelsPerEm;

    [loop]
    for (uint i = 0; i < count; i++)
    {
        uint c = offset + i * 3;
        float2 p1 = GlyphCurves[c], p2 = GlyphCurves[c + 1], p3 = GlyphCurves[c + 2];

        if (vertical) { p1 = p1.yx; p2 = p2.yx; p3 = p3.yx; }

        p1 -= origin; p2 -= origin; p3 -= origin;

        if (max(p1.x, max(p2.x, p3.x)) < xcut)
            break;

        uint code = GpuFontRootCode(p1.y, p2.y, p3.y);
        if (code == 0)
            continue;

        float2 r = GpuFontSolveHoriz(p1, p2, p3) * pixelsPerEm;

        float2 step = sigma > 0.0 ? float2(GaussCdf(r.x / sigma), GaussCdf(r.y / sigma)) : saturate(r + 0.5);

        if ((code & 1U) != 0U)
        {
            cov += step.x;
            wgt = max(wgt, saturate(1.0 - abs(r.x) * 2.0));
            winding += r.x > 0.0 ? 1 : 0;
        }

        if (code > 1U)
        {
            cov -= step.y;
            wgt = max(wgt, saturate(1.0 - abs(r.y) * 2.0));
            winding -= r.y > 0.0 ? 1 : 0;
        }
    }

    if (vertical)
    {
        cov = -cov;
        winding = -winding;
    }
}

float GpuFontCoverage(GpuFontGlyph glyph, float2 em, float pixelsPerEm)
{
    float xcov, xwgt, ycov, ywgt;
    int w;
    GpuFontRay(glyph, em, pixelsPerEm, false, 0.0, xcov, xwgt, w);
    GpuFontRay(glyph, em, pixelsPerEm, true, 0.0, ycov, ywgt, w);

    float weighted = abs(xcov * xwgt + ycov * ywgt) / max(xwgt + ywgt, 1.0 / 65536.0);
    float fallback = min(abs(xcov), abs(ycov));
    return saturate(max(weighted, fallback));
}

float GpuFontSignedDistance(GpuFontGlyph glyph, float2 em)
{
    const int segments = 8;
    float best = 1e30;

    int curveCount = min(glyph.CurveCount, 4096);

    [loop]
    for (int c = 0; c < curveCount; c++)
    {
        int i = (glyph.CurveStart + c) * 3;
        float2 p1 = GlyphCurves[i] - em;
        float2 p2 = GlyphCurves[i + 1] - em;
        float2 p3 = GlyphCurves[i + 2] - em;

        float2 prev = p1;
        [unroll]
        for (int s = 1; s <= segments; s++)
        {
            float t = s / (float)segments;
            float u = 1.0 - t;
            float2 next = p1 * (u * u) + p2 * (2.0 * u * t) + p3 * (t * t);

            float2 ab = next - prev;
            float k = saturate(-dot(prev, ab) / max(dot(ab, ab), 1e-12));
            float2 q = prev + ab * k;
            best = min(best, dot(q, q));

            prev = next;
        }
    }

    float d = sqrt(best);

    bool inside = false;
    if (all(em >= glyph.Bounds.xy) && all(em <= glyph.Bounds.zw))
    {
        float cov, wgt;
        int winding;
        GpuFontRay(glyph, em, 1.0, false, 0.0, cov, wgt, winding);
        inside = winding != 0;
    }

    return inside ? -d : d;
}

float TextFootprint(float2 p)
{
    return max(0.5 * (length(ddx(p)) + length(ddy(p))), 0.0001);
}

float TextBoxCoverage(float lo, float hi, float p, float ps)
{
    return saturate((min(hi, p + ps * 0.5) - max(lo, p - ps * 0.5)) / ps);
}

float TextBlurredBoxCoverage(float lo, float hi, float p, float sigma)
{
    return GaussCdf((p - lo) / sigma) - GaussCdf((p - hi) / sigma);
}

float TextBlurSigma(float sigma, float ps)
{
    return sqrt(sigma * sigma + 0.12 * ps * ps);
}

float TextLineCoverage(TextInstanceData inst, float2 p, float ps)
{
    float x0 = inst.BackgroundRect.x, x1 = inst.BackgroundRect.y, y0 = inst.BackgroundRect.z, y1 = inst.BackgroundRect.w;
    float y = inst.BorderRadius.x;
    float t = inst.BorderRadius.y;
    int style = int(inst.BorderRadius.w);
    float sigma = inst.BorderRadiusV.x;

    if (sigma > 0.0)
    {
        float s = TextBlurSigma(sigma, ps);
        return TextBlurredBoxCoverage(x0, x1, p.x, s) * TextBlurredBoxCoverage(y0, y1, p.y, s);
    }

    if (style == 1)
    {
        float k = round((p.x - x0) / (2.0 * t));
        float cx = x0 + k * 2.0 * t;
        if (cx < x0 - 0.001 || cx > x1 + 0.001) return 0.0;
        float d = length(p - float2(cx, y)) - t * 0.5;
        return saturate(0.5 - d / ps);
    }

    if (style == 4)
    {
        float u = p.x - x0;
        float f = y + sin(u * 0.25) * 1.25;
        float slope = cos(u * 0.25) * 0.3125;
        float d = abs(p.y - f) / sqrt(1.0 + slope * slope) - t * 0.5;
        return saturate(0.5 - d / ps) * TextBoxCoverage(x0 - t * 0.5, x1 + t * 0.5, p.x, ps);
    }

    float xcov = TextBoxCoverage(x0, x1, p.x, ps);
    float cov = xcov * TextBoxCoverage(y0, y1, p.y, ps);

    if (style == 3)
    {
        float off = inst.BorderRadius.z;
        cov = max(cov, xcov * TextBoxCoverage(y0 + off, y1 + off, p.y, ps));
    }
    else if (style == 2)
    {
        if (frac((p.x - x0 + t) / (6.0 * t)) >= 0.5) cov = 0.0;
    }

    return cov;
}

float TextGlyphShadow(GpuFontGlyph glyph, float2 em, float scale, float sigma, float ps)
{
    float sx = TextBlurSigma(sigma, ps);
    float step = max(ps, sigma * 0.5);
    int n = int(ceil(2.5 * sx / step));

    float y = em.y * scale;
    float base = floor(y / step) * step;
    float2 reach = glyph.Bounds.yw * scale;

    float sum = 0.0, wsum = 0.0;

    [loop]
    for (int k = -n; k <= n + 1; k++)
    {
        float row = base + k * step;
        float dy = row - y;
        float w = exp(-(dy * dy) / (2.0 * sx * sx));
        wsum += w;
        if (row < reach.x || row > reach.y)
            continue;

        float cov, wgt;
        int winding;
        GpuFontRay(glyph, em + float2(0, dy / scale), scale, false, sx, cov, wgt, winding);
        sum += saturate(abs(cov)) * w;
    }

    return sum / wsum;
}

float GlyphCoverage(GpuFontGlyph glyph, float2 em, float scale, float dilate, float sigma, float ps, bool aliased)
{
    if (sigma > 0.05)
        return TextGlyphShadow(glyph, em, scale, sigma, ps);

    float cov;
    int n = ps > 1.05 ? min(int(ceil(ps)), 4) : 1;
    if (dilate > 0.0)
    {
        float d = GpuFontSignedDistance(glyph, em) * scale - dilate;
        cov = saturate(0.5 - d / ps);
    }
    else if (any(abs(em - (glyph.Bounds.xy + glyph.Bounds.zw) * 0.5) > (glyph.Bounds.zw - glyph.Bounds.xy) * 0.5 + 0.51 * ps / scale))
    {
        cov = 0.0;
    }
    else if (n > 1)
    {
        float sub = ps / n;
        cov = 0.0;

        [loop]
        for (int y = 0; y < n; y++)
        {
            [loop]
            for (int x = 0; x < n; x++)
                cov += GpuFontCoverage(glyph, em + (float2(x + 0.5, y + 0.5) * sub - ps * 0.5) / scale, scale / sub);
        }

        cov /= n * n;
    }
    else
    {
        cov = GpuFontCoverage(glyph, em, scale / ps);
    }

    return aliased ? (cov >= 0.5 ? 1.0 : 0.0) : cov;
}

float TextCoverage(TextInstanceData inst, float2 p, float ps)
{
    if (inst.Mode == 5)
        return TextLineCoverage(inst, p, ps);

    float2 origin = inst.BackgroundRect.xy;
    float size = inst.BackgroundRect.z;
    float dilate = inst.BackgroundRect.w;
    float sigma = inst.BorderRadiusV.x;

    return GlyphCoverage(GlyphTable[inst.Flags], (p - origin) / size, size, dilate, sigma, ps, (inst.BorderImageMode & TEXT_ALIASED) != 0);
}

float4 EvaluateTextGradient(GradientData g, float4 rect, float2 p)
{
    float w = rect.z, h = rect.w;
    p -= rect.xy;

    float t;
    if (g.Type == 0)
    {
        float2 c = float2(w, h) * 0.5;
        float angle = radians(180.0 + g.Angle);
        float2x2 rot = float2x2(cos(angle), -sin(angle), sin(angle), cos(angle));
        float2 s = c + mul(rot, float2(w * 0.5, 0) - c);
        float2 e = c + mul(rot, float2(w * 0.5, h) - c);

        float2 span = abs(e - s);
        span = float2(span.x == 0.0 ? 1.0 : span.x, span.y == 0.0 ? 1.0 : span.y);
        float2 sc = float2(w, h) / span;

        float2 q = c + (p - c) / sc;
        t = dot(q - s, e - s) / max(dot(e - s, e - s), 1e-6);
    }
    else
    {
        float2 c;
        c.x = (g.CenterUnits & 1) != 0 ? g.Center.x * w : g.Center.x;
        c.y = (g.CenterUnits & 2) != 0 ? g.Center.y * h : g.Center.y;

        float radius = max(w, h);
        float2 nearSide = float2(min(c.x, w - c.x), min(c.y, h - c.y));
        float2 farSide = float2(max(c.x, w - c.x), max(c.y, h - c.y));

        float2 sc;
        if (g.SizeMode == 1 || g.SizeMode == 3)
        {
            float2 corners[4] = { c, float2(w, 0) - c, float2(w, h) - c, float2(0, h) - c };
            float2 pick = corners[0];
            for (int i = 1; i < 4; i++)
            {
                bool better = g.SizeMode == 1 ? length(corners[i]) > length(pick) : length(corners[i]) < length(pick);
                if (better) pick = corners[i];
            }
            sc = pick / (float2(w, h) * 0.71);
        }
        else if (g.SizeMode == 2)
            sc = nearSide / float2(w, h);
        else
            sc = farSide / float2(w, h);

        if (g.SizeMode != 4)
            sc *= float2(w, h) / radius;

        float2 q = c + (p - c) / sc;
        t = length(q - c) / radius;
    }

    t = saturate(t);

    int last = g.Count - 1;
    if (t <= g.StopOffsets[0]) return g.StopColors[0];
    if (t >= g.StopOffsets[last]) return g.StopColors[last];

    float4 col = g.StopColors[last];
    [loop]
    for (int s = 0; s < last; s++)
    {
        float o1 = g.StopOffsets[s + 1];
        if (t <= o1)
        {
            float o0 = g.StopOffsets[s];
            col = lerp(g.StopColors[s], g.StopColors[s + 1], saturate((t - o0) / max(o1 - o0, 1e-6)));
            break;
        }
    }

    return col;
}

float4 TextComposite(float2 p, float ps, int instanceOffset, int tileOffset, int tilesX, int2 size, float3 baseColor)
{
    if (any(size <= 0)) return float4(baseColor, 0.0);

    int2 tile = clamp(int2(p) / TEXT_TILE_SIZE, 0, (size + TEXT_TILE_SIZE - 1) / TEXT_TILE_SIZE - 1);
    int tileIndex = tileOffset + tile.y * tilesX + tile.x;
    uint first = TextTiles[tileIndex];
    uint last = min(TextTiles[tileIndex + 1], first + 65536u);

    float4 acc = 0.0;

    [loop]
    for (uint i = first; i < last; i++)
    {
        TextInstanceData inst = PainterTextInstances[instanceOffset + TextTiles[i]];
        if (any(p + ps * 0.5 < inst.Rect.xy) || any(p - ps * 0.5 > inst.Rect.xy + inst.Rect.zw))
            continue;

        float4 col = inst.Color;
        float cov = inst.Mode == 0
            ? TextBoxCoverage(inst.Rect.x, inst.Rect.x + inst.Rect.z, p.x, ps) * TextBoxCoverage(inst.Rect.y, inst.Rect.y + inst.Rect.w, p.y, ps)
            : TextCoverage(inst, p, ps);

        float a = saturate(col.a) * cov;
        if (a <= 0.0) continue;

        acc.rgb = col.rgb * a + acc.rgb * (1.0 - a);
        acc.a = a + acc.a * (1.0 - a);
    }

    return acc.a > 0.0 ? float4(acc.rgb / acc.a, acc.a) : float4(baseColor, 0.0);
}
