namespace Socotra;

/// <summary>A CSS length, like <c>10px</c>, <c>50%</c>, <c>2em</c> or <c>calc(100% - 20px)</c>. Use it for sizes, positions and spacing in <see cref="Styles"/>.</summary>
/// <example><code>
/// // A plain number is pixels
/// panel.Style.Width = 200;
///
/// // Half the parent's height, and 2em of padding
/// panel.Style.Height = Length.Percent(50);
/// panel.Style.PaddingLeft = Length.Em(2);
///
/// // Anything you'd write in a stylesheet
/// panel.Style.MaxWidth = Length.Parse("calc(100% - 20px)");
/// </code></example>
public readonly struct Length : IEquatable<Length>
{
    internal static Vector2 RootSize;
    internal const float InitialFontSize = 13;
    internal static float RootFontSize = InitialFontSize;
    internal static float CurrentFontSize = InitialFontSize;
    internal static float RootScale = 1;

    private readonly Calc? _expression;

    /// <summary>The number, in the length's <see cref="Unit"/>.</summary>
    public float Value { get; init; }

    /// <summary>What <see cref="Value"/> is measured in.</summary>
    public LengthUnit Unit { get; init; }

    private Length(Calc expression)
    {
        _expression = expression;
        Unit = LengthUnit.Expression;
    }

    /// <summary><c>auto</c>, which lets layout pick the size.</summary>
    public static Length Auto => new() { Unit = LengthUnit.Auto };

    /// <summary>No value at all, as opposed to zero.</summary>
    public static Length Undefined => new() { Unit = LengthUnit.Undefined };

    /// <summary><c>cover</c>, for background sizes.</summary>
    public static Length Cover => new() { Unit = LengthUnit.Cover };

    /// <summary><c>contain</c>, for background sizes.</summary>
    public static Length Contain => new() { Unit = LengthUnit.Contain };

    /// <summary>A length of <paramref name="pixels"/> pixels.</summary>
    public static Length Pixels(float pixels) => new() { Value = pixels, Unit = LengthUnit.Pixels };

    /// <summary>A percentage, usually of the parent's size. Pass 50 for 50%.</summary>
    public static Length Percent(float percent) => new() { Value = percent, Unit = LengthUnit.Percentage };

    /// <summary>A percentage written as a fraction. Pass 0.5 for 50%.</summary>
    public static Length Fraction(float fraction) => Percent(fraction * 100);

    /// <summary>A percentage of the root panel's height.</summary>
    public static Length ViewHeight(float percent) => new() { Value = percent, Unit = LengthUnit.ViewHeight };

    /// <summary>A percentage of the root panel's width.</summary>
    public static Length ViewWidth(float percent) => new() { Value = percent, Unit = LengthUnit.ViewWidth };

    /// <summary>A percentage of the root panel's shorter side.</summary>
    public static Length ViewMin(float percent) => new() { Value = percent, Unit = LengthUnit.ViewMin };

    /// <summary>A percentage of the root panel's longer side.</summary>
    public static Length ViewMax(float percent) => new() { Value = percent, Unit = LengthUnit.ViewMax };

    /// <summary>A multiple of the root panel's font size.</summary>
    public static Length Rem(float value) => new() { Value = value, Unit = LengthUnit.RootEm };

    /// <summary>A multiple of the panel's own font size.</summary>
    public static Length Em(float value) => new() { Value = value, Unit = LengthUnit.Em };

    /// <summary>Lets you pass a plain number of pixels wherever a length is wanted.</summary>
    public static implicit operator Length(float pixels) => Pixels(pixels);

    /// <summary>How many pixels this length comes to. Percentages are of <paramref name="dimension"/>.</summary>
    public float GetPixels(float dimension) => Unit switch
    {
        LengthUnit.Pixels => Value,
        LengthUnit.Percentage => dimension * (Value / 100.0f),
        LengthUnit.ViewWidth => RootSize.X * (Value / 100.0f),
        LengthUnit.ViewHeight => RootSize.Y * (Value / 100.0f),
        LengthUnit.ViewMin => MathF.Min(RootSize.X, RootSize.Y) * (Value / 100.0f),
        LengthUnit.ViewMax => MathF.Max(RootSize.X, RootSize.Y) * (Value / 100.0f),
        LengthUnit.RootEm => RootFontSize * Value,
        LengthUnit.Em => CurrentFontSize * Value,
        LengthUnit.Expression => _expression!.Evaluate(dimension),
        _ => Value,
    };

    /// <summary>Like <see cref="GetPixels(float)"/>, but <c>start</c>, <c>end</c> and <c>center</c> place <paramref name="contentSize"/> inside <paramref name="dimension"/>.</summary>
    public float GetPixels(float dimension, float contentSize) => Unit switch
    {
        LengthUnit.Start => 0,
        LengthUnit.End => dimension - contentSize,
        LengthUnit.Center => (dimension - contentSize) * 0.5f,
        _ => GetPixels(dimension),
    };

    internal float GetScaledPixels(float dimension) => Unit is LengthUnit.Pixels or LengthUnit.RootEm
        ? GetPixels(dimension) * RootScale
        : GetPixels(dimension);

    internal float GetFraction() => GetPixels(1);

    /// <summary>Reads a length written the way you'd write it in a stylesheet, like <c>10px</c> or <c>calc(100% - 20px)</c>. Returns null if <paramref name="value"/> isn't a length.</summary>
    public static Length? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        value = value.Trim();
        switch (value)
        {
            case "auto": return Auto;
            case "cover": return Cover;
            case "contain": return Contain;
            case "center": return new Length { Unit = LengthUnit.Center };
            case "left" or "top": return new Length { Unit = LengthUnit.Start };
            case "right" or "bottom": return new Length { Unit = LengthUnit.End };
            case "from": return Percent(0);
            case "to": return Percent(100);
        }

        if (Calc.IsExpression(value))
        {
            return Calc.Parse(value) is { } expression ? new Length(expression) : null;
        }

        int end = 0;
        while (end < value.Length && (char.IsDigit(value[end]) || value[end] is '.' or '-' or '+'))
        {
            end++;
        }

        if (!Translation.TryParseFloat(value.AsSpan(0, end), out var number))
        {
            return Translation.TryParseFloat(value, out var whole) ? Pixels(whole) : null;
        }

        var rest = value.AsSpan(end).TrimStart();
        int unitLength = 0;
        while (unitLength < rest.Length && (char.IsLetter(rest[unitLength]) || rest[unitLength] == '%'))
        {
            unitLength++;
        }

        return rest[..unitLength].ToString().ToLowerInvariant() switch
        {
            "" or "px" or "deg" => Pixels(number),
            "%" => Percent(number),
            "vh" or "dvh" or "svh" or "lvh" => ViewHeight(number),
            "vw" or "dvw" or "svw" or "lvw" => ViewWidth(number),
            "vmin" => ViewMin(number),
            "vmax" => ViewMax(number),
            "rem" => Rem(number),
            "em" => Em(number),
            _ => null,
        };
    }

    internal static Length Lerp(Length a, Length b, float delta) =>
        a.Unit == b.Unit && a.Unit != LengthUnit.Expression ? a with { Value = a.Value + ((b.Value - a.Value) * delta) } : b;

    internal Length Scaled(float amount, bool round = true) => Unit switch
    {
        LengthUnit.Pixels => this with { Value = round ? MathF.Ceiling(Value * amount) : Value * amount },
        LengthUnit.RootEm or LengthUnit.Em => this with { Value = Value * amount },
        _ => this,
    };

    /// <inheritdoc/>
    public bool Equals(Length other) => Value.Equals(other.Value) && Unit == other.Unit && _expression?.ToString() == other._expression?.ToString();

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Length other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Value, Unit, _expression?.ToString());

    /// <inheritdoc/>
    public static bool operator ==(Length left, Length right) => left.Equals(right);

    /// <inheritdoc/>
    public static bool operator !=(Length left, Length right) => !left.Equals(right);

    /// <inheritdoc/>
    public override string ToString() => Unit switch
    {
        LengthUnit.Pixels => FormattableString.Invariant($"{Value}px"),
        LengthUnit.Percentage => FormattableString.Invariant($"{Value}%"),
        LengthUnit.RootEm => FormattableString.Invariant($"{Value}rem"),
        LengthUnit.Em => FormattableString.Invariant($"{Value}em"),
        LengthUnit.ViewWidth => FormattableString.Invariant($"{Value}vw"),
        LengthUnit.ViewHeight => FormattableString.Invariant($"{Value}vh"),
        LengthUnit.ViewMin => FormattableString.Invariant($"{Value}vmin"),
        LengthUnit.ViewMax => FormattableString.Invariant($"{Value}vmax"),
        LengthUnit.Expression => _expression!.ToString(),
        _ => Unit.ToString().ToLowerInvariant(),
    };
}

/// <summary>What a <see cref="Length"/> is measured in.</summary>
public enum LengthUnit : byte
{
    /// <summary><c>auto</c>: layout picks the size.</summary>
    Auto,

    /// <summary>Pixels.</summary>
    Pixels,

    /// <summary>A percentage, usually of the parent's size.</summary>
    Percentage,

    /// <summary>A percentage of the root panel's height: <c>vh</c>, <c>dvh</c>, <c>svh</c> or <c>lvh</c>.</summary>
    ViewHeight,

    /// <summary>A percentage of the root panel's width: <c>vw</c>, <c>dvw</c>, <c>svw</c> or <c>lvw</c>.</summary>
    ViewWidth,

    /// <summary>A percentage of the root panel's shorter side.</summary>
    ViewMin,

    /// <summary>A percentage of the root panel's longer side.</summary>
    ViewMax,

    /// <summary>The start of the axis: <c>left</c> or <c>top</c>.</summary>
    Start,

    /// <summary><c>cover</c>: the image fills the panel, cropped if it has to be.</summary>
    Cover,

    /// <summary><c>contain</c>: the whole image fits inside the panel.</summary>
    Contain,

    /// <summary>The end of the axis: <c>right</c> or <c>bottom</c>.</summary>
    End,

    /// <summary>The middle of the axis.</summary>
    Center,

    /// <summary>No value at all, as opposed to zero.</summary>
    Undefined,

    /// <summary>A <c>calc()</c>, <c>min()</c>, <c>max()</c> or <c>clamp()</c>.</summary>
    Expression,

    /// <summary>A multiple of the root panel's font size.</summary>
    RootEm,

    /// <summary>A multiple of the panel's own font size.</summary>
    Em,
}

internal static class LengthUnitExtensions
{
    extension(LengthUnit unit)
    {
        public bool IsDynamic() => unit is LengthUnit.ViewWidth or LengthUnit.ViewHeight or LengthUnit.ViewMin or LengthUnit.ViewMax
            or LengthUnit.Expression or LengthUnit.RootEm or LengthUnit.Em;
    }
}
