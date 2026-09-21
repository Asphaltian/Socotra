namespace Socotra;

internal ref struct Parse
{
    public string FileName;
    public string Text;
    public int Pointer;
    private int _lineOffset;

    public Parse(string? value, string fileName = "nofile", int lineOffset = 0)
    {
        FileName = fileName;
        Text = value ?? string.Empty;
        _lineOffset = lineOffset;
    }

    public readonly int Length => Text.Length;
    public readonly bool IsEnd => Pointer >= Length;
    public readonly char Current => IsEnd ? '\0' : Text[Pointer];
    public readonly char Next => Pointer + 1 >= Length ? '\0' : Text[Pointer + 1];
    public readonly bool IsWhitespace => char.IsWhiteSpace(Current);
    public readonly bool IsNewline => Current is '\n' or '\r';
    public readonly bool IsDigit => char.IsDigit(Current);
    public readonly bool IsLetter => char.IsLetter(Current);

    public readonly int CurrentLine => Text.AsSpan(0, Math.Min(Pointer, Text.Length)).Count('\n') + _lineOffset;

    public readonly string FileAndLine => $"[{FileName}:{CurrentLine}]";

    public readonly bool IsOneOf(string? chars) => chars is not null && chars.Contains(Current);

    public string Read(int chars)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(chars);
        var result = Text.Substring(Pointer, chars);
        Pointer += chars;
        return result;
    }

    public string ReadRemaining()
    {
        var result = Text[Math.Min(Pointer, Length)..];
        Pointer = Length;
        return result;
    }

    public Parse SkipWhitespaceAndNewlines(string? andCharacters = null)
    {
        while (!IsEnd)
        {
            if (!IsWhitespace && !IsNewline && !IsOneOf(andCharacters))
            {
                return this;
            }

            Pointer++;
        }

        return this;
    }

    public string ReadUntilWhitespaceOrNewlineOrEnd(string? andCharacters = null)
    {
        var p = this;
        while (!p.IsEnd && !p.IsNewline && !p.IsWhitespace && !p.IsOneOf(andCharacters))
        {
            p.Pointer++;
        }

        return Read(p.Pointer - Pointer);
    }

    public string ReadUntilWhitespaceOrNewlineOrEndAndObeyBrackets()
    {
        var p = this;
        int depth = 0;
        while (!p.IsEnd && (depth > 0 || (!p.IsNewline && !p.IsWhitespace)))
        {
            if (p.Current == '(')
            {
                depth++;
            }
            else if (p.Current == ')')
            {
                depth--;
            }

            p.Pointer++;
        }

        return Read(p.Pointer - Pointer);
    }

    public string? ReadInnerBrackets(char open = '(', char close = ')')
    {
        int depth = 0;
        int start = Pointer;
        while (!IsEnd)
        {
            if (Current == open)
            {
                if (depth == 0)
                {
                    start = Pointer + 1;
                }

                depth++;
            }
            else if (Current == close && --depth == 0)
            {
                var inner = Text[start..Pointer];
                Pointer++;
                return inner;
            }

            Pointer++;
        }

        return null;
    }

    public string? ReadWord(string? endOnCharacter = null, bool readUntilEnd = false, bool respectParens = false)
    {
        var p = this;
        int depth = 0;
        while (true)
        {
            if (p.IsEnd)
            {
                return readUntilEnd ? Read(p.Pointer - Pointer) : null;
            }

            var c = p.Current;
            if (respectParens && c is '(' or '[' or '{')
            {
                depth++;
            }
            else if (respectParens && c is ')' or ']' or '}')
            {
                depth = Math.Max(0, depth - 1);
            }
            else if (depth == 0 && (p.IsWhitespace || p.IsNewline || p.IsOneOf(endOnCharacter)))
            {
                return Read(p.Pointer - Pointer);
            }

            p.Pointer++;
        }
    }

    public string? ReadChars(string chars)
    {
        var p = this;
        while (!p.IsEnd && p.IsOneOf(chars))
        {
            p.Pointer++;
        }

        return p.Pointer == Pointer ? null : Read(p.Pointer - Pointer);
    }

    public string ReadSentence()
    {
        var p = this;
        while (!p.IsEnd && p.Current != ',')
        {
            if (p.Current == '(')
            {
                while (!p.IsEnd && p.Current != ')')
                {
                    p.Pointer++;
                }
            }
            else
            {
                p.Pointer++;
            }
        }

        return Read(p.Pointer - Pointer);
    }

    public string? ReadUntil(string characters)
    {
        var p = this;
        while (!p.IsEnd)
        {
            if (p.IsOneOf(characters))
            {
                return Read(p.Pointer - Pointer);
            }

            p.Pointer++;
        }

        return null;
    }

    public string ReadUntilOrEnd(string characters, bool respectParens = false)
    {
        var p = this;
        int depth = 0;
        while (!p.IsEnd)
        {
            var c = p.Current;
            if (respectParens && c is '(' or '[' or '{')
            {
                depth++;
            }
            else if (respectParens && c is ')' or ']' or '}')
            {
                depth = Math.Max(0, depth - 1);
            }
            else if (depth == 0 && p.IsOneOf(characters))
            {
                return Read(p.Pointer - Pointer);
            }

            p.Pointer++;
        }

        return ReadRemaining();
    }

    public (string Key, string Value) ReadKeyValue()
    {
        var key = ReadUntilOrEnd(":");
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new FormatException($"Expected key {FileAndLine}");
        }

        Pointer++;
        var value = ReadUntilOrEnd(";");
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new FormatException($"Expected value {FileAndLine}");
        }

        Pointer++;
        return (key.Trim(), value.Trim());
    }

    public bool TryReadTime(out float milliseconds)
    {
        milliseconds = 0;
        var p = SkipWhitespaceAndNewlines();
        int start = p.Pointer;
        if (p.Current is '-' or '+')
        {
            p.Pointer++;
        }

        while (!p.IsEnd && (p.IsDigit || p.Current == '.'))
        {
            p.Pointer++;
        }

        if (!Translation.TryParseFloat(p.Text.AsSpan(start, p.Pointer - start), out var value))
        {
            return false;
        }

        if (p.Current is 's' or 'S')
        {
            Pointer = p.Pointer + 1;
            milliseconds = value * 1000.0f;
            return true;
        }

        if (p.Current is 'm' or 'M' && p.Next is 's' or 'S')
        {
            Pointer = p.Pointer + 2;
            milliseconds = value;
            return true;
        }

        return false;
    }

    public bool TryReadLength(out Length length)
    {
        length = default;
        var p = SkipWhitespaceAndNewlines();
        if (p.IsEnd)
        {
            return false;
        }

        bool isFunction = p.Is("calc(", ignoreCase: true) || p.Is("min(", ignoreCase: true) || p.Is("max(", ignoreCase: true)
            || p.Is("clamp(", ignoreCase: true) || p.Is("var(", ignoreCase: true);
        var word = isFunction ? p.ReadWord(null, true, true) : p.ReadWord(")/{,", true);
        if (string.IsNullOrEmpty(word) || Socotra.Length.Parse(word) is not { } parsed)
        {
            return false;
        }

        length = parsed;
        Pointer = p.Pointer;
        return true;
    }

    public bool TryReadFloat(out float value)
    {
        value = 0;
        var p = SkipWhitespaceAndNewlines();
        var word = p.ReadChars("-0123456789.Ee");
        if (word is null || !Translation.TryParseFloat(word, out value))
        {
            return false;
        }

        Pointer = p.Pointer;
        if (Current == 'f')
        {
            Pointer++;
        }

        return true;
    }

    public bool TryReadColor(out Color color)
    {
        color = default;
        var p = SkipWhitespaceAndNewlines();
        int start = p.Pointer;
        int depth = 0;
        while (!p.IsEnd)
        {
            if (p.Current == '(')
            {
                depth++;
            }
            else if (p.Current == ')' && --depth < 0)
            {
                return false;
            }

            if (depth == 0 && p.IsOneOf(" ;\t\n\r,"))
            {
                break;
            }

            p.Pointer++;
        }

        if (start == p.Pointer || Color.ParseStyle(p.Text[start..p.Pointer]) is not { } parsed)
        {
            return false;
        }

        color = p.ReadBrightness(parsed);
        Pointer = p.Pointer;
        return true;
    }

    public Color ReadBrightness(Color color)
    {
        var end = Pointer;
        if (SkipWhitespaceAndNewlines().Current == '*')
        {
            Pointer++;
            if (TryReadFloat(out var scale))
            {
                return color.ScaleBrightness(scale);
            }
        }

        Pointer = end;
        return color;
    }

    public bool TryReadLineStyle(out string style) => TryReadKeyword(out style, "none", "solid", "double", "dotted", "dashed", "inset", "outset", "ridge", "groove", "hidden");

    public bool TryReadRepeat(out string repeat) => TryReadKeyword(out repeat, "no-repeat", "repeat-x", "repeat-y", "repeat", "clamp");

    public bool TryReadMaskMode(out string mode) => TryReadKeyword(out mode, "match-source", "alpha", "luminance");

    public bool TryReadShadowInset() => TryReadKeyword(out _, "inset");

    public bool TryReadPositionAndSize(out Length positionX, out Length positionY, out Length sizeX, out Length sizeY)
    {
        positionY = 0;
        sizeX = Socotra.Length.Auto;
        sizeY = Socotra.Length.Auto;
        if (!TryReadLength(out positionX))
        {
            return false;
        }

        if (!TryReadLength(out positionY))
        {
            positionY = positionX;
        }

        SkipWhitespaceAndNewlines();
        if (TrySkip("/"))
        {
            if (!TryReadLength(out sizeX))
            {
                return false;
            }

            TryReadLength(out sizeY);
        }

        SkipWhitespaceAndNewlines();
        return true;
    }

    private bool TryReadKeyword(out string keyword, params ReadOnlySpan<string> keywords)
    {
        keyword = "";
        var p = SkipWhitespaceAndNewlines();
        if (!p.IsLetter)
        {
            return false;
        }

        var word = p.ReadWord(",", readUntilEnd: true)!.ToLowerInvariant();
        foreach (var candidate in keywords)
        {
            if (word == candidate)
            {
                keyword = word;
                Pointer = p.Pointer;
                return true;
            }
        }

        return false;
    }

    public readonly bool Is(string value, int offset = 0, bool ignoreCase = false)
    {
        for (int i = 0; i < value.Length; i++)
        {
            if (!Is(value[i], offset + i, ignoreCase))
            {
                return false;
            }
        }

        return true;
    }

    public readonly bool Is(char value, int offset = 0, bool ignoreCase = false)
    {
        var index = Pointer + offset;
        if (index < 0 || index >= Length)
        {
            return false;
        }

        return ignoreCase ? char.ToLowerInvariant(Text[index]) == char.ToLowerInvariant(value) : Text[index] == value;
    }

    public bool TrySkip(string value, bool ignoreCase = false)
    {
        if (!Is(value, 0, ignoreCase))
        {
            return false;
        }

        Pointer += value.Length;
        return true;
    }

    public bool TrySkipCommaSeparation()
    {
        this = SkipWhitespaceAndNewlines();
        if (Current != ',')
        {
            return false;
        }

        Pointer++;
        this = SkipWhitespaceAndNewlines();
        return true;
    }
}
