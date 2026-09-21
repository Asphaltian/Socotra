using System.Globalization;

namespace Socotra;

internal struct PickerColor(float hue, float saturation, float value, float alpha, float brightness = 1)
{
    public float Hue = hue;

    public float Saturation = saturation;

    public float Value = value;

    public float Alpha = alpha;

    public float Brightness = brightness;

    public readonly Color BaseColor => Color.FromHsv(Hue, Saturation, Value, Alpha);

    public static PickerColor FromColor(Color color, float hueIfGrey = 0)
    {
        var brightness = 1.0f;
        var baseColor = color;
        var linear = color.ToLinear();
        var max = MathF.Max(linear.R, MathF.Max(linear.G, linear.B));
        if (max > 1)
        {
            brightness = max;
            baseColor = new Color(linear.R / max, linear.G / max, linear.B / max, color.A).ToSrgb();
        }

        var (hue, saturation, value) = baseColor.ToHsv();
        if (saturation <= 0 || value <= 0)
        {
            hue = hueIfGrey;
        }

        return new PickerColor(hue, saturation, value, baseColor.A, brightness);
    }

    public static bool TryParse(string? text, float hueIfGrey, out PickerColor result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var star = text.LastIndexOf('*');
        if (star >= 0
            && Translation.TryParseTypedNumber(text[(star + 1)..], out var brightness)
            && brightness >= 1
            && Color.Parse(text[..star]) is { } baseColor
            && baseColor.R <= 1 && baseColor.G <= 1 && baseColor.B <= 1)
        {
            result = FromColor(baseColor, hueIfGrey);
            result.Brightness = brightness;
            return true;
        }

        if (Color.Parse(text) is not { } color)
        {
            return false;
        }

        result = FromColor(color, hueIfGrey);
        return true;
    }

    public readonly Color ToColor() => Brightness == 1 ? BaseColor : BaseColor.ScaleBrightness(Brightness);

    public readonly PickerColor Limit(bool hasAlpha, bool isHdr)
    {
        var limited = this;
        if (!hasAlpha)
        {
            limited.Alpha = 1;
        }

        if (!isHdr)
        {
            limited.Brightness = 1;
        }

        return limited;
    }

    public readonly string ToText()
    {
        var text = BaseColor.ToHex();
        return Brightness == 1 ? text : $"{text} * {Brightness.ToString("0.##", CultureInfo.InvariantCulture)}";
    }
}
