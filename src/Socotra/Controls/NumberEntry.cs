namespace Socotra;

/// <summary>
/// <c>NumberEntry</c> is a <see cref="TextEntry"/> for numbers, so only numbers can be typed into it. Dragging the
/// <see cref="TextEntry.Prefix"/> label sideways changes the value too, and holding the right mouse button while you
/// drag makes the steps finer.
/// </summary>
/// <example>
/// <code>
/// // A volume setting from 0 to 100 that can be typed or dragged
/// var volume = new NumberEntry { Parent = row, Prefix = "Volume", MinValue = 0, MaxValue = 100, WholeNumbers = true };
/// </code>
/// </example>
public partial class NumberEntry : TextEntry
{
    /// <summary>Makes an empty number entry that shows up to three decimal places.</summary>
    public NumberEntry()
    {
        Numeric = true;
        NumberFormat = "0.###";
    }
}
