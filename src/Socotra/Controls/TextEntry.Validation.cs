using System.Globalization;
using System.Text.RegularExpressions;

namespace Socotra;

public partial class TextEntry
{
    /// <summary>The fewest characters the text should have. Shorter text sets <see cref="HasValidationErrors"/>.</summary>
    public int? MinLength { get; set; }

    /// <summary>The most characters the text can have. Typing and pasting stop at it.</summary>
    public int? MaxLength { get; set; }

    /// <summary>A regular expression each typed character has to match, like <c>[a-z]</c>. Other characters can't be typed.</summary>
    public string? CharacterRegex { get; set; }

    /// <summary>A regular expression the whole text has to match. Text that doesn't sets <see cref="HasValidationErrors"/>.</summary>
    public string? StringRegex { get; set; }

    /// <summary>Only lets numbers be typed, and tidies the text with <see cref="FixNumeric"/> when the entry loses focus.</summary>
    public bool Numeric { get; set; }

    /// <summary>With <see cref="Numeric"/>, only lets whole numbers be typed.</summary>
    public bool WholeNumbers { get; set; }

    /// <summary>Whether the text breaks one of the rules above. The entry has the <c>invalid</c> class while it does.</summary>
    public bool HasValidationErrors { get; set; }

    /// <summary>Checks the text against <see cref="MinLength"/>, <see cref="MaxLength"/>, <see cref="StringRegex"/> and <see cref="CharacterRegex"/>, and updates <see cref="HasValidationErrors"/>.</summary>
    public void UpdateValidation()
    {
        HasValidationErrors = TextLength < MinLength || TextLength > MaxLength;
        if (StringRegex is not null)
        {
            HasValidationErrors |= !Regex.IsMatch(Text, StringRegex);
        }

        if (CharacterRegex is not null)
        {
            foreach (var rune in Text.EnumerateRunes())
            {
                HasValidationErrors |= !Regex.IsMatch(rune.ToString(), CharacterRegex);
            }
        }

        SetClass("invalid", HasValidationErrors);
    }

    /// <summary>Called for each character the user types or pastes. Return false to keep it out. By default it applies <see cref="CharacterRegex"/>, <see cref="Multiline"/> and <see cref="Numeric"/>.</summary>
    /// <param name="c">The typed character.</param>
    public virtual bool CanEnterCharacter(char c)
    {
        if (CharacterRegex is not null && !Regex.IsMatch(c.ToString(), CharacterRegex))
        {
            return false;
        }

        if (!Multiline && c is '\n' or '\r')
        {
            return false;
        }

        if (!Numeric)
        {
            return true;
        }

        var context = Text;
        if (Label.HasSelection())
        {
            var indices = StringInfo.ParseCombiningCharacters(context);
            var start = Math.Min(Label.SelectionStart, Label.SelectionEnd);
            var end = Math.Max(Label.SelectionStart, Label.SelectionEnd);
            var from = start < indices.Length ? indices[start] : context.Length;
            var to = end < indices.Length ? indices[end] : context.Length;
            context = context.Remove(from, to - from);
        }

        return CanEnterNumericCharacter(c, context);
    }

    private bool CanEnterNumericCharacter(char c, string context)
    {
        if (char.IsDigit(c))
        {
            return true;
        }

        if (c is '.' or ',')
        {
            return !WholeNumbers && !context.Contains('.') && !context.Contains(',');
        }

        return c == '-' && !context.Contains('-') && (MinValue is null || MinValue < 0);
    }
}
