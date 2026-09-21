using System.Globalization;

namespace Socotra;

internal static class Translation
{
    public static bool TryConvert(object? from, Type targetType, out object? convertedValue)
    {
        convertedValue = null;
        if (from is null)
        {
            return true;
        }

        targetType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        var fromType = from.GetType();
        if (fromType.IsAssignableTo(targetType))
        {
            convertedValue = from;
            return true;
        }

        if (targetType == typeof(string))
        {
            convertedValue = FormattableString.Invariant($"{from}");
            return true;
        }

        if (targetType == typeof(bool))
        {
            convertedValue = ToBool($"{from}");
            return true;
        }

        if (targetType.IsEnum)
        {
            return Enum.TryParse(targetType, from.ToString(), ignoreCase: true, out convertedValue);
        }

        if (targetType.GetMethod("op_Implicit", [fromType]) is { } implicitOperator)
        {
            convertedValue = implicitOperator.Invoke(null, [from]);
            return true;
        }

        if (from is IConvertible)
        {
            try
            {
                convertedValue = Convert.ChangeType(from, targetType, CultureInfo.InvariantCulture);
                return convertedValue is not null;
            }
            catch (Exception e) when (e is FormatException or InvalidCastException or OverflowException)
            {
                return false;
            }
        }

        return false;
    }

    public static bool TryParseFloat(ReadOnlySpan<char> text, out float value) =>
        float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    public static bool TryParseTypedNumber(string text, out float value) => TryParseFloat(text.Replace(',', '.'), out value);

    public static bool ToBool(string? str)
    {
        if (string.IsNullOrEmpty(str) || str == "0")
        {
            return false;
        }

        if (char.IsDigit(str[0]) && str[0] != '0')
        {
            return true;
        }

        if (str.Equals("false", StringComparison.OrdinalIgnoreCase)
            || str.Equals("no", StringComparison.OrdinalIgnoreCase)
            || str.Equals("null", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !TryParseFloat(str, out var f) || f != 0;
    }
}
