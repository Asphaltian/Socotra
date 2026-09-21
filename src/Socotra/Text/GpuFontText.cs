using System.Runtime.InteropServices;
using SkiaSharp;
using Topten.RichTextKit;

namespace Socotra;

internal static class GpuFontText
{
    internal const int TileSize = 16;
    private const int ModeGlyph = 4;
    private const int ModeLine = 5;
    private const int FlagAliased = 1;
    private const int FlagGradient = 2;

    internal static bool WantsGradient(in TextInstance instance) => instance.Mode >= ModeGlyph && (instance.BorderImageMode & FlagGradient) != 0;

    public static void Build(Topten.RichTextKit.TextBlock block, Vector2 origin, in Options options, List<TextInstance> instances)
    {
        var builder = new Builder
        {
            Instances = instances,
            Options = options,
            Origin = origin,
            GradientRect = new Vector4(origin.X + block.MeasuredPadding.Left, origin.Y, block.MeasuredWidth, block.MeasuredHeight),
            AliasedFlag = options.Aliased ? FlagAliased : 0,
            GradientFlag = options.HasGradient ? FlagGradient : 0,
        };

        builder.Build(block);
    }

    public static Texture Render(Topten.RichTextKit.TextBlock block, Vector2 origin, int width, int height, in Options options, Texture? reuse)
    {
        var instances = new List<TextInstance>();
        Build(block, origin, options, instances);

        var tilesX = (width + TileSize - 1) / TileSize;
        var raster = new Raster([.. instances], BinTiles(CollectionsMarshal.AsSpan(instances), tilesX, (height + TileSize - 1) / TileSize), tilesX, BaseColor(block));
        if (reuse is not null && reuse.Width == width && reuse.Height == height)
        {
            reuse.SetRaster(raster);
            return reuse;
        }

        return Texture.FromRaster(width, height, raster);
    }

    private static Vector3 BaseColor(Topten.RichTextKit.TextBlock block)
    {
        foreach (var line in block.Lines)
        {
            foreach (var run in line.Runs)
            {
                return new Vector3(run.Style.TextColor.Red, run.Style.TextColor.Green, run.Style.TextColor.Blue);
            }
        }

        return Vector3.Zero;
    }

    private static uint[] BinTiles(ReadOnlySpan<TextInstance> instances, int tilesX, int tilesY)
    {
        int tileCount = tilesX * tilesY;
        var counts = new int[tileCount];

        foreach (var instance in instances)
        {
            GetTileRange(instance.Rect, tilesX, tilesY, out var x0, out var y0, out var x1, out var y1);
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    counts[(y * tilesX) + x]++;
                }
            }
        }

        var tiles = new uint[tileCount + 1 + counts.Sum()];
        var running = (uint)(tileCount + 1);
        for (int t = 0; t < tileCount; t++)
        {
            tiles[t] = running;
            running += (uint)counts[t];
        }

        tiles[tileCount] = running;
        Array.Clear(counts);

        for (int i = 0; i < instances.Length; i++)
        {
            GetTileRange(instances[i].Rect, tilesX, tilesY, out var x0, out var y0, out var x1, out var y1);
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    int t = (y * tilesX) + x;
                    tiles[tiles[t] + counts[t]] = (uint)i;
                    counts[t]++;
                }
            }
        }

        return tiles;
    }

    private static void GetTileRange(Vector4 rect, int tilesX, int tilesY, out int x0, out int y0, out int x1, out int y1)
    {
        x0 = Math.Clamp((int)MathF.Floor(rect.X / TileSize), 0, tilesX - 1);
        y0 = Math.Clamp((int)MathF.Floor(rect.Y / TileSize), 0, tilesY - 1);
        x1 = Math.Clamp((int)MathF.Floor((rect.X + rect.Z) / TileSize), 0, tilesX - 1);
        y1 = Math.Clamp((int)MathF.Floor((rect.Y + rect.W) / TileSize), 0, tilesY - 1);
    }

    internal sealed record Raster(TextInstance[] Instances, uint[] Tiles, int TilesX, Vector3 BaseColor);

    internal struct Options
    {
        public bool Aliased;
        public int SelectionStart;
        public int SelectionEnd;
        public Color SelectionColor;
        public bool HasGradient;
        public float Opacity;

        public static Options Default => new() { SelectionStart = -1, SelectionEnd = -1, Opacity = 1 };
    }

    private ref struct Builder
    {
        public List<TextInstance> Instances;
        public Options Options;
        public Vector2 Origin;
        public Vector4 GradientRect;
        public int AliasedFlag;
        public int GradientFlag;

        public readonly void Build(Topten.RichTextKit.TextBlock block)
        {
            foreach (var line in block.Lines)
            {
                foreach (var run in line.Runs)
                {
                    if (run.RunKind == FontRunKind.TrailingWhitespace)
                    {
                        continue;
                    }

                    var style = run.Style;

                    if (style.BackgroundColor.Alpha > 0 && run.RunKind == FontRunKind.Normal)
                    {
                        AddRect(Origin + new Vector2(run.XCoord, line.YCoord), Origin + new Vector2(run.XCoord + run.Width, line.YCoord + line.Height), ToColor(style.BackgroundColor));
                    }

                    if (run.RunKind == FontRunKind.Tabs || run.Glyphs.Length == 0 || style.TextEffects is null)
                    {
                        continue;
                    }

                    foreach (var effect in style.TextEffects)
                    {
                        AddEffectPass(run, line, Origin + new Vector2(effect.Offset.X, effect.Offset.Y), effect.Width * 0.5f, effect.BlurSize, ToColor(effect.Color));
                    }
                }
            }

            bool selection = Options.SelectionStart >= 0 && Options.SelectionEnd >= 0 && Options.SelectionStart != Options.SelectionEnd;
            int selectionStart = Math.Min(Options.SelectionStart, Options.SelectionEnd);
            int selectionEnd = Math.Max(Options.SelectionStart, Options.SelectionEnd);

            foreach (var line in block.Lines)
            {
                foreach (var run in line.Runs)
                {
                    if (selection && run.RunKind != FontRunKind.Ellipsis)
                    {
                        AddSelection(run, line, selectionStart, selectionEnd);
                    }

                    if (run.RunKind == FontRunKind.Tabs || run.RunKind == FontRunKind.TrailingWhitespace)
                    {
                        continue;
                    }

                    AddGlyphs(run, Origin, 0, 0, ToColor(run.Style.TextColor), AliasedFlag | GradientFlag, layerColors: true);
                    AddDecorations(run, line, Origin, 0, ToColor(run.Style.UnderlineColor ?? run.Style.TextColor), GradientFlag);
                }
            }
        }

        private static Color ToColor(SKColorF color) => new(color.Red, color.Green, color.Blue, color.Alpha);

        private readonly void AddEffectPass(FontRun run, TextLine line, Vector2 origin, float dilate, float sigma, Color color)
        {
            AddGlyphs(run, origin, dilate, sigma, color, AliasedFlag);
            AddDecorations(run, line, origin, sigma, color, 0);
        }

        private readonly TextInstance Instance(Vector2 min, Vector2 max, Color color, int mode, int flags) => new()
        {
            Rect = new Vector4(min.X, min.Y, max.X - min.X, max.Y - min.Y),
            Color = color.WithAlphaMultiplied(Options.Opacity),
            Mode = mode,
            BorderImageMode = flags,
            BorderImageSlice = GradientRect,
            InverseScissorIndex = -1,
            ShapeIndex = -1,
        };

        private readonly void AddGlyphs(FontRun run, Vector2 origin, float dilate, float sigma, Color color, int flags, bool layerColors = false)
        {
            float fontSize = run.Style.FontSize;
            var glyphs = run.Glyphs.AsSpan();
            var positions = run.GlyphPositions.AsSpan();

            for (int i = 0; i < glyphs.Length; i++)
            {
                var position = origin + new Vector2(positions[i].X, positions[i].Y);
                var layers = GpuFontGlyphCache.GetLayers(run.Typeface, glyphs[i]);

                if (layers is null)
                {
                    AddGlyph(GpuFontGlyphCache.Get(run.Typeface, glyphs[i]), position, fontSize, dilate, sigma, color, flags);
                    continue;
                }

                foreach (var (layer, layerColor) in layers)
                {
                    var glyphColor = layerColors && layerColor is { } palette ? palette.WithAlphaMultiplied(color.A) : color;
                    AddGlyph(GpuFontGlyphCache.Get(run.Typeface, layer), position, fontSize, dilate, sigma, glyphColor, flags & ~FlagGradient);
                }
            }
        }

        private readonly void AddGlyph(in GpuFontGlyphCache.Glyph glyph, Vector2 position, float fontSize, float dilate, float sigma, Color color, int flags)
        {
            if (glyph.IsEmpty)
            {
                return;
            }

            float grow = dilate + (sigma * 3);
            var min = position + (new Vector2(glyph.Bounds.X, glyph.Bounds.Y) * fontSize) - new Vector2(grow);
            var max = position + (new Vector2(glyph.Bounds.Z, glyph.Bounds.W) * fontSize) + new Vector2(grow);

            var instance = Instance(min, max, color, ModeGlyph, flags);
            instance.BackgroundRect = new Vector4(position.X, position.Y, fontSize, dilate);
            instance.BorderRadiusV = new Vector4(sigma, 0, 0, 0);
            instance.Flags = glyph.Index;
            Instances.Add(instance);
        }

        private readonly void AddDecorations(FontRun run, TextLine line, Vector2 origin, float sigma, Color color, int flags)
        {
            var style = run.Style;
            if (run.RunKind != FontRunKind.Normal)
            {
                return;
            }

            if (style.Underline == UnderlineStyle.None && style.StrikeThrough == StrikeThroughStyle.None)
            {
                return;
            }

            using var font = new SKFont(run.Typeface, style.FontSize);
            var metrics = font.Metrics;
            float x0 = run.XCoord, x1 = run.XCoord + run.Width;

            var underlineWidth = style.StrokeThickness ?? metrics.UnderlineThickness ?? 0;
            if (underlineWidth > 0 && style.Underline != UnderlineStyle.None)
            {
                float thickness = MathF.Max(underlineWidth, 1);
                float underlineY = line.YCoord + line.BaseLine + (metrics.UnderlinePosition ?? 0);
                bool hasUnderline = false;

                if ((style.Underline & UnderlineStyle.Gapped) != 0)
                {
                    AddGappedLine(run, x0, x1, underlineY + style.UnderlineOffset, thickness, style, origin, sigma, color, flags, false);
                    hasUnderline = true;
                }

                if ((style.Underline & UnderlineStyle.Overline) != 0)
                {
                    AddGappedLine(run, x0, x1, line.YCoord + style.OverlineOffset, thickness, style, origin, sigma, color, flags, true);
                    hasUnderline = true;
                }

                if (!hasUnderline || (style.Underline & UnderlineStyle.Solid) != 0)
                {
                    AddLine(x0, x1, underlineY + style.UnderlineOffset, thickness, style.UnderlineStrokeType, false, origin, sigma, color, flags);
                }
            }

            var strikeWidth = style.StrokeThickness ?? metrics.StrikeoutThickness ?? 0;
            if (strikeWidth > 0 && style.StrikeThrough != StrikeThroughStyle.None)
            {
                float thickness = MathF.Max(strikeWidth, 1);
                float y = line.YCoord + line.BaseLine + (metrics.StrikeoutPosition ?? 0) + style.StrikeThroughOffset;
                AddLine(x0, x1, y, thickness, style.UnderlineStrokeType, false, origin, sigma, color, flags);
            }
        }

        private readonly void AddGappedLine(FontRun run, float x0, float x1, float y, float thickness, IStyle style, Vector2 origin, float sigma, Color color, int flags, bool overline)
        {
            float x = x0;

            if (style.StrokeInkSkip)
            {
                float fontSize = style.FontSize;
                var glyphs = run.Glyphs.AsSpan();
                var positions = run.GlyphPositions.AsSpan();
                var spans = new List<Vector2>();

                for (int i = 0; i < glyphs.Length; i++)
                {
                    var glyph = GpuFontGlyphCache.Get(run.Typeface, glyphs[i]);
                    if (glyph.IsEmpty)
                    {
                        continue;
                    }

                    float baseline = positions[i].Y;
                    int start = spans.Count;
                    GpuFontGlyphCache.Intercepts(glyph, (y - (thickness / 2) - baseline) / fontSize, (y + thickness - baseline) / fontSize, spans);
                    for (int s = start; s < spans.Count; s++)
                    {
                        spans[s] = (spans[s] * fontSize) + new Vector2(positions[i].X);
                    }
                }

                spans.Sort((a, b) => a.X.CompareTo(b.X));

                foreach (var span in spans)
                {
                    float before = span.X - thickness;
                    if (x < before)
                    {
                        AddLine(x, before, y, thickness, style.UnderlineStrokeType, overline, origin, sigma, color, flags);
                    }

                    x = MathF.Max(x, span.Y + thickness);
                }
            }

            if (x < x1)
            {
                AddLine(x, x1, y, thickness, style.UnderlineStrokeType, overline, origin, sigma, color, flags);
            }
        }

        private readonly void AddLine(float x0, float x1, float y, float thickness, UnderlineType type, bool overline, Vector2 origin, float sigma, Color color, int flags)
        {
            x0 += origin.X;
            x1 += origin.X;
            y += origin.Y;

            float y0 = MathF.Round(y - (thickness / 2));
            float y1 = MathF.Max(MathF.Round(y + (thickness / 2)), y0 + 1);

            float doubleOffset = type == UnderlineType.Double ? (overline ? -2 * thickness : 2 * thickness) : 0;
            float grow = thickness + (sigma * 3) + 1;
            var min = new Vector2(x0 - grow, MathF.Min(y0, y0 + doubleOffset) - grow);
            var max = new Vector2(x1 + grow, MathF.Max(y1, y1 + doubleOffset) + grow);

            var instance = Instance(min, max, color, ModeLine, flags);
            instance.BackgroundRect = new Vector4(x0, x1, y0, y1);
            instance.BorderRadius = new Vector4(y, thickness, doubleOffset, (int)type);
            instance.BorderRadiusV = new Vector4(sigma, 0, 0, 0);
            Instances.Add(instance);
        }

        private readonly void AddRect(Vector2 min, Vector2 max, Color color)
        {
            min = new Vector2(MathF.Round(min.X), MathF.Round(min.Y));
            max = new Vector2(MathF.Round(max.X), MathF.Round(max.Y));
            if (max.X <= min.X || max.Y <= min.Y)
            {
                return;
            }

            Instances.Add(Instance(min, max, color, 0, 0));
        }

        private readonly void AddSelection(FontRun run, TextLine line, int selectionStart, int selectionEnd)
        {
            bool ltr = run.Direction == TextDirection.LTR;

            float startX;
            if (selectionStart < run.Start)
            {
                startX = ltr ? 0 : run.Width;
            }
            else if (selectionStart >= run.End)
            {
                startX = ltr ? run.Width : 0;
            }
            else
            {
                startX = run.RelativeCodePointXCoords[selectionStart - run.Start];
            }

            float endX;
            if (selectionEnd < run.Start)
            {
                endX = ltr ? 0 : run.Width;
            }
            else if (selectionEnd >= run.End)
            {
                endX = ltr ? run.Width : 0;
            }
            else
            {
                endX = run.RelativeCodePointXCoords[selectionEnd - run.Start];
            }

            if (startX == endX)
            {
                return;
            }

            var a = Origin + new Vector2(run.XCoord + startX, line.YCoord);
            var b = Origin + new Vector2(run.XCoord + endX, line.YCoord + line.Height);
            AddRect(Vector2.Min(a, b), Vector2.Max(a, b), Options.SelectionColor);
        }
    }
}
