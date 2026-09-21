namespace Socotra;

/// <summary>
/// A <see cref="TextEntry"/> for numbers. Only numbers can be typed, and dragging the <see cref="TextEntry.Prefix"/> label
/// sideways changes the value, in finer steps while the right mouse button is held too.
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
