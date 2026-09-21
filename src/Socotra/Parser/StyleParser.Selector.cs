using System.Globalization;

namespace Socotra;

internal static partial class StyleParser
{
    public static List<StyleSelector> Selector(string text, StyleBlock? parent)
    {
        var selectors = new List<StyleSelector>();
        var p = new Parse(text);
        while (!(p = p.SkipWhitespaceAndNewlines()).IsEnd)
        {
            var group = p.ReadUntilOrEnd(",", respectParens: true).Trim();
            p.Pointer++;
            if (group.Length == 0)
            {
                throw new FormatException($"Empty selector in \"{text}\"");
            }

            selectors.Add(ParseSelector(group, parent));
        }

        return selectors;
    }

    private static StyleSelector ParseSelector(string text, StyleBlock? parent)
    {
        StyleSelector? last = null;
        var p = new Parse(text);
        while (!(p = p.SkipWhitespaceAndNewlines()).IsEnd)
        {
            var combinator = p.Current;
            if (combinator is '>' or '+' or '~')
            {
                p.Pointer++;
                p = p.SkipWhitespaceAndNewlines();
            }

            var rule = ParseCompound(p.ReadUntilWhitespaceOrNewlineOrEndAndObeyBrackets(), last is null ? parent : null);
            rule.Parent = last;
            rule.ImmediateParent = combinator == '>';
            rule.AdjacentSibling = combinator == '+';
            rule.GeneralSibling = combinator == '~';
            last = rule;
        }

        if (last is null)
        {
            throw new FormatException($"Empty selector \"{text}\"");
        }

        last.AsString = text;
        return last;
    }

    private static StyleSelector ParseCompound(string text, StyleBlock? parent)
    {
        var rule = new StyleSelector { AsString = text };
        var p = new Parse(text);
        if (p.Current == '&')
        {
            p.Pointer++;
            rule.AnyOf = parent?.Selectors ?? throw new FormatException($"\"{text}\" starts with & outside a block");
        }
        else if (parent is not null)
        {
            rule.DescendantOf = parent.Selectors;
        }

        var classes = new List<string>();
        while (!p.IsEnd)
        {
            switch (p.Current)
            {
                case '.':
                    p.Pointer++;
                    classes.Add(ReadName(ref p, text).ToLowerInvariant());
                    break;
                case '#':
                    p.Pointer++;
                    rule.Id = ReadName(ref p, text);
                    break;
                case ':':
                    while (p.Current == ':')
                    {
                        p.Pointer++;
                    }

                    ReadPseudoClass(rule, ref p, text);
                    break;
                case '*':
                    p.Pointer++;
                    break;
                default:
                    rule.Element = ReadName(ref p, text).ToLowerInvariant();
                    break;
            }
        }

        if (classes.Count > 0)
        {
            rule.Classes = [.. classes];
        }

        return rule;
    }

    private static string ReadName(ref Parse p, string text)
    {
        var name = p.ReadUntilOrEnd(".:#");
        return name.Length > 0 ? name : throw new FormatException($"Invalid selector \"{text}\"");
    }

    private static void ReadPseudoClass(StyleSelector rule, ref Parse p, string text)
    {
        if (p.Is("not(", ignoreCase: true))
        {
            p.Pointer += 3;
            rule.Not = ParseSelector(p.ReadInnerBrackets() ?? throw new FormatException($"Unclosed :not in \"{text}\""), null);
            return;
        }

        if (p.Is("has(", ignoreCase: true))
        {
            p.Pointer += 3;
            rule.Has = [.. (p.ReadInnerBrackets() ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(ParseRelative)];
            return;
        }

        if (p.Is("nth-child(", ignoreCase: true))
        {
            p.Pointer += "nth-child".Length;
            rule.NthChild = ParseNthChild((p.ReadInnerBrackets() ?? "").Trim(), text);
            return;
        }

        var name = p.ReadUntilOrEnd(".:#").ToLowerInvariant();
        rule.Flags |= name switch
        {
            "hover" => PseudoClass.Hover,
            "active" => PseudoClass.Active,
            "focus" => PseudoClass.Focus,
            "focus-visible" => PseudoClass.FocusVisible,
            "intro" => PseudoClass.Intro,
            "outro" => PseudoClass.Outro,
            "empty" => PseudoClass.Empty,
            "first-child" => PseudoClass.FirstChild,
            "last-child" => PseudoClass.LastChild,
            "only-child" => PseudoClass.OnlyChild,
            "disabled" => PseudoClass.Disabled,
            "before" => PseudoClass.Before,
            "after" => PseudoClass.After,
            _ => throw new FormatException($"Unsupported pseudo-class :{name} in \"{text}\""),
        };
    }

    private static StyleSelector ParseRelative(string text)
    {
        var combinator = text[0];
        var selector = ParseSelector(combinator is '>' or '+' or '~' ? text[1..].Trim() : text, null);
        var first = selector;
        while (first.Parent is not null)
        {
            first = first.Parent;
        }

        first.Parent = new StyleSelector { IsScope = true };
        first.ImmediateParent = combinator == '>';
        first.AdjacentSibling = combinator == '+';
        first.GeneralSibling = combinator == '~';
        return selector;
    }

    private static Func<IStyleTarget, bool> ParseNthChild(string argument, string text)
    {
        if (int.TryParse(argument, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
        {
            return panel => panel.SiblingIndex + 1 == index;
        }

        return argument.ToLowerInvariant() switch
        {
            "odd" => panel => panel.SiblingIndex % 2 == 0,
            "even" => panel => panel.SiblingIndex % 2 == 1,
            _ => throw new FormatException($"Unsupported :nth-child({argument}) in \"{text}\""),
        };
    }
}
