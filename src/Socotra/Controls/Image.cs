using Microsoft.AspNetCore.Components;
using Socotra.Layout;

namespace Socotra;

/// <summary>
/// A panel that shows a picture. Without a set size it takes the picture's size, scaled with the UI. <c>object-fit</c>
/// decides how the picture fills the panel, and <c>background-size</c> and <c>background-position</c> can place it exactly.
/// </summary>
/// <example>
/// <code>
/// // A logo that keeps its shape inside a fixed box
/// var logo = new Image { Parent = header, Texture = Texture.FromFile("ui/logo.png") };
/// logo.Style.Set("width: 200px; height: 100px; object-fit: contain;");
/// </code>
/// </example>
public class Image : Panel
{
    private Texture? _texture;
    private Vector2 _textureSize;
    private int _textureVersion;
    private float _measuredScale = 1;

    /// <summary>Makes an empty image.</summary>
    public Image()
    {
        LayoutTree.SetMeasure(MeasureTexture);
    }

    /// <summary>The picture to show.</summary>
    [Parameter]
    public Texture? Texture
    {
        get => _texture;
        set
        {
            if (_texture == value)
            {
                return;
            }

            _texture = value;
            _textureSize = value is null ? Vector2.Zero : new Vector2(value.Width, value.Height);
            _textureVersion = value?.DirtyVersion ?? 0;
            LayoutTree.MarkDirty();
            SetNeedsPreLayout();
        }
    }

    /// <summary>
    /// Shows the picture file at <paramref name="name"/>, relative to the program's folder unless the path is absolute.
    /// If the file can't be read or isn't an image, a warning is logged and the image shows nothing.
    /// </summary>
    public void SetTexture(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        try
        {
            Texture = Texture.FromFile(name);
        }
        catch (IOException e)
        {
            Log.Warning($"Can't load {name}: {e.Message}");
            Texture = null;
        }
    }

    /// <summary>Sets <c>src</c> from markup, a path to the picture.</summary>
    public override void SetProperty(string name, string? value)
    {
        base.SetProperty(name, value);
        if (name == "src" && value is not null)
        {
            SetTexture(value);
        }
    }

    /// <inheritdoc/>
    public override void Tick()
    {
        base.Tick();
        if (_texture is null || _texture.DirtyVersion == _textureVersion)
        {
            return;
        }

        _textureVersion = _texture.DirtyVersion;
        var size = new Vector2(_texture.Width, _texture.Height);
        if (size == _textureSize)
        {
            return;
        }

        _textureSize = size;
        LayoutTree.MarkDirty();
        SetNeedsPreLayout();
    }

    /// <inheritdoc/>
    public override void OnDraw(Painter painter)
    {
        var size = ComputedStyle?.ObjectFit switch
        {
            ObjectFit.Contain => Length.Contain,
            ObjectFit.Cover => Length.Cover,
            ObjectFit.Fill => Length.Percent(100),
            _ => Length.Auto,
        };

        DrawTexture(painter, _texture, size);
    }

    internal override void PreLayout(LayoutCascade cascade)
    {
        base.PreLayout(cascade);
        if (ScaleToScreen != _measuredScale)
        {
            LayoutTree.MarkDirty();
        }
    }

    private Vector2 MeasureTexture(float width, MeasureMode widthMode, float height, MeasureMode heightMode)
    {
        if (_texture is null)
        {
            return Vector2.Zero;
        }

        var (w, h) = ((float)_texture.Width, (float)_texture.Height);
        _measuredScale = ScaleToScreen;
        var ideal = new Vector2(w, h) * ScaleToScreen;
        if (widthMode == MeasureMode.Exactly)
        {
            return new Vector2(width, h * width / w);
        }

        if (heightMode == MeasureMode.Exactly)
        {
            return new Vector2(w * height / h, height);
        }

        if (widthMode == MeasureMode.AtMost && heightMode == MeasureMode.AtMost && (width < ideal.X || height < ideal.Y))
        {
            var scale = MathF.Min(width / w, height / h);
            return new Vector2(w * scale, h * scale);
        }

        if (widthMode == MeasureMode.AtMost && width < ideal.X)
        {
            return new Vector2(width, h * width / w);
        }

        if (heightMode == MeasureMode.AtMost && height < ideal.Y)
        {
            return new Vector2(w * height / h, height);
        }

        return ideal;
    }
}

/// <summary>Adds images through <see cref="Panel.Add"/>.</summary>
public static class ImageConstructor
{
    /// <summary>Adds an image showing the file at <paramref name="image"/>, with the classes in <paramref name="classname"/>.</summary>
    public static Image Image(this PanelCreator self, string? image = null, string? classname = null)
    {
        var control = self.panel.AddChild<Image>(classname);
        if (image is not null)
        {
            control.SetTexture(image);
        }

        return control;
    }
}
