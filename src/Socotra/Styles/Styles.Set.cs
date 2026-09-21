namespace Socotra;

public partial class Styles
{
    /// <summary>
    /// Sets one property the way you'd write it in a stylesheet, like <c>Set("margin", "10px 20px")</c>. Shorthands,
    /// <c>!important</c>, <c>inherit</c>, <c>initial</c>, <c>unset</c> and <c>revert</c> all work. Returns false if
    /// <paramref name="property"/> or <paramref name="value"/> isn't understood.
    /// </summary>
    public virtual bool Set(string property, string value)
    {
        property = StyleParser.GetPropertyFromAlias(property.Trim().ToLowerInvariant());
        value = value.Trim();
        if (value.EndsWith("!important", StringComparison.OrdinalIgnoreCase))
        {
            value = value[..^"!important".Length].TrimEnd();
        }

        bool success;
        if (ParseCssWideKeyword(value) is { } keyword && MarkCssWide(property, keyword))
        {
            success = true;
        }
        else
        {
            ClearCssWide(property);
            success = SetSpecial(property, value) ?? (PropertiesByName.TryGetValue(property, out var known) && known.Set(this, value));
        }

        Dirty();
        return success;
    }

    /// <summary>Sets several properties at once, written like the inside of a CSS rule: <c>"width: 10px; color: red"</c>. Returns false if any of them weren't understood.</summary>
    public bool Set(string styles)
    {
        bool success = true;
        var p = new Parse(styles);
        while (!(p = p.SkipWhitespaceAndNewlines(";")).IsEnd)
        {
            var declaration = p.ReadUntilOrEnd(";", respectParens: true);
            int colon = declaration.IndexOf(':');
            success &= colon > 0 && Set(declaration[..colon], declaration[(colon + 1)..]);
        }

        return success;
    }

    private bool? SetSpecial(string property, string value) => property switch
    {
        "transition" or "transition-delay" or "transition-duration" or "transition-property" or "transition-timing-function" => SetTransition(property, value),
        "pointer-events" when value.Equals("auto", StringComparison.OrdinalIgnoreCase) => Clear(ref _pointerEvents),
        "flex" => SetFlex(value),
        "flex-flow" => SetFlexFlow(value),
        "gap" => SetPair(value, "row-gap", "column-gap"),
        "padding" => SetEdges(value, "padding-top", "padding-right", "padding-bottom", "padding-left"),
        "margin" => SetEdges(value, "margin-top", "margin-right", "margin-bottom", "margin-left"),
        "inset" => SetEdges(value, "top", "right", "bottom", "left"),
        "margin-block" => SetPair(value, "margin-top", "margin-bottom"),
        "margin-inline" => SetPair(value, "margin-left", "margin-right"),
        "padding-block" => SetPair(value, "padding-top", "padding-bottom"),
        "padding-inline" => SetPair(value, "padding-left", "padding-right"),
        "inset-block" => SetPair(value, "top", "bottom"),
        "inset-inline" => SetPair(value, "left", "right"),
        "border-radius" => SetBorderRadius(value),
        "border-top-left-radius" => SetCornerRadius(value, ref _borderTopLeftRadius, ref _borderTopLeftRadiusV),
        "border-top-right-radius" => SetCornerRadius(value, ref _borderTopRightRadius, ref _borderTopRightRadiusV),
        "border-bottom-right-radius" => SetCornerRadius(value, ref _borderBottomRightRadius, ref _borderBottomRightRadiusV),
        "border-bottom-left-radius" => SetCornerRadius(value, ref _borderBottomLeftRadius, ref _borderBottomLeftRadiusV),
        "border" => SetBorder(value, w => BorderWidth = w, c => BorderColor = c, s => _borderStyle = s),
        "border-left" => SetBorder(value, w => _borderLeftWidth = w, c => _borderLeftColor = c, s => _borderStyle = s),
        "border-top" => SetBorder(value, w => _borderTopWidth = w, c => _borderTopColor = c, s => _borderStyle = s),
        "border-right" => SetBorder(value, w => _borderRightWidth = w, c => _borderRightColor = c, s => _borderStyle = s),
        "border-bottom" => SetBorder(value, w => _borderBottomWidth = w, c => _borderBottomColor = c, s => _borderStyle = s),
        "outline" => SetBorder(value, w => _outlineWidth = w, c => _outlineColor = c, null),
        "border-width" => SetEdges(value, "border-top-width", "border-right-width", "border-bottom-width", "border-left-width"),
        "border-color" => SetEdges(value, "border-top-color", "border-right-color", "border-bottom-color", "border-left-color"),
        "border-image" => SetBorderImage(value),
        "filter" => SetFilter(value),
        "backdrop-filter" => SetBackdropFilter(value),
        "font" => SetFont(value),
        "font-color" => Update(ref _fontColor, Color.ParseStyle(value)) || SetTextGradient(value),
        "place-items" => SetPair(value, "align-items", "justify-items"),
        "place-self" => SetPair(value, "align-self", "justify-self"),
        "grid-column" => SetGridLine(value, ref _gridColumnStart, ref _gridColumnEnd),
        "grid-row" => SetGridLine(value, ref _gridRowStart, ref _gridRowEnd),
        "grid-area" => SetGridArea(value),
        "grid-template" => SetGridTemplate(value),
        "text-decoration" => SetTextDecoration(value),
        "text-stroke" => SetTextStroke(value),
        "transform-origin" => SetLengthPair(value, ref _transformOriginX, ref _transformOriginY),
        "perspective-origin" => SetLengthPair(value, ref _perspectiveOriginX, ref _perspectiveOriginY),
        "overflow" => SetPair(value, "overflow-x", "overflow-y"),
        "overscroll-behavior" => SetPair(value, "overscroll-behavior-x", "overscroll-behavior-y"),
        "scrollbar-color" => SetScrollbarColor(value),
        "animation" => SetAnimation(value),
        "background" => SetBackground(value),
        "background-image" => SetImage(value, ImageTarget.Background),
        "background-size" => SetLengthPair(value, ref _backgroundSizeX, ref _backgroundSizeY),
        "background-position" => SetLengthPair(value, ref _backgroundPositionX, ref _backgroundPositionY),
        "mask" => SetMask(value),
        "mask-image" => SetImage(value, ImageTarget.Mask),
        "mask-size" => SetLengthPair(value, ref _maskSizeX, ref _maskSizeY),
        "mask-position" => SetLengthPair(value, ref _maskPositionX, ref _maskPositionY),
        _ => null,
    };

    private static bool Assign<T>(ref T field, T value)
    {
        field = value;
        return true;
    }

    private static bool Update<T>(ref T? field, T? value)
        where T : struct
    {
        if (value is null)
        {
            return false;
        }

        field = value;
        return true;
    }

    private static bool Clear<T>(ref T? field)
        where T : struct
    {
        field = null;
        return true;
    }

    private bool SetTransition(string property, string value)
    {
        if (TransitionDesc.ParseProperty(property, value, _transitions) is not { } transitions)
        {
            return false;
        }

        _transitions = transitions;
        return true;
    }

    private bool SetFlex(string value)
    {
        int numbers = 0;
        foreach (var word in SplitValues(value))
        {
            switch (word.ToLowerInvariant())
            {
                case "none":
                    _flexShrink ??= 0;
                    _flexGrow ??= 0;
                    _flexBasis = Length.Auto;
                    return true;
                case "auto":
                    _flexShrink ??= 1;
                    _flexGrow ??= 1;
                    _flexBasis = Length.Auto;
                    return true;
                case "initial":
                    _flexGrow ??= 0;
                    _flexShrink ??= 1;
                    _flexBasis = Length.Auto;
                    return true;
            }

            if (ParseFloat(word) is { } number)
            {
                switch (numbers++)
                {
                    case 0:
                        _flexGrow = number;
                        _flexShrink = 1;
                        _flexBasis = 0;
                        break;
                    case 1:
                        _flexShrink = number;
                        break;
                    default:
                        _flexBasis = number;
                        break;
                }
            }
            else if (Length.Parse(word) is { } basis)
            {
                _flexGrow ??= 0;
                _flexShrink ??= 1;
                _flexBasis = basis;
                return true;
            }
            else
            {
                return false;
            }
        }

        return true;
    }

    private bool SetFlexFlow(string value)
    {
        bool any = false;
        foreach (var word in SplitValues(value))
        {
            any |= PropertiesByName["flex-direction"].Set(this, word) || PropertiesByName["flex-wrap"].Set(this, word);
        }

        return any;
    }

    private bool SetEdges(string value, string top, string right, string bottom, string left)
    {
        var parts = SplitValues(value);
        if (parts.Count is < 1 or > 4)
        {
            return false;
        }

        var (t, r, b, l) = ExpandFour(parts);
        return SetAll([(top, t), (right, r), (bottom, b), (left, l)]);
    }

    private static (T, T, T, T) ExpandFour<T>(IReadOnlyList<T> values) => values.Count switch
    {
        1 => (values[0], values[0], values[0], values[0]),
        2 => (values[0], values[1], values[0], values[1]),
        3 => (values[0], values[1], values[2], values[1]),
        _ => (values[0], values[1], values[2], values[3]),
    };

    private bool SetPair(string value, string first, string second)
    {
        var parts = SplitValues(value);
        return parts.Count is 1 or 2 && SetAll([(first, parts[0]), (second, parts[^1])]);
    }

    private bool SetAll(ReadOnlySpan<(string Property, string Value)> parts)
    {
        var parsed = new Styles();
        foreach (var (property, value) in parts)
        {
            if (!PropertiesByName[property].Set(parsed, value))
            {
                return false;
            }
        }

        foreach (var (property, _) in parts)
        {
            PropertiesByName[property].Copy(this, parsed);
        }

        return true;
    }

    private static bool SetLengthPair(string value, ref Length? x, ref Length? y)
    {
        var p = new Parse(value);
        if (!p.TryReadLength(out var first))
        {
            return false;
        }

        x = first;
        y = p.TryReadLength(out var second) ? second : first;
        return true;
    }

    private bool SetBorderRadius(string value)
    {
        var slash = value.IndexOf('/');
        if (ReadCornerLengths(slash < 0 ? value : value[..slash]) is not { } horizontal)
        {
            return false;
        }

        var vertical = slash < 0 ? horizontal : ReadCornerLengths(value[(slash + 1)..]);
        if (vertical is null)
        {
            return false;
        }

        (_borderTopLeftRadius, _borderTopRightRadius, _borderBottomRightRadius, _borderBottomLeftRadius) = horizontal;
        (_borderTopLeftRadiusV, _borderTopRightRadiusV, _borderBottomRightRadiusV, _borderBottomLeftRadiusV) = vertical.Value;
        return true;
    }

    private static (Length, Length, Length, Length)? ReadCornerLengths(string value)
    {
        var p = new Parse(value);
        var read = new List<Length>(4);
        while (read.Count < 4 && p.TryReadLength(out var length))
        {
            read.Add(length);
        }

        return read.Count == 0 || !p.SkipWhitespaceAndNewlines().IsEnd ? null : ExpandFour(read);
    }

    private static bool SetCornerRadius(string value, ref Length? horizontal, ref Length? vertical)
    {
        var p = new Parse(value);
        if (!p.TryReadLength(out var h))
        {
            return false;
        }

        var v = p.TryReadLength(out var read) ? read : h;
        if (!p.SkipWhitespaceAndNewlines().IsEnd)
        {
            return false;
        }

        horizontal = h;
        vertical = v;
        return true;
    }

    private static bool SetBorder(string value, Action<Length?> setWidth, Action<Color?> setColor, Action<Socotra.BorderStyle>? setStyle)
    {
        Length? width = null;
        Color? color = null;
        Socotra.BorderStyle? style = null;
        var p = new Parse(value);
        if (p.SkipWhitespaceAndNewlines().IsEnd)
        {
            return false;
        }

        while (!p.SkipWhitespaceAndNewlines().IsEnd)
        {
            if (p.TryReadLineStyle(out var word))
            {
                if (style is not null)
                {
                    return false;
                }

                style = Enum.Parse<Socotra.BorderStyle>(word, true);
            }
            else if (p.TryReadLength(out var length))
            {
                if (width is not null || (length.Unit != LengthUnit.Expression && length.Value < 0))
                {
                    return false;
                }

                width = length;
            }
            else if (p.TryReadColor(out var parsed))
            {
                if (color is not null)
                {
                    return false;
                }

                color = parsed;
            }
            else
            {
                return false;
            }
        }

        if (style is Socotra.BorderStyle.None or Socotra.BorderStyle.Hidden)
        {
            setWidth(Length.Pixels(0));
        }
        else
        {
            if (width is not null)
            {
                setWidth(width);
            }

            if (style is { } visible)
            {
                setStyle?.Invoke(visible);
            }
        }

        if (color is not null)
        {
            setColor(color);
        }

        return true;
    }

    private void SetInitialLonghands(string shorthand)
    {
        foreach (var name in ShorthandExpansions[shorthand])
        {
            PropertiesByName[name].SetInitial(this);
        }
    }

    private bool SetFilter(string value)
    {
        if (value.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            SetInitialLonghands("filter");
            return true;
        }

        return ReadFunctions(value, (name, arguments) => name switch
        {
            "blur" => Update(ref _filterBlur, Length.Parse(arguments)),
            "saturate" => Update(ref _filterSaturate, Length.Parse(arguments)),
            "grayscale" or "greyscale" => Update(ref _filterSaturate, Grayscale(arguments)),
            "sepia" => Update(ref _filterSepia, Length.Parse(arguments)),
            "brightness" => Update(ref _filterBrightness, Length.Parse(arguments)),
            "contrast" => Update(ref _filterContrast, Length.Parse(arguments)),
            "hue-rotate" => Update(ref _filterHueRotate, Length.Parse(arguments)),
            "invert" => Update(ref _filterInvert, Length.Parse(arguments)),
            "tint" => Update(ref _filterTint, Color.ParseStyle(arguments)),
            "drop-shadow" => Shadow.ParseList(arguments) is { } shadows && Assign(ref _filterDropShadow, shadows),
            "border-wrap" => SetFilterBorderWrap(arguments),
            _ => false,
        });
    }

    private bool SetBackdropFilter(string value)
    {
        if (value.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            SetInitialLonghands("backdrop-filter");
            return true;
        }

        return ReadFunctions(value, (name, arguments) => name switch
        {
            "blur" => Update(ref _backdropFilterBlur, Length.Parse(arguments)),
            "invert" => Update(ref _backdropFilterInvert, Length.Parse(arguments)),
            "contrast" => Update(ref _backdropFilterContrast, Length.Parse(arguments)),
            "brightness" => Update(ref _backdropFilterBrightness, Length.Parse(arguments)),
            "grayscale" or "greyscale" => Update(ref _backdropFilterSaturate, Grayscale(arguments)),
            "saturate" => Update(ref _backdropFilterSaturate, Length.Parse(arguments)),
            "sepia" => Update(ref _backdropFilterSepia, Length.Parse(arguments)),
            "hue-rotate" => Update(ref _backdropFilterHueRotate, Length.Parse(arguments)),
            _ => false,
        });
    }

    private static Length? Grayscale(string arguments) => Length.Parse(arguments) is { } amount ? 1 - amount.GetPixels(1) : null;

    private static bool ReadFunctions(string value, Func<string, string, bool> apply)
    {
        var p = new Parse(value);
        while (!(p = p.SkipWhitespaceAndNewlines()).IsEnd)
        {
            var name = p.ReadWord("(")?.ToLowerInvariant();
            var arguments = p.ReadInnerBrackets();
            if (name is null || arguments is null || !apply(name, arguments))
            {
                return false;
            }
        }

        return true;
    }

    private bool SetFilterBorderWrap(string value)
    {
        var p = new Parse(value);
        while (!p.SkipWhitespaceAndNewlines().IsEnd)
        {
            if (p.TryReadLength(out var width))
            {
                _filterBorderWidth = width;
            }
            else if (p.TryReadColor(out var color))
            {
                _filterBorderColor = color;
            }
            else
            {
                return false;
            }
        }

        return true;
    }

    private bool SetFont(string value)
    {
        var words = value.Split([' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
        var size = Array.FindIndex(words, IsFontSizeWord);
        if (size < 0 || size + 1 >= words.Length)
        {
            return false;
        }

        foreach (var word in words[..size])
        {
            var lower = word.ToLowerInvariant();
            if (lower is "italic" or "oblique")
            {
                Set("font-style", lower);
            }
            else if (lower is not ("normal" or "small-caps"))
            {
                Set("font-weight", lower);
            }
        }

        var slash = words[size].IndexOf('/');
        Set("font-size", slash < 0 ? words[size] : words[size][..slash]);
        if (slash >= 0)
        {
            Set("line-height", words[size][(slash + 1)..]);
        }

        Set("font-family", string.Join(' ', words[(size + 1)..]));
        return true;
    }

    private static bool IsFontSizeWord(string word)
    {
        if (FontSizeKeywords.ContainsKey(word) || word.Contains('/'))
        {
            return true;
        }

        return word.Any(char.IsDigit) && word.Any(c => !char.IsDigit(c) && c is not ('.' or '-' or '+'));
    }

    private static bool SetGridLine(string value, ref string? start, ref string? end)
    {
        var parts = value.Split('/', StringSplitOptions.TrimEntries);
        if (parts.Length > 2 || parts.Any(p => p.Length == 0))
        {
            return false;
        }

        start = parts[0];
        end = parts.Length == 2 ? parts[1] : IsCustomIdent(parts[0]) ? parts[0] : "auto";
        return true;
    }

    private bool SetGridArea(string value)
    {
        var parts = value.Split('/', StringSplitOptions.TrimEntries);
        if (parts.Length > 4 || parts.Any(p => p.Length == 0))
        {
            return false;
        }

        _gridRowStart = parts[0];
        _gridColumnStart = parts.Length > 1 ? parts[1] : IsCustomIdent(parts[0]) ? parts[0] : "auto";
        _gridRowEnd = parts.Length > 2 ? parts[2] : IsCustomIdent(parts[0]) ? parts[0] : "auto";
        _gridColumnEnd = parts.Length > 3 ? parts[3] : IsCustomIdent(_gridColumnStart) ? _gridColumnStart : "auto";
        return true;
    }

    private bool SetGridTemplate(string value)
    {
        if (value == "none")
        {
            (_gridTemplateRows, _gridTemplateColumns) = ("none", "none");
            return true;
        }

        var parts = value.Split('/', StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            return false;
        }

        (_gridTemplateRows, _gridTemplateColumns) = (parts[0], parts[1]);
        return true;
    }

    private static bool IsCustomIdent(string value) =>
        value.Length > 0 && !char.IsDigit(value[0]) && value[0] != '-' && value is not ("auto" or "span")
        && value.All(c => char.IsLetterOrDigit(c) || c is '-' or '_');

    private bool SetTextDecoration(string value)
    {
        var lines = TextDecoration.None;
        var p = new Parse(value);
        if (p.SkipWhitespaceAndNewlines().IsEnd)
        {
            return false;
        }

        while (!p.SkipWhitespaceAndNewlines().IsEnd)
        {
            if (p.TryReadLength(out var thickness))
            {
                _textDecorationThickness = thickness;
                continue;
            }

            if (p.TryReadColor(out var color))
            {
                _textDecorationColor = color;
                continue;
            }

            var word = p.ReadWord(null, readUntilEnd: true)!;
            if (word == "none")
            {
                _textDecorationLine = TextDecoration.None;
            }
            else if (ParseTextDecorationLine(word) is { } line and not TextDecoration.None)
            {
                lines |= line;
            }
            else if (!PropertiesByName["text-decoration-style"].Set(this, word))
            {
                return false;
            }
        }

        if (lines != TextDecoration.None)
        {
            _textDecorationLine = lines;
        }

        return true;
    }

    private bool SetTextStroke(string value)
    {
        var p = new Parse(value);
        if (!p.TryReadLength(out var width) || !p.TryReadColor(out var color))
        {
            return false;
        }

        _textStrokeWidth = width;
        _textStrokeColor = color;
        return true;
    }

    private bool SetScrollbarColor(string value)
    {
        if (value == "auto")
        {
            (_scrollbarThumbColor, _scrollbarTrackColor) = (null, null);
            return true;
        }

        var parts = SplitValues(value);
        if (parts.Count is not (1 or 2) || Color.ParseStyle(parts[0]) is not { } thumb)
        {
            return false;
        }

        var track = parts.Count == 2 ? Color.ParseStyle(parts[1]) : null;
        if (parts.Count == 2 && track is null)
        {
            return false;
        }

        (_scrollbarThumbColor, _scrollbarTrackColor) = (thumb, track);
        return true;
    }

    private bool SetAnimation(string value)
    {
        var p = new Parse(value).SkipWhitespaceAndNewlines();
        if (p.Is("none", ignoreCase: true))
        {
            _animationName = "none";
            return true;
        }

        int times = 0;
        while (!(p = p.SkipWhitespaceAndNewlines()).IsEnd)
        {
            if (p.TryReadTime(out var milliseconds))
            {
                if (times++ == 0)
                {
                    _animationDuration = milliseconds / 1000.0f;
                }
                else
                {
                    _animationDelay = milliseconds / 1000.0f;
                }

                continue;
            }

            var word = p.ReadWord(null, readUntilEnd: true, respectParens: true)!.ToLowerInvariant();
            if (Easing.TryGetFunction(word, out _))
            {
                _animationTimingFunction = word;
            }
            else if (!PropertiesByName["animation-iteration-count"].Set(this, word)
                && !PropertiesByName["animation-direction"].Set(this, word)
                && !PropertiesByName["animation-fill-mode"].Set(this, word)
                && !PropertiesByName["animation-play-state"].Set(this, word))
            {
                _animationName = word;
            }
        }

        return true;
    }

    private static List<string> SplitValues(string value)
    {
        var parts = new List<string>();
        var p = new Parse(value);
        while (!(p = p.SkipWhitespaceAndNewlines()).IsEnd)
        {
            parts.Add(p.ReadWord(null, readUntilEnd: true, respectParens: true)!);
        }

        return parts;
    }
}
