using System.Web;
using SkiaSharp;
using Svg.Skia;

namespace Socotra;

/// <summary>
/// A picture you can show in an <see cref="Image"/> or use as a background, mask or border image.
/// Load one from a file, draw one from SVG markup, or make one from your own RGBA pixels.
/// </summary>
/// <example><code>
/// // Show a picture that ships next to your program
/// image.Texture = Texture.FromFile("assets/ui/logo.png");
///
/// // Or build a 2x1 red and blue texture yourself
/// var pixels = new byte[] { 255, 0, 0, 255, 0, 0, 255, 255 };
/// panel.Style.BackgroundImage = Texture.FromPixels(2, 1, pixels);
/// </code></example>
public sealed class Texture
{
    private const int MaxSvgSize = 4096;

    private static readonly Dictionary<string, Texture> Loaded = new(StringComparer.OrdinalIgnoreCase);

    private readonly List<DrawList> _paints = [];

    private Texture(int width, int height, byte[] pixels)
    {
        Width = width;
        Height = height;
        Pixels = pixels;
    }

    private Texture(int width, int height, GpuFontText.Raster raster)
        : this(width, height, [])
    {
        Raster = raster;
    }

    /// <summary>How wide the picture is, in pixels.</summary>
    public int Width { get; }

    /// <summary>How tall the picture is, in pixels.</summary>
    public int Height { get; }

    /// <summary>The RGBA pixels, four bytes each, from the top row down. Use <see cref="Update"/> to change them. Empty for a render target.</summary>
    public byte[] Pixels { get; }

    /// <summary>Whether the pixels' colors are already multiplied by their alpha.</summary>
    public bool PremultipliedAlpha { get; private init; }

    /// <summary>Whether it was made with <see cref="CreateRenderTarget"/>, so you draw into it with <see cref="Painter.Begin(Texture)"/>.</summary>
    public bool IsRenderTarget { get; private init; }

    internal int DirtyVersion { get; private set; }

    internal GpuFontText.Raster? Raster { get; private set; }

    /// <summary>
    /// Makes a texture from your own RGBA pixels, four bytes each, from the top row down. Pass
    /// <paramref name="premultipliedAlpha"/> as true if the colors are already multiplied by their alpha.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="pixels"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="width"/> or <paramref name="height"/> isn't above 0, or <paramref name="pixels"/> isn't exactly <paramref name="width"/> × <paramref name="height"/> × 4 bytes.</exception>
    public static Texture FromPixels(int width, int height, byte[] pixels, bool premultipliedAlpha = false)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfNotEqual(pixels.LongLength, (long)width * height * 4);
        return new Texture(width, height, pixels) { PremultipliedAlpha = premultipliedAlpha };
    }

    /// <summary>
    /// Replaces some or all of the pixels with RGBA <paramref name="data"/>, four bytes each, from the top row down.
    /// Leave the rest out to replace the whole picture, or pass a rectangle: <paramref name="x"/> and <paramref name="y"/>
    /// are its top left corner, and a <paramref name="width"/> or <paramref name="height"/> of 0 reaches the picture's edge.
    /// </summary>
    /// <example><code>
    /// // Paint the top left 16x16 corner with new pixels
    /// texture.Update(corner, 0, 0, 16, 16);
    /// </code></example>
    /// <exception cref="ArgumentOutOfRangeException">The rectangle doesn't fit inside the picture, or <paramref name="data"/> is too short to fill it.</exception>
    /// <exception cref="InvalidOperationException">It's a render target; draw into it with <see cref="Painter.Begin(Texture)"/> instead.</exception>
    public void Update(ReadOnlySpan<byte> data, int x = 0, int y = 0, int width = 0, int height = 0)
    {
        if (IsRenderTarget)
        {
            throw new InvalidOperationException("Draw into a render target with Painter.Begin instead.");
        }

        width = width == 0 ? Width - x : width;
        height = height == 0 ? Height - y : height;
        if (x < 0 || y < 0 || width <= 0 || height <= 0 || x + width > Width || y + height > Height)
        {
            throw new ArgumentOutOfRangeException(nameof(data), "The rectangle must lie inside the texture.");
        }

        var row = width * 4;
        ArgumentOutOfRangeException.ThrowIfLessThan(data.Length, row * height);
        for (int i = 0; i < height; i++)
        {
            data.Slice(i * row, row).CopyTo(Pixels.AsSpan((((y + i) * Width) + x) * 4, row));
        }

        DirtyVersion++;
    }

    /// <summary>
    /// Makes a transparent texture you draw into with <see cref="Painter.Begin(Texture)"/>, then show like any other
    /// texture.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="width"/> or <paramref name="height"/> isn't above 0.</exception>
    public static Texture CreateRenderTarget(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        return new Texture(width, height, []) { PremultipliedAlpha = true, IsRenderTarget = true };
    }

    internal static Texture FromRaster(int width, int height, GpuFontText.Raster raster) => new(width, height, raster);

    internal void AddPaint(DrawList list)
    {
        lock (_paints)
        {
            _paints.Add(list);
        }
    }

    internal List<DrawList> TakePaints()
    {
        lock (_paints)
        {
            List<DrawList> paints = [.. _paints];
            _paints.Clear();
            return paints;
        }
    }

    internal void SetRaster(GpuFontText.Raster raster)
    {
        Raster = raster;
        DirtyVersion++;
    }

    /// <summary>
    /// Loads a PNG, JPEG, WebP, GIF or SVG. A relative <paramref name="path"/> starts from your program's folder.
    /// An SVG takes a size and a color after its name, like <c>icons/save.svg?w=64&amp;h=64&amp;color=white</c>; see
    /// <see cref="FromSvg"/>. Loading the same file twice gives you the same texture, so an <see cref="Update"/> shows up
    /// everywhere it's used.
    /// </summary>
    /// <exception cref="IOException">The file can't be read or isn't an image.</exception>
    public static Texture FromFile(string path)
    {
        var parts = path.Split('?', 2);
        var fullPath = Path.GetFullPath(parts[0], AppContext.BaseDirectory);
        var query = parts.Length > 1 ? parts[1] : null;
        var key = query is null ? fullPath : $"{fullPath}?{query}";
        lock (Loaded)
        {
            if (!Loaded.TryGetValue(key, out var texture))
            {
                texture = IsSvg(fullPath) ? LoadSvg(fullPath, query) : Decode(fullPath);
                Loaded[key] = texture;
            }

            return texture;
        }
    }

    /// <summary>
    /// Draws SVG markup into a texture <paramref name="width"/> by <paramref name="height"/> pixels, with the picture
    /// kept in shape and centered. Give only one of them to get the picture's own shape, or neither to get the size
    /// the SVG asks for. Pass <paramref name="color"/> to paint the whole picture in that color.
    /// </summary>
    /// <example><code>
    /// // A white 32x32 icon
    /// var icon = Texture.FromSvg(File.ReadAllText("icons/save.svg"), 32, 32, Color.White);
    /// </code></example>
    /// <exception cref="FormatException"><paramref name="svg"/> isn't SVG markup, or has no size to draw at.</exception>
    public static Texture FromSvg(string svg, int? width = null, int? height = null, Color? color = null)
    {
        Svg.SvgDocument document;
        try
        {
            document = Svg.SvgDocument.FromSvg<Svg.SvgDocument>(svg.Trim());
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            throw new FormatException($"That isn't SVG markup: {e.Message}", e);
        }

        int nativeWidth = (int)MathF.Floor(document.Width.Value);
        int nativeHeight = (int)MathF.Floor(document.Height.Value);
        var resolvedWidth = width ?? nativeWidth;
        var resolvedHeight = height ?? nativeHeight;
        if (width is { } onlyWidth && height is null && nativeWidth > 0)
        {
            resolvedHeight = onlyWidth * nativeHeight / nativeWidth;
        }

        if (height is { } onlyHeight && width is null && nativeHeight > 0)
        {
            resolvedWidth = onlyHeight * nativeWidth / nativeHeight;
        }

        using var drawing = new SKSvg();
        drawing.FromSvgDocument(document);
        return (drawing.Picture is { } picture ? RasterizeSvg(picture, resolvedWidth, resolvedHeight, color, null) : null)
            ?? throw new FormatException("The SVG has no size to draw at.");
    }

    internal static Texture? RasterizeSvg(SKPicture picture, int width, int height, Color? color, Texture? reuse)
    {
        width = Math.Min(width, MaxSvgSize);
        height = Math.Min(height, MaxSvgSize);
        var bounds = picture.CullRect;
        if (width <= 0 || height <= 0 || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return null;
        }

        using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var canvas = new SKCanvas(bitmap);
        using var paint = new SKPaint();
        if (color is { } tint)
        {
            paint.ColorFilter = SKColorFilter.CreateBlendMode((SKColor)tint.ToSkF(), SKBlendMode.SrcIn);
        }

        canvas.Translate(width / 2f, height / 2f);
        canvas.Scale(Math.Min(width / bounds.Width, height / bounds.Height));
        canvas.Translate(-(bounds.Left + (bounds.Width / 2)), -(bounds.Top + (bounds.Height / 2)));
        canvas.DrawPicture(picture, paint);
        canvas.Flush();

        if (reuse is not null && reuse.Width == width && reuse.Height == height)
        {
            reuse.Update(bitmap.Bytes);
            return reuse;
        }

        return FromPixels(width, height, bitmap.Bytes);
    }

    private static bool IsSvg(string path) => Path.GetExtension(path).Equals(".svg", StringComparison.OrdinalIgnoreCase);

    private static Texture LoadSvg(string path, string? query)
    {
        int? width = null;
        int? height = null;
        Color? color = null;
        if (query is not null)
        {
            var values = HttpUtility.ParseQueryString(query);
            width = int.TryParse(values.Get("w"), out var parsedWidth) ? parsedWidth : null;
            height = int.TryParse(values.Get("h"), out var parsedHeight) ? parsedHeight : null;
            color = Color.Parse(values.Get("color"));
        }

        try
        {
            return FromSvg(File.ReadAllText(path), width, height, color);
        }
        catch (FormatException e)
        {
            throw new IOException($"{path} isn't an SVG that can be drawn: {e.Message}", e);
        }
    }

    /// <summary>Makes a texture from the bytes of a PNG, JPEG, WebP or GIF, like one read out of a zip file.</summary>
    /// <exception cref="InvalidDataException"><paramref name="data"/> isn't an image.</exception>
    public static Texture FromImage(byte[] data)
    {
        using var stream = new MemoryStream(data);
        return Decode(stream) ?? throw new InvalidDataException("That isn't an image.");
    }

    private static Texture Decode(string path)
    {
        using var stream = File.OpenRead(path);
        return Decode(stream) ?? throw new IOException($"{path} isn't an image.");
    }

    private static Texture? Decode(Stream stream)
    {
        using var codec = SKCodec.Create(stream);
        if (codec is null)
        {
            return null;
        }

        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bitmap = SKBitmap.Decode(codec, info);
        return bitmap is null ? null : new Texture(bitmap.Width, bitmap.Height, bitmap.Bytes);
    }
}
