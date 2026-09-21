using System.Text;
using System.Text.RegularExpressions;

namespace Socotra;

internal sealed record MixinParameter(string Name, string? DefaultValue, bool IsVariadic);

internal sealed partial class MixinDefinition(string name, IReadOnlyList<MixinParameter> parameters, string content)
{
    public string Name => name;

    public IReadOnlyList<MixinParameter> Parameters => parameters;

    public string Expand(Dictionary<string, string> arguments, string? contentBlock)
    {
        var result = content;
        foreach (var parameter in parameters)
        {
            var value = arguments.GetValueOrDefault(parameter.Name) ?? parameter.DefaultValue
                ?? throw new FormatException($"Missing ${parameter.Name} for mixin {name}");
            var dots = parameter.IsVariadic ? @"(\.\.\.)?" : "";
            result = Regex.Replace(result, $@"\${Regex.Escape(parameter.Name)}{dots}(?![\w-])", _ => value);
        }

        return ContentPattern().Replace(result, contentBlock ?? "");
    }

    [GeneratedRegex(@"@content(\s*;)?", RegexOptions.IgnoreCase)]
    private static partial Regex ContentPattern();
}

internal static partial class StyleParser
{
    private static bool ParseMixinDefinition(ref Parse p, StyleSheet sheet)
    {
        if (!p.TrySkip("@mixin", ignoreCase: true))
        {
            return false;
        }

        p = p.SkipWhitespaceAndNewlines();
        var name = p.ReadUntilOrEnd("({").Trim();
        if (name.Length == 0)
        {
            throw new FormatException($"Expected a name after @mixin {p.FileAndLine}");
        }

        List<MixinParameter> parameters = [];
        if ((p = p.SkipWhitespaceAndNewlines()).Current == '(')
        {
            parameters = ParseMixinParameters(p.ReadInnerBrackets() ?? "", p.FileAndLine);
        }

        if ((p = p.SkipWhitespaceAndNewlines()).Current != '{')
        {
            throw new FormatException($"Expected {{ after @mixin {name} {p.FileAndLine}");
        }

        sheet.Mixins[name] = new MixinDefinition(name, parameters, ReadBracedContent(ref p));
        return true;
    }

    private static List<MixinParameter> ParseMixinParameters(string content, string fileAndLine)
    {
        var parameters = new List<MixinParameter>();
        foreach (var part in SplitArguments(content))
        {
            if (parameters is [.., { IsVariadic: true }])
            {
                throw new FormatException($"Only the last mixin parameter can take the rest {fileAndLine}");
            }

            if (!part.StartsWith('$'))
            {
                throw new FormatException($"Mixin parameters start with $: '{part}' {fileAndLine}");
            }

            bool variadic = part.EndsWith("...");
            var text = variadic ? part[1..^3] : part[1..];
            int colon = text.IndexOf(':');
            parameters.Add(colon < 0
                ? new MixinParameter(text.Trim(), variadic ? "" : null, variadic)
                : new MixinParameter(text[..colon].Trim(), text[(colon + 1)..].Trim(), variadic));
        }

        return parameters;
    }

    private static string ExpandInclude(string include, string? contentBlock, StyleSheet sheet, string fileAndLine)
    {
        var p = new Parse(include.Trim());
        p.TrySkip("@include", ignoreCase: true);
        p = p.SkipWhitespaceAndNewlines();
        var name = p.ReadUntilOrEnd("(;{").Trim();
        if (!sheet.Mixins.TryGetValue(name, out var mixin))
        {
            throw new FormatException($"Unknown mixin '{name}' {fileAndLine}");
        }

        var arguments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if ((p = p.SkipWhitespaceAndNewlines()).Current == '(')
        {
            var rest = new List<string>();
            int position = 0;
            bool named = false;
            foreach (var argument in SplitArguments(p.ReadInnerBrackets() ?? ""))
            {
                int colon = argument.IndexOf(':');
                if (argument.StartsWith('$') && colon > 0)
                {
                    named = true;
                    arguments[argument[1..colon].Trim()] = sheet.ReplaceVariables(argument[(colon + 1)..].Trim());
                }
                else if (named)
                {
                    throw new FormatException($"A positional argument can't follow a named one in {name} {fileAndLine}");
                }
                else if (position < mixin.Parameters.Count && !mixin.Parameters[position].IsVariadic)
                {
                    arguments[mixin.Parameters[position++].Name] = sheet.ReplaceVariables(argument);
                }
                else if (mixin.Parameters is [.., { IsVariadic: true }])
                {
                    rest.Add(sheet.ReplaceVariables(argument));
                }
                else
                {
                    throw new FormatException($"Too many arguments for mixin {name} {fileAndLine}");
                }
            }

            if (rest.Count > 0)
            {
                arguments[mixin.Parameters[^1].Name] = string.Join(", ", rest);
            }
        }

        return mixin.Expand(arguments, contentBlock);
    }

    private static List<string> SplitArguments(string content)
    {
        var parts = new List<string>();
        var current = new StringBuilder();
        int depth = 0;
        foreach (var c in content)
        {
            if (c == ',' && depth == 0)
            {
                parts.Add(current.ToString().Trim());
                current.Clear();
                continue;
            }

            depth += c == '(' ? 1 : c == ')' ? -1 : 0;
            current.Append(c);
        }

        if (current.ToString().Trim() is { Length: > 0 } last)
        {
            parts.Add(last);
        }

        return parts;
    }
}
