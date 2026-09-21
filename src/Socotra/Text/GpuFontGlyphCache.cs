using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SkiaSharp;

namespace Socotra;

internal static class GpuFontGlyphCache
{
    private const int MaxBands = 128;
    private const float BandDensity = 2f;

    private static readonly Lock Gate = new();
    private static readonly ConditionalWeakTable<SKTypeface, Dictionary<ushort, Glyph>> Glyphs = [];
    private static readonly ConditionalWeakTable<SKTypeface, ColorTable> ColorTables = [];
    private static readonly List<Vector2> Curves = [];
    private static readonly List<uint> Bands = [];
    private static readonly List<Glyph> Table = [];
    private static readonly List<float> Crossings = [];
    private static readonly IComparer<Vector2> ByX = Comparer<Vector2>.Create((a, b) => a.X.CompareTo(b.X));

    internal delegate void DataReader(ReadOnlySpan<Vector2> curves, ReadOnlySpan<uint> bands, ReadOnlySpan<Glyph> table);

    internal static void Read(DataReader read)
    {
        lock (Gate)
        {
            read(CollectionsMarshal.AsSpan(Curves), CollectionsMarshal.AsSpan(Bands), CollectionsMarshal.AsSpan(Table));
        }
    }

    internal static (ushort Glyph, Color? Color)[]? GetLayers(SKTypeface typeface, ushort glyphId)
    {
        var table = ColorTables.GetValue(typeface, ColorTable.Load);
        if (table.IsEmpty)
        {
            return null;
        }

        lock (table)
        {
            return table.Get(glyphId);
        }
    }

    internal static Glyph Get(SKTypeface typeface, ushort glyphId)
    {
        lock (Gate)
        {
            var glyphs = Glyphs.GetOrCreateValue(typeface);
            if (glyphs.TryGetValue(glyphId, out var glyph))
            {
                return glyph;
            }

            glyph = Encode(typeface, glyphId);
            glyphs[glyphId] = glyph;
            return glyph;
        }
    }

    internal static void Intercepts(in Glyph glyph, float top, float bottom, List<Vector2> spans)
    {
        const int segments = 8;
        int start = spans.Count;

        lock (Gate)
        {
            for (int edge = 0; edge < 2; edge++)
            {
                float y = edge == 0 ? top : bottom;
                Crossings.Clear();

                for (int c = 0; c < glyph.CurveCount; c++)
                {
                    int i = (glyph.CurveStart + c) * 3;
                    var p1 = Curves[i];
                    var p2 = Curves[i + 1];
                    var p3 = Curves[i + 2];

                    var prev = p1;
                    for (int k = 1; k <= segments; k++)
                    {
                        float t = k / (float)segments;
                        float u = 1 - t;
                        var next = (p1 * (u * u)) + (p2 * (2 * u * t)) + (p3 * (t * t));

                        if ((prev.Y <= y) != (next.Y <= y))
                        {
                            Crossings.Add(prev.X + ((next.X - prev.X) * (y - prev.Y) / (next.Y - prev.Y)));
                        }

                        prev = next;
                    }
                }

                Crossings.Sort();
                for (int i = 0; i + 1 < Crossings.Count; i += 2)
                {
                    spans.Add(new Vector2(Crossings[i], Crossings[i + 1]));
                }
            }
        }

        if (spans.Count == start)
        {
            return;
        }

        spans.Sort(start, spans.Count - start, ByX);

        int w = start;
        for (int r = start + 1; r < spans.Count; r++)
        {
            if (spans[r].X <= spans[w].Y)
            {
                spans[w] = new Vector2(spans[w].X, MathF.Max(spans[w].Y, spans[r].Y));
            }
            else
            {
                spans[++w] = spans[r];
            }
        }

        spans.RemoveRange(w + 1, spans.Count - w - 1);
    }

    private static Glyph Encode(SKTypeface typeface, ushort glyphId)
    {
        var curves = new List<Vector2>();
        var upem = typeface.UnitsPerEm;
        if (upem <= 0)
        {
            upem = 1000;
        }

        using (var font = new SKFont(typeface, upem) { Hinting = SKFontHinting.None })
        using (var path = font.GetGlyphPath(glyphId))
        {
            if (path is not null && !path.IsEmpty)
            {
                ExtractCurves(path, 1.0f / upem, curves);
            }
        }

        int curveCount = curves.Count / 3;
        if (curveCount == 0)
        {
            return default;
        }

        var lo = new Vector2[curveCount];
        var hi = new Vector2[curveCount];
        var min = new Vector2(float.MaxValue);
        var max = new Vector2(float.MinValue);
        var extent = Vector2.Zero;

        for (int c = 0; c < curveCount; c++)
        {
            lo[c] = Vector2.Min(curves[c * 3], Vector2.Min(curves[(c * 3) + 1], curves[(c * 3) + 2]));
            hi[c] = Vector2.Max(curves[c * 3], Vector2.Max(curves[(c * 3) + 1], curves[(c * 3) + 2]));
            min = Vector2.Min(min, lo[c]);
            max = Vector2.Max(max, hi[c]);
            extent += hi[c] - lo[c];
        }

        var size = Vector2.Max(max - min, new Vector2(1e-6f));

        float crossings = MathF.Max(extent.X / size.X, extent.Y / size.Y);
        int bandCount = Math.Clamp((int)MathF.Ceiling(curveCount / crossings * BandDensity), 1, MaxBands);
        int Band(float t) => Math.Clamp((int)(t * bandCount), 0, bandCount - 1);

        var lists = new List<int>[bandCount * 2];
        for (int i = 0; i < lists.Length; i++)
        {
            lists[i] = [];
        }

        for (int c = 0; c < curveCount; c++)
        {
            var a = (lo[c] - min) / size;
            var b = (hi[c] - min) / size;
            for (int i = Band(a.Y); i <= Band(b.Y); i++)
            {
                lists[i].Add(c);
            }

            for (int i = Band(a.X); i <= Band(b.X); i++)
            {
                lists[bandCount + i].Add(c);
            }
        }

        int curveStart = Curves.Count / 3;
        Curves.AddRange(curves);
        int bandOffset = Bands.Count;

        for (int i = 0; i < lists.Length; i++)
        {
            bool vertical = i >= bandCount;
            float Far(int c) => vertical ? hi[c].Y : hi[c].X;

            lists[i].Sort((a, b) => Far(b).CompareTo(Far(a)));
            Bands.Add((uint)(Curves.Count / 3));
            Bands.Add((uint)lists[i].Count);
            foreach (var c in lists[i])
            {
                Curves.AddRange(CollectionsMarshal.AsSpan(curves).Slice(c * 3, 3));
            }
        }

        var glyph = new Glyph
        {
            Bounds = new Vector4(min.X, min.Y, max.X, max.Y),
            BandOffset = bandOffset,
            BandCount = bandCount,
            CurveStart = curveStart,
            CurveCount = curveCount,
            Index = Table.Count,
        };
        Table.Add(glyph);
        return glyph;
    }

    private static void ExtractCurves(SKPath path, float scale, List<Vector2> curves)
    {
        using var iterator = path.CreateIterator(false);
        var points = new SKPoint[4];
        Vector2 current = default, start = default;

        while (true)
        {
            var verb = iterator.Next(points);
            if (verb == SKPathVerb.Done)
            {
                break;
            }

            switch (verb)
            {
                case SKPathVerb.Move:
                    current = start = Point(points[0], scale);
                    break;

                case SKPathVerb.Line:
                    AddLine(curves, current, current = Point(points[1], scale));
                    break;

                case SKPathVerb.Quad:
                case SKPathVerb.Conic:
                    AddQuad(curves, current, Point(points[1], scale), current = Point(points[2], scale));
                    break;

                case SKPathVerb.Cubic:
                    AddCubic(curves, current, Point(points[1], scale), Point(points[2], scale), current = Point(points[3], scale), 0);
                    break;

                case SKPathVerb.Close:
                    AddLine(curves, current, start);
                    current = start;
                    break;
            }
        }
    }

    private static Vector2 Point(SKPoint point, float scale) => new(point.X * scale, point.Y * scale);

    private static bool AlmostEqual(Vector2 a, Vector2 b) => MathF.Abs(a.X - b.X) <= 1e-7f && MathF.Abs(a.Y - b.Y) <= 1e-7f;

    private static void AddLine(List<Vector2> curves, Vector2 a, Vector2 b)
    {
        if (AlmostEqual(a, b))
        {
            return;
        }

        AddQuad(curves, a, (a + b) * 0.5f, b);
    }

    private static void AddQuad(List<Vector2> curves, Vector2 a, Vector2 control, Vector2 b)
    {
        if (AlmostEqual(a, b) && AlmostEqual(a, control))
        {
            return;
        }

        curves.Add(a);
        curves.Add(control);
        curves.Add(b);
    }

    private static void AddCubic(List<Vector2> curves, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, int depth)
    {
        var error = (p0 - (p1 * 3) + (p2 * 3) - p3).Length() * 0.0481f;
        if (error < 0.002f || depth >= 4)
        {
            AddQuad(curves, p0, ((p1 * 3) + (p2 * 3) - p0 - p3) * 0.25f, p3);
            return;
        }

        var p01 = (p0 + p1) * 0.5f;
        var p12 = (p1 + p2) * 0.5f;
        var p23 = (p2 + p3) * 0.5f;
        var p012 = (p01 + p12) * 0.5f;
        var p123 = (p12 + p23) * 0.5f;
        var mid = (p012 + p123) * 0.5f;

        AddCubic(curves, p0, p01, p012, mid, depth + 1);
        AddCubic(curves, mid, p123, p23, p3, depth + 1);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Glyph
    {
        public Vector4 Bounds;
        public int BandOffset;
        public int BandCount;
        public int CurveStart;
        public int CurveCount;
        public int Index;

        public readonly bool IsEmpty => CurveCount == 0;
    }

    private sealed class ColorTable
    {
        private readonly Dictionary<ushort, (ushort, Color?)[]?> _layers = [];
        private byte[] _colr = [];
        private byte[] _cpal = [];
        private int _baseCount;
        private int _baseOffset;
        private int _layerOffset;
        private int _colorOffset;

        public bool IsEmpty => _baseCount == 0;

        public static ColorTable Load(SKTypeface typeface)
        {
            var table = new ColorTable();
            if ((!typeface.TryGetTableData(0x434F4C52, out var colr) && !typeface.TryGetTableData(0x434F4C58, out colr)) || !typeface.TryGetTableData(0x4350414C, out var cpal) || colr.Length < 14 || cpal.Length < 14)
            {
                return table;
            }

            table._colr = colr;
            table._cpal = cpal;
            table._baseCount = U16(colr, 2);
            table._baseOffset = U32(colr, 4);
            table._layerOffset = U32(colr, 8);
            table._colorOffset = U32(cpal, 8) + (U16(cpal, 12) * 4);
            return table;
        }

        public (ushort, Color?)[]? Get(ushort glyphId)
        {
            if (_baseCount == 0)
            {
                return null;
            }

            if (_layers.TryGetValue(glyphId, out var layers))
            {
                return layers;
            }

            int lo = 0, hi = _baseCount - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) / 2;
                int record = _baseOffset + (mid * 6);
                int gid = U16(_colr, record);
                if (gid < glyphId)
                {
                    lo = mid + 1;
                }
                else if (gid > glyphId)
                {
                    hi = mid - 1;
                }
                else
                {
                    int first = U16(_colr, record + 2), count = U16(_colr, record + 4);
                    layers = new (ushort, Color?)[count];
                    for (int i = 0; i < count; i++)
                    {
                        int layer = _layerOffset + ((first + i) * 4);
                        int palette = U16(_colr, layer + 2);
                        Color? color = null;
                        if (palette != 0xFFFF)
                        {
                            int c = _colorOffset + (palette * 4);
                            color = new Color(_cpal[c + 2] / 255f, _cpal[c + 1] / 255f, _cpal[c] / 255f, _cpal[c + 3] / 255f);
                        }

                        layers[i] = (U16(_colr, layer), color);
                    }

                    break;
                }
            }

            _layers[glyphId] = layers;
            return layers;
        }

        private static ushort U16(byte[] bytes, int offset) => (ushort)((bytes[offset] << 8) | bytes[offset + 1]);

        private static int U32(byte[] bytes, int offset) => (bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3];
    }
}
