using System.Globalization;
using System.Runtime.InteropServices;

namespace Socotra;

/// <summary>An sRGB color with red, green, blue and alpha from 0 to 1.</summary>
/// <example><code>
/// // Three ways to get the same orange
/// var a = new Color(1, 0.5f, 0);
/// var b = Color.FromBytes(255, 128, 0);
/// var c = Color.Parse("#ff8000");
///
/// // Half see-through and a bit darker
/// panel.Style.BackgroundColor = a.WithAlpha(0.5f).Darken(0.2f);
/// </code></example>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct Color(float R, float G, float B, float A = 1)
{
    internal static readonly Color CurrentColor = new(float.NaN, float.NaN, float.NaN, float.NaN);

    /// <summary>Fully transparent black.</summary>
    public static Color Transparent => new(0, 0, 0, 0);

    /// <summary>Opaque white.</summary>
    public static Color White => new(1, 1, 1);

    /// <summary>Opaque black.</summary>
    public static Color Black => new(0, 0, 0);

    internal bool IsCurrentColor => float.IsNaN(R);

    /// <summary>This color with its alpha set to <paramref name="alpha"/>.</summary>
    public Color WithAlpha(float alpha) => this with { A = alpha };

    /// <summary>This color with its alpha multiplied by <paramref name="alpha"/>, so 0.5 makes it half as opaque.</summary>
    public Color WithAlphaMultiplied(float alpha) => this with { A = A * alpha };

    /// <summary>This color with its hue turned by <paramref name="degrees"/>.</summary>
    public Color AdjustHue(float degrees)
    {
        var (hue, saturation, value) = ToHsv();
        return FromHsv(hue + degrees, saturation, value, A);
    }

    /// <summary>This color darkened, from 0 (unchanged) to 1 (black).</summary>
    public Color Darken(float fraction) => new(R * (1 - fraction), G * (1 - fraction), B * (1 - fraction), A);

    /// <summary>This color lightened, from 0 (unchanged) to 1 (twice as bright).</summary>
    public Color Lighten(float fraction) => new(R * (1 + fraction), G * (1 + fraction), B * (1 + fraction), A);

    /// <summary>This color inverted. The alpha stays the same.</summary>
    public Color Invert() => new(1 - R, 1 - G, 1 - B, A);

    /// <summary>This color with less saturation, from 0 (unchanged) to 1 (gray).</summary>
    public Color Desaturate(float fraction)
    {
        var (hue, saturation, value) = ToHsv();
        return FromHsv(hue, saturation * (1 - fraction), value, A);
    }

    /// <summary>This color with more saturation, from 0 (unchanged) to 1 (twice as saturated).</summary>
    public Color Saturate(float fraction)
    {
        var (hue, saturation, value) = ToHsv();
        return FromHsv(hue, saturation * (1 + fraction), value, A);
    }

    /// <summary>A blend of <paramref name="a"/> and <paramref name="b"/>: 0 gives <paramref name="a"/>, 1 gives <paramref name="b"/>.</summary>
    public static Color Lerp(Color a, Color b, float delta) => new(
        a.R + ((b.R - a.R) * delta),
        a.G + ((b.G - a.G) * delta),
        a.B + ((b.B - a.B) * delta),
        a.A + ((b.A - a.A) * delta));

    /// <summary>A color from red, green, blue and alpha values from 0 to 255.</summary>
    public static Color FromBytes(int r, int g, int b, int a = 255) => new(r / 255.0f, g / 255.0f, b / 255.0f, a / 255.0f);

    /// <summary>
    /// Reads a color written the way you'd write it in a stylesheet: a name, <c>#hex</c>, <c>rgb()</c>, <c>hsl()</c>,
    /// <c>hwb()</c>, <c>lab()</c>, <c>oklch()</c>, or an SCSS color function like <c>darken()</c> or <c>mix()</c>.
    /// Add <c>* 2</c> after it to double its brightness. Returns null if <paramref name="value"/> isn't a color.
    /// </summary>
    public static Color? Parse(string? value) => ParseStyle(value) is { IsCurrentColor: false } color ? color : null;

    internal static Color? ParseStyle(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var p = new Parse(value);
        return Parse(ref p) is { } color && p.SkipWhitespaceAndNewlines().IsEnd ? color : null;
    }

    internal static Color? Parse(ref Parse p)
    {
        p = p.SkipWhitespaceAndNewlines();
        var start = p.Pointer;
        if (ParseColor(ref p) is not { } color)
        {
            p.Pointer = start;
            return null;
        }

        return p.ReadBrightness(color);
    }

    private static Color? ParseColor(ref Parse p)
    {
        if (p.TrySkip("currentcolor", ignoreCase: true))
        {
            return CurrentColor;
        }

        if (p.Current == '#')
        {
            p.Pointer++;
            int start = p.Pointer;
            while (char.IsAsciiLetterOrDigit(p.Current))
            {
                p.Pointer++;
            }

            return ParseHex(p.Text.AsSpan(start, p.Pointer - start));
        }

        if (p.IsDigit)
        {
            return ParseFloats(ref p) ?? ParseBytes(ref p);
        }

        int nameStart = p.Pointer;
        while (char.IsAsciiLetter(p.Current) || p.Current == '-')
        {
            p.Pointer++;
        }

        var name = p.Text[nameStart..p.Pointer].ToLowerInvariant();
        if (p.Current != '(')
        {
            return name switch
            {
                "transparent" => Transparent,
                _ => NamedColors.TryGetValue(name, out var named) ? FromHex(named) : null,
            };
        }

        if (p.ReadInnerBrackets() is not { } inner)
        {
            return null;
        }

        var arguments = new Parse(inner);
        return name switch
        {
            "rgb" or "rgba" => ParseRgb(ref arguments),
            "hsl" or "hsla" => ParseHsl(ref arguments),
            "hwb" => ParseHwb(ref arguments),
            "lab" => ParseLab(ref arguments),
            "oklch" => ParseOklch(ref arguments),
            "color" => ParseColorFunction(ref arguments),
            "invert" => ReadColor(ref arguments) is { } color && arguments.SkipWhitespaceAndNewlines().IsEnd ? color.Invert() : null,
            "mix" or "lerp" => ParseMix(ref arguments),
            "adjust-hue" => ParseAdjust(ref arguments, static (c, amount) => c.AdjustHue(amount.GetPixels(1))),
            "darken" => ParseAdjust(ref arguments, static (c, amount) => c.Darken(amount.GetFraction())),
            "lighten" => ParseAdjust(ref arguments, static (c, amount) => c.Lighten(amount.GetFraction())),
            "desaturate" => ParseAdjust(ref arguments, static (c, amount) => c.Desaturate(amount.GetFraction())),
            "saturate" => ParseAdjust(ref arguments, static (c, amount) => c.Saturate(amount.GetFraction())),
            _ => null,
        };
    }

    private static Color? ParseHex(ReadOnlySpan<char> hex)
    {
        if (!uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var packed))
        {
            return null;
        }

        return hex.Length switch
        {
            3 => FromBytes((int)((packed >> 8) & 0xF) * 17, (int)((packed >> 4) & 0xF) * 17, (int)(packed & 0xF) * 17),
            4 => FromBytes((int)((packed >> 12) & 0xF) * 17, (int)((packed >> 8) & 0xF) * 17, (int)((packed >> 4) & 0xF) * 17, (int)(packed & 0xF) * 17),
            6 => FromHex(packed),
            8 => FromBytes((int)(packed >> 24), (int)((packed >> 16) & 0xFF), (int)((packed >> 8) & 0xFF), (int)(packed & 0xFF)),
            _ => null,
        };
    }

    private static Color FromHex(uint rgb) => FromBytes((int)((rgb >> 16) & 0xFF), (int)((rgb >> 8) & 0xFF), (int)(rgb & 0xFF));

    private static Color? ParseFloats(ref Parse p)
    {
        var start = p.Pointer;
        if (!p.TryReadFloat(out var r) || p.SkipWhitespaceAndNewlines().Current != ',')
        {
            p.Pointer = start;
            return null;
        }

        Span<float> channels = [r, 0, 0, 1];
        for (int i = 1; i < 4 && p.TrySkipCommaSeparation() && p.TryReadFloat(out var channel); i++)
        {
            channels[i] = channel;
        }

        return new Color(channels[0], channels[1], channels[2], channels[3]);
    }

    private static Color? ParseBytes(ref Parse p)
    {
        Span<int> channels = [0, 0, 0, 255];
        for (int i = 0; i < 4; i++)
        {
            p = p.SkipWhitespaceAndNewlines();
            if (i == 3 && !p.IsDigit)
            {
                break;
            }

            var word = p.ReadUntilWhitespaceOrNewlineOrEnd();
            if (!int.TryParse(word, NumberStyles.None, CultureInfo.InvariantCulture, out channels[i]))
            {
                return null;
            }
        }

        return FromBytes(channels[0], channels[1], channels[2], channels[3]);
    }

    private static Color? ParseRgb(ref Parse p)
    {
        Color color;
        p.SkipWhitespaceAndNewlines();
        if (p.IsDigit || p.Current == '.')
        {
            Span<float> channels = stackalloc float[3];
            for (int i = 0; i < 3; i++)
            {
                if (!p.TryReadFloat(out channels[i]))
                {
                    return null;
                }

                channels[i] = p.TrySkip("%") ? channels[i] / 100.0f : channels[i] / 255.0f;
                p = p.SkipWhitespaceAndNewlines(",");
            }

            color = new Color(channels[0], channels[1], channels[2]);
        }
        else if (ReadColor(ref p) is { } nested)
        {
            color = nested;
        }
        else
        {
            return null;
        }

        return ReadAlpha(ref p, color.A) is { } alpha ? color.WithAlpha(alpha) : null;
    }

    private static Color? ParseHsl(ref Parse p)
    {
        if (!p.TryReadFloat(out var hue))
        {
            return null;
        }

        hue = ReadAngleUnit(ref p, hue);
        p.SkipWhitespaceAndNewlines(",");
        if (!p.TryReadFloat(out var saturation))
        {
            return null;
        }

        p.SkipWhitespaceAndNewlines(",%");
        if (!p.TryReadFloat(out var lightness))
        {
            return null;
        }

        p.TrySkip("%");
        if (ReadAlpha(ref p, 1) is not { } alpha)
        {
            return null;
        }

        saturation = Math.Clamp(saturation / 100.0f, 0, 1);
        lightness = Math.Clamp(lightness / 100.0f, 0, 1);
        hue = ((hue % 360.0f) + 360.0f) % 360.0f;

        float Channel(float n)
        {
            var k = (n + (hue / 30.0f)) % 12.0f;
            return lightness - (saturation * MathF.Min(lightness, 1 - lightness) * Math.Clamp(MathF.Min(k - 3, 9 - k), -1, 1));
        }

        return new Color(Channel(0), Channel(8), Channel(4), alpha);
    }

    private static Color? ParseHwb(ref Parse p)
    {
        if (!p.TryReadFloat(out var hue))
        {
            return null;
        }

        hue = ReadAngleUnit(ref p, hue);
        p.SkipWhitespaceAndNewlines(",");
        if (!p.TryReadFloat(out var whiteness))
        {
            return null;
        }

        p.SkipWhitespaceAndNewlines(",%");
        if (!p.TryReadFloat(out var blackness))
        {
            return null;
        }

        p.TrySkip("%");
        if (ReadAlpha(ref p, 1) is not { } alpha)
        {
            return null;
        }

        var white = whiteness / 100.0;
        var black = blackness / 100.0;
        if (white + black >= 1)
        {
            var gray = (float)(white / (white + black));
            return new Color(gray, gray, gray, alpha);
        }

        var h = ((hue % 360.0) + 360.0) % 360.0;
        float Channel(double n)
        {
            var k = (n + (h / 30.0)) % 12.0;
            var pure = 0.5 - (0.5 * Math.Max(-1.0, Math.Min(Math.Min(k - 3.0, 9.0 - k), 1.0)));
            return (float)((pure * (1.0 - white - black)) + white);
        }

        return new Color(Channel(0), Channel(8), Channel(4), alpha);
    }

    private static Color? ParseLab(ref Parse p)
    {
        if (!p.TryReadFloat(out var l))
        {
            return null;
        }

        p.TrySkip("%");
        p.SkipWhitespaceAndNewlines(",");
        if (!p.TryReadFloat(out var a))
        {
            return null;
        }

        a = p.TrySkip("%") ? a / 100.0f * 125.0f : a;
        p.SkipWhitespaceAndNewlines(",");
        if (!p.TryReadFloat(out var b))
        {
            return null;
        }

        b = p.TrySkip("%") ? b / 100.0f * 125.0f : b;
        if (ReadAlpha(ref p, 1) is not { } alpha)
        {
            return null;
        }

        const double epsilon = 216.0 / 24389.0;
        const double kappa = 24389.0 / 27.0;
        var fy = (l + 16.0) / 116.0;
        var fx = (a / 500.0) + fy;
        var fz = fy - (b / 200.0);
        var x = (fx * fx * fx > epsilon ? fx * fx * fx : ((116.0 * fx) - 16.0) / kappa) * (0.3127 / 0.3290);
        var y = l > kappa * epsilon ? fy * fy * fy : l / kappa;
        var z = (fz * fz * fz > epsilon ? fz * fz * fz : ((116.0 * fz) - 16.0) / kappa) * ((1.0 - 0.3127 - 0.3290) / 0.3290);

        return new Color(
            LinearToSrgb((3.2409699419045226 * x) - (1.5373831775700940 * y) - (0.4986107602930034 * z)),
            LinearToSrgb((-0.9692436362808796 * x) + (1.8759675015077202 * y) + (0.0415550574071756 * z)),
            LinearToSrgb((0.0556300796969936 * x) - (0.2039769588889765 * y) + (1.0569715142428786 * z)),
            alpha);
    }

    private static Color? ParseOklch(ref Parse p)
    {
        if (!p.TryReadFloat(out var lightness))
        {
            return null;
        }

        lightness = p.TrySkip("%") ? lightness / 100.0f : lightness;
        p.SkipWhitespaceAndNewlines(",");
        if (!p.TryReadFloat(out var chroma))
        {
            return null;
        }

        chroma = p.TrySkip("%") ? chroma / 100.0f * 0.4f : chroma;
        p.SkipWhitespaceAndNewlines(",");
        if (!p.TryReadFloat(out var hue))
        {
            return null;
        }

        hue = ReadAngleUnit(ref p, hue);
        if (ReadAlpha(ref p, 1) is not { } alpha)
        {
            return null;
        }

        var h = hue * (Math.PI / 180.0);
        var a = chroma * Math.Cos(h);
        var b = chroma * Math.Sin(h);
        var l = Math.Pow(lightness + (0.3963377774 * a) + (0.2158037573 * b), 3);
        var m = Math.Pow(lightness - (0.1055613458 * a) - (0.0638541728 * b), 3);
        var s = Math.Pow(lightness - (0.0894841775 * a) - (1.2914855480 * b), 3);

        return new Color(
            LinearToSrgb((4.0767416621 * l) - (3.3077115913 * m) + (0.2309699292 * s)),
            LinearToSrgb((-1.2684380046 * l) + (2.6097574011 * m) - (0.3413193965 * s)),
            LinearToSrgb((-0.0041960863 * l) - (0.7034186147 * m) + (1.7076147010 * s)),
            alpha);
    }

    private static Color? ParseColorFunction(ref Parse p)
    {
        p = p.SkipWhitespaceAndNewlines();
        if (p.TrySkip("rgba", ignoreCase: true) || p.TrySkip("rgb", ignoreCase: true))
        {
            return ParseRgb(ref p);
        }

        if (p.TrySkip("hsla", ignoreCase: true) || p.TrySkip("hsl", ignoreCase: true))
        {
            return ParseHsl(ref p);
        }

        return ReadColor(ref p) is { } color && p.SkipWhitespaceAndNewlines().IsEnd ? color : null;
    }

    private static Color? ParseMix(ref Parse p)
    {
        if (ReadColor(ref p) is not { } a || !p.TrySkipCommaSeparation() || ReadColor(ref p) is not { } b || !p.TrySkipCommaSeparation()
            || !p.TryReadLength(out var amount) || !p.SkipWhitespaceAndNewlines().IsEnd)
        {
            return null;
        }

        return Lerp(a, b, amount.GetFraction());
    }

    private static Color? ParseAdjust(ref Parse p, Func<Color, Length, Color> adjust)
    {
        if (ReadColor(ref p) is not { } color || !p.TrySkipCommaSeparation() || !p.TryReadLength(out var amount) || !p.SkipWhitespaceAndNewlines().IsEnd)
        {
            return null;
        }

        return adjust(color, amount);
    }

    private static Color? ReadColor(ref Parse p) => p.TryReadColor(out var color) ? color : null;

    private static float? ReadAlpha(ref Parse p, float fallback)
    {
        p = p.SkipWhitespaceAndNewlines(",/");
        var alpha = fallback;
        if (p.TryReadFloat(out var read))
        {
            alpha = p.TrySkip("%") ? read / 100.0f : read;
        }

        return p.SkipWhitespaceAndNewlines().IsEnd ? alpha : null;
    }

    private static float ReadAngleUnit(ref Parse p, float value)
    {
        if (!p.IsLetter)
        {
            return value;
        }

        var unit = p.ReadUntilWhitespaceOrNewlineOrEnd(",/)");
        return unit.StartsWith("grad", StringComparison.OrdinalIgnoreCase) ? value * 0.9f
            : unit.StartsWith("rad", StringComparison.OrdinalIgnoreCase) ? float.RadiansToDegrees(value)
            : unit.StartsWith("turn", StringComparison.OrdinalIgnoreCase) ? value * 360.0f
            : value;
    }

    private static float LinearToSrgb(double c) => Math.Clamp(LinearToGamma((float)c), 0, 1);

    private static float SrgbToLinear(float c) => c <= 0.04045f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);

    private static float LinearToGamma(float c) => c <= 0.0031308f ? c * 12.92f : (1.055f * MathF.Pow(c, 1.0f / 2.4f)) - 0.055f;

    internal Color ToLinear() => new(SrgbToLinear(R), SrgbToLinear(G), SrgbToLinear(B), A);

    internal Color ToSrgb() => new(LinearToGamma(R), LinearToGamma(G), LinearToGamma(B), A);

    internal Color ScaleBrightness(float scale)
    {
        scale = MathF.Max(scale, 0);
        return new Color(LinearToGamma(SrgbToLinear(R) * scale), LinearToGamma(SrgbToLinear(G) * scale), LinearToGamma(SrgbToLinear(B) * scale), A);
    }

    internal (float Hue, float Saturation, float Value) ToHsv()
    {
        var max = MathF.Max(MathF.Max(R, G), B);
        var delta = max - MathF.Min(MathF.Min(R, G), B);
        var saturation = max == 0 ? 0 : delta / max;
        if (saturation == 0)
        {
            return (0, 0, max);
        }

        var hue = R == max ? (G - B) / delta : G == max ? 2 + ((B - R) / delta) : 4 + ((R - G) / delta);
        return ((((hue * 60) % 360) + 360) % 360, saturation, max);
    }

    internal static Color FromHsv(float hue, float saturation, float value, float alpha)
    {
        if (saturation == 0)
        {
            return new Color(value, value, value, alpha);
        }

        var h = (((hue % 360) + 360) % 360) / 60.0f;
        var sector = (int)h;
        var f = h - sector;
        var p = value * (1 - saturation);
        var q = value * (1 - (saturation * f));
        var t = value * (1 - (saturation * (1 - f)));
        return sector switch
        {
            0 => new Color(value, t, p, alpha),
            1 => new Color(q, value, p, alpha),
            2 => new Color(p, value, t, alpha),
            3 => new Color(p, q, value, alpha),
            4 => new Color(t, p, value, alpha),
            _ => new Color(value, p, q, alpha),
        };
    }

    /// <summary>The color written as a stylesheet <c>rgba()</c> value.</summary>
    public override string ToString() => ToCss();

    internal string ToCss() => FormattableString.Invariant($"rgba({R * 255:0.#}, {G * 255:0.#}, {B * 255:0.#}, {A:0.###})");

    internal string ToHex()
    {
        var (r, g, b, a) = (ToByte(R), ToByte(G), ToByte(B), ToByte(A));
        return a == 255 ? $"#{r:X2}{g:X2}{b:X2}" : $"#{r:X2}{g:X2}{b:X2}{a:X2}";
    }

    internal static byte ToByte(float value) => (byte)MathF.Floor(Math.Clamp(value, 0, 1) * 255.999f);

    private static readonly Dictionary<string, uint> NamedColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["aliceblue"] = 0xF0F8FF,
        ["antiquewhite"] = 0xFAEBD7,
        ["aqua"] = 0x00FFFF,
        ["aquamarine"] = 0x7FFFD4,
        ["azure"] = 0xF0FFFF,
        ["beige"] = 0xF5F5DC,
        ["bisque"] = 0xFFE4C4,
        ["black"] = 0x000000,
        ["blanchedalmond"] = 0xFFEBCD,
        ["blue"] = 0x0000FF,
        ["blueviolet"] = 0x8A2BE2,
        ["brown"] = 0xA52A2A,
        ["burlywood"] = 0xDEB887,
        ["cadetblue"] = 0x5F9EA0,
        ["chartreuse"] = 0x7FFF00,
        ["chocolate"] = 0xD2691E,
        ["coral"] = 0xFF7F50,
        ["cornflowerblue"] = 0x6495ED,
        ["cornsilk"] = 0xFFF8DC,
        ["crimson"] = 0xDC143C,
        ["cyan"] = 0x00FFFF,
        ["darkblue"] = 0x00008B,
        ["darkcyan"] = 0x008B8B,
        ["darkgoldenrod"] = 0xB8860B,
        ["darkgray"] = 0xA9A9A9,
        ["darkgreen"] = 0x006400,
        ["darkgrey"] = 0xA9A9A9,
        ["darkkhaki"] = 0xBDB76B,
        ["darkmagenta"] = 0x8B008B,
        ["darkolivegreen"] = 0x556B2F,
        ["darkorange"] = 0xFF8C00,
        ["darkorchid"] = 0x9932CC,
        ["darkred"] = 0x8B0000,
        ["darksalmon"] = 0xE9967A,
        ["darkseagreen"] = 0x8FBC8F,
        ["darkslateblue"] = 0x483D8B,
        ["darkslategray"] = 0x2F4F4F,
        ["darkslategrey"] = 0x2F4F4F,
        ["darkturquoise"] = 0x00CED1,
        ["darkviolet"] = 0x9400D3,
        ["deeppink"] = 0xFF1493,
        ["deepskyblue"] = 0x00BFFF,
        ["dimgray"] = 0x696969,
        ["dimgrey"] = 0x696969,
        ["dodgerblue"] = 0x1E90FF,
        ["firebrick"] = 0xB22222,
        ["floralwhite"] = 0xFFFAF0,
        ["forestgreen"] = 0x228B22,
        ["fuchsia"] = 0xFF00FF,
        ["gainsboro"] = 0xDCDCDC,
        ["ghostwhite"] = 0xF8F8FF,
        ["gold"] = 0xFFD700,
        ["goldenrod"] = 0xDAA520,
        ["gray"] = 0x808080,
        ["green"] = 0x008000,
        ["greenyellow"] = 0xADFF2F,
        ["grey"] = 0x808080,
        ["honeydew"] = 0xF0FFF0,
        ["hotpink"] = 0xFF69B4,
        ["indianred"] = 0xCD5C5C,
        ["indigo"] = 0x4B0082,
        ["ivory"] = 0xFFFFF0,
        ["khaki"] = 0xF0E68C,
        ["lavender"] = 0xE6E6FA,
        ["lavenderblush"] = 0xFFF0F5,
        ["lawngreen"] = 0x7CFC00,
        ["lemonchiffon"] = 0xFFFACD,
        ["lightblue"] = 0xADD8E6,
        ["lightcoral"] = 0xF08080,
        ["lightcyan"] = 0xE0FFFF,
        ["lightgoldenrodyellow"] = 0xFAFAD2,
        ["lightgray"] = 0xD3D3D3,
        ["lightgreen"] = 0x90EE90,
        ["lightgrey"] = 0xD3D3D3,
        ["lightpink"] = 0xFFB6C1,
        ["lightsalmon"] = 0xFFA07A,
        ["lightseagreen"] = 0x20B2AA,
        ["lightskyblue"] = 0x87CEFA,
        ["lightslategray"] = 0x778899,
        ["lightslategrey"] = 0x778899,
        ["lightsteelblue"] = 0xB0C4DE,
        ["lightyellow"] = 0xFFFFE0,
        ["lime"] = 0x00FF00,
        ["limegreen"] = 0x32CD32,
        ["linen"] = 0xFAF0E6,
        ["magenta"] = 0xFF00FF,
        ["maroon"] = 0x800000,
        ["mediumaquamarine"] = 0x66CDAA,
        ["mediumblue"] = 0x0000CD,
        ["mediumorchid"] = 0xBA55D3,
        ["mediumpurple"] = 0x9370DB,
        ["mediumseagreen"] = 0x3CB371,
        ["mediumslateblue"] = 0x7B68EE,
        ["mediumspringgreen"] = 0x00FA9A,
        ["mediumturquoise"] = 0x48D1CC,
        ["mediumvioletred"] = 0xC71585,
        ["midnightblue"] = 0x191970,
        ["mintcream"] = 0xF5FFFA,
        ["mistyrose"] = 0xFFE4E1,
        ["moccasin"] = 0xFFE4B5,
        ["navajowhite"] = 0xFFDEAD,
        ["navy"] = 0x000080,
        ["oldlace"] = 0xFDF5E6,
        ["olive"] = 0x808000,
        ["olivedrab"] = 0x6B8E23,
        ["orange"] = 0xFFA500,
        ["orangered"] = 0xFF4500,
        ["orchid"] = 0xDA70D6,
        ["palegoldenrod"] = 0xEEE8AA,
        ["palegreen"] = 0x98FB98,
        ["paleturquoise"] = 0xAFEEEE,
        ["palevioletred"] = 0xDB7093,
        ["papayawhip"] = 0xFFEFD5,
        ["peachpuff"] = 0xFFDAB9,
        ["peru"] = 0xCD853F,
        ["pink"] = 0xFFC0CB,
        ["plum"] = 0xDDA0DD,
        ["powderblue"] = 0xB0E0E6,
        ["purple"] = 0x800080,
        ["rebeccapurple"] = 0x663399,
        ["red"] = 0xFF0000,
        ["rosybrown"] = 0xBC8F8F,
        ["royalblue"] = 0x4169E1,
        ["saddlebrown"] = 0x8B4513,
        ["salmon"] = 0xFA8072,
        ["sandybrown"] = 0xF4A460,
        ["seagreen"] = 0x2E8B57,
        ["seashell"] = 0xFFF5EE,
        ["sienna"] = 0xA0522D,
        ["silver"] = 0xC0C0C0,
        ["skyblue"] = 0x87CEEB,
        ["slateblue"] = 0x6A5ACD,
        ["slategray"] = 0x708090,
        ["slategrey"] = 0x708090,
        ["snow"] = 0xFFFAFA,
        ["springgreen"] = 0x00FF7F,
        ["steelblue"] = 0x4682B4,
        ["tan"] = 0xD2B48C,
        ["teal"] = 0x008080,
        ["thistle"] = 0xD8BFD8,
        ["tomato"] = 0xFF6347,
        ["turquoise"] = 0x40E0D0,
        ["violet"] = 0xEE82EE,
        ["wheat"] = 0xF5DEB3,
        ["white"] = 0xFFFFFF,
        ["whitesmoke"] = 0xF5F5F5,
        ["yellow"] = 0xFFFF00,
        ["yellowgreen"] = 0x9ACD32,
    };
}
