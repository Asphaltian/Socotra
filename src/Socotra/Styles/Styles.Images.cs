namespace Socotra;

public partial class Styles
{
    private static readonly Lazy<Texture?> NoImage = new(static () => null);

    private enum ImageTarget
    {
        Background,
        Mask,
        BorderImage,
    }

    private bool SetImage(string value, ImageTarget target)
    {
        var p = new Parse(value).SkipWhitespaceAndNewlines();
        if (p.Is("none", ignoreCase: true))
        {
            SetImageField(target, NoImage);
            return true;
        }

        if (ReadFunction(ref p, "url") is { } url)
        {
            url = url.Trim(' ', '"', '\'');
            SetImageField(target, string.IsNullOrWhiteSpace(url) ? NoImage : LoadImage(url));
            return true;
        }

        foreach (var type in (ReadOnlySpan<GradientType>)[GradientType.Linear, GradientType.Radial, GradientType.Conic])
        {
            if (ReadFunction(ref p, $"{type.ToString().ToLowerInvariant()}-gradient") is not { } gradient)
            {
                continue;
            }

            if (target == ImageTarget.Background)
            {
                _backgroundImage = NoImage;
                BackgroundGradient = TryParseGradient(type, gradient, out var info) ? info : default;
                return true;
            }

            SetImageField(target, new Lazy<Texture?>(BakeGradient(type, gradient, out var angle)));
            if (target == ImageTarget.Mask)
            {
                _maskAngle = angle;
                _maskSizeX = _maskSizeY = Length.Percent(100);
                _maskRepeat = Socotra.BackgroundRepeat.Clamp;
            }

            return true;
        }

        Log.Warning($"Unknown image type \"{value}\"");
        return false;
    }

    private void SetImageField(ImageTarget target, Lazy<Texture?> image)
    {
        switch (target)
        {
            case ImageTarget.Background:
                BackgroundGradient = default;
                _backgroundImage = image;
                break;
            case ImageTarget.Mask:
                _maskImage = image;
                break;
            default:
                _borderImageSource = image;
                break;
        }
    }

    private static Lazy<Texture?> LoadImage(string path) => new(() =>
    {
        try
        {
            return Texture.FromFile(path);
        }
        catch (IOException e)
        {
            Log.Warning($"Can't load {path}: {e.Message}");
            return null;
        }
    });

    private static string? ReadFunction(ref Parse p, string name)
    {
        var start = p.Pointer;
        if (!p.TrySkip(name, ignoreCase: true) || !p.SkipWhitespaceAndNewlines().Is('('))
        {
            p.Pointer = start;
            return null;
        }

        return p.ReadInnerBrackets();
    }

    private bool SetBackground(string value)
    {
        _backgroundImage = NoImage;
        _backgroundColor = Color.Transparent;
        BackgroundGradient = default;
        _backgroundClip = Socotra.BackgroundClip.BorderBox;

        var lengths = new List<Length>();
        foreach (var word in SplitValues(value))
        {
            if (word.Contains('('))
            {
                if (word.StartsWith("url(", StringComparison.OrdinalIgnoreCase) || word.Contains("gradient(", StringComparison.OrdinalIgnoreCase))
                {
                    SetImage(word, ImageTarget.Background);
                }
                else if (Color.ParseStyle(word) is { } functionColor)
                {
                    _backgroundColor = functionColor;
                }
            }
            else if (word is "/" or "none")
            {
                continue;
            }
            else if (Length.Parse(word) is { } length)
            {
                lengths.Add(length);
            }
            else if (word is "repeat-x" or "repeat-y" or "repeat" or "space" or "round" or "no-repeat")
            {
                PropertiesByName["background-repeat"].Set(this, word);
            }
            else if (!PropertiesByName["background-clip"].Set(this, word))
            {
                if (Color.ParseStyle(word) is { } color)
                {
                    _backgroundColor = color;
                }
                else
                {
                    Log.Warning($"Unrecognized part {word} in background");
                }
            }
        }

        switch (lengths)
        {
            case [var position]:
                (_backgroundPositionX, _backgroundPositionY) = (position, position);
                break;
            case [var position, var size]:
                (_backgroundPositionX, _backgroundPositionY, _backgroundSizeX, _backgroundSizeY) = (position, position, size, size);
                break;
            case [var x, var y, var size]:
                (_backgroundPositionX, _backgroundPositionY, _backgroundSizeX, _backgroundSizeY) = (x, y, size, size);
                break;
            case [var x, var y, var width, var height, ..]:
                (_backgroundPositionX, _backgroundPositionY, _backgroundSizeX, _backgroundSizeY) = (x, y, width, height);
                break;
        }

        return true;
    }

    private bool SetMask(string value)
    {
        var p = new Parse(value).SkipWhitespaceAndNewlines();
        if (!SetImage(p.ReadWord(null, readUntilEnd: true, respectParens: true)!, ImageTarget.Mask))
        {
            return false;
        }

        if (!p.TryReadPositionAndSize(out var positionX, out var positionY, out var sizeX, out var sizeY))
        {
            (positionX, positionY) = (0, 0);
        }

        (_maskPositionX, _maskPositionY) = (positionX, positionY);
        if (sizeX.Unit != LengthUnit.Auto)
        {
            _maskSizeX = sizeX;
        }

        if (sizeY.Unit != LengthUnit.Auto)
        {
            _maskSizeY = sizeY;
        }

        if (p.TryReadRepeat(out var repeat))
        {
            PropertiesByName["mask-repeat"].Set(this, repeat);
        }

        if (p.TryReadMaskMode(out var mode))
        {
            PropertiesByName["mask-mode"].Set(this, mode);
        }

        return true;
    }

    private bool SetBorderImage(string value)
    {
        var p = new Parse(value).SkipWhitespaceAndNewlines();
        if (!SetImage(p.ReadWord(null, readUntilEnd: true, respectParens: true)!, ImageTarget.BorderImage))
        {
            return false;
        }

        var slices = new List<Length>();
        var widths = new List<Length>();
        bool afterSlash = false;
        while (!p.SkipWhitespaceAndNewlines().IsEnd)
        {
            if (p.TrySkip("stretch", ignoreCase: true))
            {
                _borderImageRepeat = Socotra.BorderImageRepeat.Stretch;
            }
            else if (p.TrySkip("round", ignoreCase: true))
            {
                _borderImageRepeat = Socotra.BorderImageRepeat.Round;
            }
            else if (p.TrySkip("fill", ignoreCase: true))
            {
                _borderImageFill = Socotra.BorderImageFill.Filled;
            }
            else if (p.TrySkip("/"))
            {
                if (slices.Count == 0 || afterSlash)
                {
                    return false;
                }

                afterSlash = true;
            }
            else if (p.TryReadLength(out var length))
            {
                (afterSlash ? widths : slices).Add(length);
            }
            else
            {
                return false;
            }
        }

        if (slices.Count == 0)
        {
            slices.Add((_borderImageSource?.Value?.Width ?? 0) / 3.0f);
        }

        (_borderImageWidthTop, _borderImageWidthRight, _borderImageWidthBottom, _borderImageWidthLeft) = ExpandFour(slices);
        (_borderTopWidth, _borderRightWidth, _borderBottomWidth, _borderLeftWidth) = ExpandFour(widths.Count == 0 ? slices : widths);
        return true;
    }
}
