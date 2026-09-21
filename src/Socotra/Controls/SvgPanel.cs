using Svg.Skia;

namespace Socotra;

/// <summary>
/// Shows an SVG file, drawn sharp at whatever size the panel is. The picture keeps its shape and is centered, like
/// <c>object-fit: contain</c>. Set <c>background-size</c> to draw it at another size. In markup it's <c>&lt;svg src="..."&gt;</c>.
/// </summary>
/// <example>
/// <code>
/// // A 32px icon tinted white
/// var icon = new SvgPanel { Parent = toolbar, Src = "assets/ui/save.svg", Color = "white" };
/// icon.Style.Set("width: 32px; height: 32px;");
/// </code>
/// </example>
public class SvgPanel : Panel
{
    private string? _src;
    private string? _color;
    private SKSvg? _svg;
    private Texture? _texture;
    private int _sizeHash;

    /// <summary>The SVG file to show. A relative path starts from your program's folder.</summary>
    public string? Src
    {
        get => _src;
        set
        {
            if (_src == value)
            {
                return;
            }

            _src = value;
            LoadSvg();
            ReloadTexture();
        }
    }

    /// <summary>A color to paint the whole picture in, written the way you'd write it in a stylesheet, like <c>white</c> or <c>#ff8000</c>. Leave it null to keep the picture's own colors.</summary>
    public string? Color
    {
        get => _color;
        set
        {
            if (_color == value)
            {
                return;
            }

            _color = value;
            ReloadTexture();
        }
    }

    /// <inheritdoc/>
    public override void OnDraw(Painter painter) => DrawTexture(painter, _texture, Length.Cover);

    /// <inheritdoc/>
    public override void OnDeleted()
    {
        base.OnDeleted();
        _svg?.Dispose();
        _svg = null;
    }

    internal override void FinalLayout(Vector2 offset)
    {
        base.FinalLayout(offset);
        if (!IsVisible || ComputedStyle is not { } style)
        {
            return;
        }

        var hash = HashCode.Combine(Box.Rect.Width, Box.Rect.Height, style.BackgroundSizeX, style.BackgroundSizeY);
        if (hash == _sizeHash)
        {
            return;
        }

        _sizeHash = hash;
        ReloadTexture();
    }

    private void ReloadTexture()
    {
        if (ComputedStyle is not { } style)
        {
            return;
        }

        var width = Box.Rect.Width;
        var height = Box.Rect.Height;
        if (style.BackgroundSizeX is { Unit: not LengthUnit.Undefined } sizeX)
        {
            width = sizeX.GetPixels(width);
        }

        if (style.BackgroundSizeY is { Unit: not LengthUnit.Undefined } sizeY)
        {
            height = sizeY.GetPixels(height);
        }

        _texture = _svg?.Picture is { } picture ? Texture.RasterizeSvg(picture, (int)width, (int)height, Socotra.Color.Parse(Color), _texture) : null;
    }

    private void LoadSvg()
    {
        _svg?.Dispose();
        _svg = null;
        if (string.IsNullOrEmpty(_src))
        {
            return;
        }

        var svg = new SKSvg();
        try
        {
            var contents = File.ReadAllText(Path.GetFullPath(_src, AppContext.BaseDirectory));
            svg.FromSvgDocument(Svg.SvgDocument.FromSvg<Svg.SvgDocument>(contents.Trim()));
            _svg = svg;
        }
        catch (Exception e)
        {
            svg.Dispose();
            Log.Warning($"Couldn't read the SVG {_src}: {e.Message}");
        }
    }
}
