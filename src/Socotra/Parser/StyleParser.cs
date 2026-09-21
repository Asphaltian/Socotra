using System.Text;
using System.Text.RegularExpressions;

namespace Socotra;

internal static partial class StyleParser
{
    private const int MaxImportDepth = 16;

    public static StyleSheet ParseFile(string fullPath, IEnumerable<(string Key, string Value)>? variables) =>
        ParseString(File.ReadAllText(fullPath), fullPath, variables);

    public static StyleSheet ParseString(string content, string fileName, IEnumerable<(string Key, string Value)>? variables)
    {
        var sheet = new StyleSheet(fileName);
        foreach (var (key, value) in variables ?? [])
        {
            sheet.SetVariable(key, value);
        }

        ParseInto(sheet, content, fileName, 0);
        sheet.BuildIndex();
        return sheet;
    }

    public static string GetPropertyFromAlias(string name) => name switch
    {
        "color" => "font-color",
        "background-image-tint" => "background-tint",
        "inset-block-start" => "top",
        "inset-block-end" => "bottom",
        "inset-inline-start" => "left",
        "inset-inline-end" => "right",
        "margin-block-start" => "margin-top",
        "margin-block-end" => "margin-bottom",
        "margin-inline-start" => "margin-left",
        "margin-inline-end" => "margin-right",
        "padding-block-start" => "padding-top",
        "padding-block-end" => "padding-bottom",
        "padding-inline-start" => "padding-left",
        "padding-inline-end" => "padding-right",
        _ => name,
    };

    private static void ParseInto(StyleSheet sheet, string content, string fileName, int depth)
    {
        if (depth > MaxImportDepth)
        {
            throw new FormatException($"Imports nest too deeply in {fileName}");
        }

        content = StripComments(content);

        var p = new Parse(content, fileName);
        while (!(p = p.SkipWhitespaceAndNewlines()).IsEnd)
        {
            int start = p.Pointer;
            int blocksBefore = sheet.Blocks.Count;
            try
            {
                if (ParseVariable(ref p, sheet) || ParseKeyFrames(ref p, sheet) || ParseMixinDefinition(ref p, sheet) || ParseImport(ref p, sheet, fileName, depth)
                    || ParseTopLevelInclude(ref p, sheet))
                {
                    continue;
                }

                if (p.Current == '@')
                {
                    throw new FormatException($"Unknown rule {p.ReadWord(null, readUntilEnd: true)} {p.FileAndLine}");
                }

                var selector = p.ReadUntilOrEnd("{;$@");
                if (p.Current != '{')
                {
                    throw new FormatException($"Expected {{ after selector {p.FileAndLine}");
                }

                ReadStyleBlock(ref p, selector, sheet, null);
            }
            catch (FormatException e)
            {
                sheet.Blocks.RemoveRange(blocksBefore, sheet.Blocks.Count - blocksBefore);
                p.Pointer = SkipMalformedBlock(content, start);
                Log.Warning($"Skipped a rule in {fileName}: {e.Message}");
            }
        }
    }

    public static string StripComments(string text)
    {
        var builder = new StringBuilder(text.Length);
        char quote = '\0';
        for (int i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quote != '\0')
            {
                builder.Append(c);
                if (c == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            if (c is '"' or '\'')
            {
                quote = c;
                builder.Append(c);
            }
            else if (c == '/' && i + 1 < text.Length && text[i + 1] == '*')
            {
                var end = text.IndexOf("*/", i + 2, StringComparison.Ordinal);
                if (end < 0)
                {
                    throw new FormatException("Unterminated comment");
                }

                builder.Append('\n', text.AsSpan(i, end - i).Count('\n'));
                i = end + 1;
            }
            else if (c == '/' && i + 1 < text.Length && text[i + 1] == '/' && (i == 0 || text[i - 1] != ':'))
            {
                while (i + 1 < text.Length && text[i + 1] != '\n')
                {
                    i++;
                }
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    private static int SkipMalformedBlock(string text, int start)
    {
        int i = start;
        char quote = '\0';
        for (; i < text.Length; i++)
        {
            var c = text[i];
            if (quote != '\0')
            {
                quote = c == quote ? '\0' : quote;
            }
            else if (c is '"' or '\'')
            {
                quote = c;
            }
            else if (c == ';')
            {
                return i + 1;
            }
            else if (c == '{')
            {
                break;
            }
        }

        int depth = 0;
        for (; i < text.Length; i++)
        {
            var c = text[i];
            if (quote != '\0')
            {
                quote = c == quote ? '\0' : quote;
            }
            else if (c is '"' or '\'')
            {
                quote = c;
            }
            else if (c == '{')
            {
                depth++;
            }
            else if (c == '}' && --depth == 0)
            {
                return i + 1;
            }
        }

        return text.Length;
    }

    private static bool ParseKeyFrames(ref Parse p, StyleSheet sheet)
    {
        if (KeyFrames.Parse(ref p, sheet) is not { } frames)
        {
            return false;
        }

        sheet.KeyFrames[frames.Name] = frames;
        return true;
    }

    public static void ParseDeclarationBlock(ref Parse p, Styles styles, StyleSheet sheet)
    {
        p = p.SkipWhitespaceAndNewlines();
        if (!p.TrySkip("{"))
        {
            throw new FormatException($"Expected {{ {p.FileAndLine}");
        }

        while (!(p = p.SkipWhitespaceAndNewlines(";")).IsEnd)
        {
            if (p.TrySkip("}"))
            {
                return;
            }

            var declaration = p.ReadUntilOrEnd(";}", respectParens: true);
            SetDeclaration(styles, declaration, sheet, p);
        }

        throw new FormatException($"Expected }} {p.FileAndLine}");
    }

    private static bool ParseVariable(ref Parse p, StyleSheet sheet)
    {
        if (p.Current != '$')
        {
            return false;
        }

        var (key, value) = p.ReadKeyValue();
        bool isDefault = value.EndsWith("!default", StringComparison.OrdinalIgnoreCase);
        sheet.SetVariable(key, isDefault ? value[..^"!default".Length].Trim() : value, isDefault);
        return true;
    }

    private static bool ParseImport(ref Parse p, StyleSheet sheet, string fileName, int depth)
    {
        if (!p.TrySkip("@import", ignoreCase: true))
        {
            return false;
        }

        var files = p.ReadUntilOrEnd(";");
        p.Pointer++;
        var directory = Path.GetDirectoryName(fileName) ?? "";
        foreach (var file in files.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var path = ResolveImport(directory, file.Trim('"', '\''))
                ?? throw new FormatException($"Can't find import {file} {p.FileAndLine}");
            ParseInto(sheet, File.ReadAllText(path), path, depth + 1);
        }

        return true;
    }

    private static string? ResolveImport(string directory, string name)
    {
        var path = Path.GetFullPath(name, Path.IsPathRooted(directory) ? directory : AppContext.BaseDirectory);
        if (Path.HasExtension(path))
        {
            return File.Exists(path) ? path : null;
        }

        var file = Path.GetFileName(path);
        var folder = Path.GetDirectoryName(path) ?? directory;
        return new[] { Path.Combine(folder, file + ".scss"), Path.Combine(folder, "_" + file + ".scss") }.FirstOrDefault(File.Exists);
    }

    private static bool ParseTopLevelInclude(ref Parse p, StyleSheet sheet)
    {
        if (!p.Is("@include", ignoreCase: true))
        {
            return false;
        }

        int line = p.CurrentLine;
        var include = p.ReadUntilOrEnd(";");
        p.Pointer++;
        var expanded = ExpandInclude(include, null, sheet, p.FileAndLine);

        var inner = new Parse(expanded, p.FileName, line);
        while (!(inner = inner.SkipWhitespaceAndNewlines()).IsEnd)
        {
            var selector = inner.ReadUntilOrEnd("{");
            if (inner.Current != '{')
            {
                break;
            }

            ReadStyleBlock(ref inner, selector, sheet, null);
        }

        return true;
    }

    private static void ReadStyleBlock(ref Parse p, string selectors, StyleSheet sheet, StyleBlock? parent)
    {
        p.Pointer++;
        var block = new StyleBlock(p.FileName, p.CurrentLine);
        block.SetSelector(selectors, parent);

        while (!(p = p.SkipWhitespaceAndNewlines()).IsEnd)
        {
            if (p.Current == '}')
            {
                p.Pointer++;
                if (!block.Styles.IsEmpty)
                {
                    block.LoadOrder = sheet.Blocks.Count;
                    sheet.Blocks.Add(block);
                }

                return;
            }

            var content = p.ReadUntilOrEnd(";{}");
            if (p.Current == '{')
            {
                if (IsInclude(content))
                {
                    var body = ReadBracedContent(ref p);
                    ParseDeclarations(ExpandInclude(content, body, sheet, p.FileAndLine), block, sheet, p.FileName, p.CurrentLine);
                }
                else
                {
                    ReadStyleBlock(ref p, content, sheet, block);
                }

                continue;
            }

            if (IsInclude(content))
            {
                ParseDeclarations(ExpandInclude(content, null, sheet, p.FileAndLine), block, sheet, p.FileName, p.CurrentLine);
            }
            else
            {
                SetDeclaration(block.Styles, content, sheet, p);
            }

            if (p.Current == ';')
            {
                p.Pointer++;
            }
        }

        throw new FormatException($"Unexpected end of block {p.FileAndLine}");
    }

    private static void ParseDeclarations(string content, StyleBlock block, StyleSheet sheet, string fileName, int line)
    {
        var p = new Parse(content, fileName, line);
        while (!(p = p.SkipWhitespaceAndNewlines()).IsEnd)
        {
            if (IsNestedBlock(p))
            {
                var selector = p.ReadUntilOrEnd("{");
                ReadStyleBlock(ref p, selector, sheet, block);
                continue;
            }

            var declaration = p.ReadUntilOrEnd(";}");
            p.Pointer++;
            if (IsInclude(declaration))
            {
                ParseDeclarations(ExpandInclude(declaration, null, sheet, p.FileAndLine), block, sheet, fileName, line);
            }
            else if (!string.IsNullOrWhiteSpace(declaration))
            {
                SetDeclaration(block.Styles, declaration, sheet, p);
            }
        }
    }

    private static void SetDeclaration(Styles styles, string declaration, StyleSheet sheet, in Parse p)
    {
        int colon = declaration.IndexOf(':');
        if (colon <= 0)
        {
            throw new FormatException($"Expected property: value {p.FileAndLine}");
        }

        var property = declaration[..colon].Trim();
        var value = ResolveUrls(sheet.ReplaceVariables(declaration[(colon + 1)..].Trim()), p.FileName);
        if (!styles.Set(property, value))
        {
            Log.Warning($"'{value}' isn't a valid {property} {p.FileAndLine}");
        }
    }

    private static string ResolveUrls(string value, string fileName) => Path.IsPathRooted(fileName)
        ? UrlPattern().Replace(value, m => $"url(\"{Path.GetFullPath(m.Groups[1].Value, Path.GetDirectoryName(fileName)!)}\")")
        : value;

    [GeneratedRegex("""url\(\s*["']?([^"')]+)["']?\s*\)""")]
    private static partial Regex UrlPattern();

    private static bool IsInclude(string content) => content.TrimStart().StartsWith("@include", StringComparison.OrdinalIgnoreCase);

    private static bool IsNestedBlock(Parse p)
    {
        int depth = 0;
        for (; !p.IsEnd; p.Pointer++)
        {
            switch (p.Current)
            {
                case '(':
                    depth++;
                    break;
                case ')':
                    depth--;
                    break;
                case '{' when depth == 0:
                    return true;
                case ';' when depth == 0:
                    return false;
            }
        }

        return false;
    }

    private static string ReadBracedContent(ref Parse p)
    {
        int depth = 0;
        int start = p.Pointer + 1;
        for (; !p.IsEnd; p.Pointer++)
        {
            if (p.Current == '{')
            {
                depth++;
            }
            else if (p.Current == '}' && --depth == 0)
            {
                var content = p.Text[start..p.Pointer];
                p.Pointer++;
                return content;
            }
        }

        throw new FormatException($"Unterminated block {p.FileAndLine}");
    }
}
