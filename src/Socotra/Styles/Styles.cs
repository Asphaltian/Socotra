namespace Socotra;

/// <summary>
/// A set of CSS properties, like a panel's <see cref="Panel.Style"/>. Leave a property null to let stylesheets
/// and the parent decide it. Set properties from C# or with the same text you'd write in a stylesheet.
/// </summary>
/// <example><code>
/// // Typed properties
/// panel.Style.Width = Length.Percent(50);
/// panel.Style.BackgroundColor = Color.Parse("#223");
///
/// // Or CSS, shorthands included
/// panel.Style.Set("padding", "8px 16px");
/// panel.Style.Set("border: 2px solid white; border-radius: 4px");
/// </code></example>
public partial class Styles
{
    internal static readonly Styles Default = new() { Padding = 0 };

    private TransitionList? _transitions;

    internal GradientInfo BackgroundGradient;

    internal TextGradientInfo TextGradient;

    /// <summary><c>content</c>: the text a <c>::before</c> or <c>::after</c> element, or a label, shows instead of its own.</summary>
    public string? Content { get => _content; set => Set(ref _content, value); }

    /// <summary><c>width</c>: how wide the panel is. Leave it unset to let layout decide.</summary>
    public Length? Width { get => _width; set => Set(ref _width, value); }

    /// <summary><c>min-width</c>: the narrowest layout can make the panel.</summary>
    public Length? MinWidth { get => _minWidth; set => Set(ref _minWidth, value); }

    /// <summary><c>max-width</c>: the widest layout can make the panel.</summary>
    public Length? MaxWidth { get => _maxWidth; set => Set(ref _maxWidth, value); }

    /// <summary><c>height</c>: how tall the panel is. Leave it unset to let layout decide.</summary>
    public Length? Height { get => _height; set => Set(ref _height, value); }

    /// <summary><c>min-height</c>: the shortest layout can make the panel.</summary>
    public Length? MinHeight { get => _minHeight; set => Set(ref _minHeight, value); }

    /// <summary><c>max-height</c>: the tallest layout can make the panel.</summary>
    public Length? MaxHeight { get => _maxHeight; set => Set(ref _maxHeight, value); }

    /// <summary><c>left</c>: moves a positioned panel's left edge in from the left. Has no effect with <c>position: static</c>.</summary>
    public Length? Left { get => _left; set => Set(ref _left, value); }

    /// <summary><c>top</c>: moves a positioned panel's top edge down from the top. Has no effect with <c>position: static</c>.</summary>
    public Length? Top { get => _top; set => Set(ref _top, value); }

    /// <summary><c>right</c>: moves a positioned panel's right edge in from the right. Has no effect with <c>position: static</c>.</summary>
    public Length? Right { get => _right; set => Set(ref _right, value); }

    /// <summary><c>bottom</c>: moves a positioned panel's bottom edge up from the bottom. Has no effect with <c>position: static</c>.</summary>
    public Length? Bottom { get => _bottom; set => Set(ref _bottom, value); }

    /// <summary><c>opacity</c>: how see-through the panel and everything in it is, from 0 (invisible) to 1 (solid). A percentage works too.</summary>
    public float? Opacity { get => _opacity; set => Set(ref _opacity, value); }

    /// <summary><c>background-color</c>: the color that fills the panel behind its content.</summary>
    public Color? BackgroundColor { get => _backgroundColor; set => Set(ref _backgroundColor, value); }

    /// <summary><c>padding-left</c>: the space between the panel's left border and its content.</summary>
    public Length? PaddingLeft { get => _paddingLeft; set => Set(ref _paddingLeft, value); }

    /// <summary><c>padding-top</c>: the space between the panel's top border and its content.</summary>
    public Length? PaddingTop { get => _paddingTop; set => Set(ref _paddingTop, value); }

    /// <summary><c>padding-right</c>: the space between the panel's right border and its content.</summary>
    public Length? PaddingRight { get => _paddingRight; set => Set(ref _paddingRight, value); }

    /// <summary><c>padding-bottom</c>: the space between the panel's bottom border and its content.</summary>
    public Length? PaddingBottom { get => _paddingBottom; set => Set(ref _paddingBottom, value); }

    /// <summary><c>margin-left</c>: the space kept clear outside the panel's left edge.</summary>
    public Length? MarginLeft { get => _marginLeft; set => Set(ref _marginLeft, value); }

    /// <summary><c>margin-top</c>: the space kept clear outside the panel's top edge.</summary>
    public Length? MarginTop { get => _marginTop; set => Set(ref _marginTop, value); }

    /// <summary><c>margin-right</c>: the space kept clear outside the panel's right edge.</summary>
    public Length? MarginRight { get => _marginRight; set => Set(ref _marginRight, value); }

    /// <summary><c>margin-bottom</c>: the space kept clear outside the panel's bottom edge.</summary>
    public Length? MarginBottom { get => _marginBottom; set => Set(ref _marginBottom, value); }

    /// <summary><c>border-top-left-radius</c>: how rounded the top left corner is. This is the horizontal radius; set <see cref="BorderTopLeftRadiusV"/> as well for an elliptical corner.</summary>
    public Length? BorderTopLeftRadius { get => _borderTopLeftRadius; set => Set(ref _borderTopLeftRadius, value); }

    /// <summary><c>border-top-right-radius</c>: how rounded the top right corner is. This is the horizontal radius; set <see cref="BorderTopRightRadiusV"/> as well for an elliptical corner.</summary>
    public Length? BorderTopRightRadius { get => _borderTopRightRadius; set => Set(ref _borderTopRightRadius, value); }

    /// <summary><c>border-bottom-right-radius</c>: how rounded the bottom right corner is. This is the horizontal radius; set <see cref="BorderBottomRightRadiusV"/> as well for an elliptical corner.</summary>
    public Length? BorderBottomRightRadius { get => _borderBottomRightRadius; set => Set(ref _borderBottomRightRadius, value); }

    /// <summary><c>border-bottom-left-radius</c>: how rounded the bottom left corner is. This is the horizontal radius; set <see cref="BorderBottomLeftRadiusV"/> as well for an elliptical corner.</summary>
    public Length? BorderBottomLeftRadius { get => _borderBottomLeftRadius; set => Set(ref _borderBottomLeftRadius, value); }

    /// <summary>The vertical radius of <c>border-top-left-radius</c>, for an elliptical corner. Leave it null to match <see cref="BorderTopLeftRadius"/>.</summary>
    public Length? BorderTopLeftRadiusV { get => _borderTopLeftRadiusV; set => Set(ref _borderTopLeftRadiusV, value); }

    /// <summary>The vertical radius of <c>border-top-right-radius</c>, for an elliptical corner. Leave it null to match <see cref="BorderTopRightRadius"/>.</summary>
    public Length? BorderTopRightRadiusV { get => _borderTopRightRadiusV; set => Set(ref _borderTopRightRadiusV, value); }

    /// <summary>The vertical radius of <c>border-bottom-right-radius</c>, for an elliptical corner. Leave it null to match <see cref="BorderBottomRightRadius"/>.</summary>
    public Length? BorderBottomRightRadiusV { get => _borderBottomRightRadiusV; set => Set(ref _borderBottomRightRadiusV, value); }

    /// <summary>The vertical radius of <c>border-bottom-left-radius</c>, for an elliptical corner. Leave it null to match <see cref="BorderBottomLeftRadius"/>.</summary>
    public Length? BorderBottomLeftRadiusV { get => _borderBottomLeftRadiusV; set => Set(ref _borderBottomLeftRadiusV, value); }

    /// <summary><c>border-left-width</c>: how thick the left border is. It counts as zero while <see cref="BorderStyle"/> is <c>none</c> or <c>hidden</c>.</summary>
    public Length? BorderLeftWidth { get => _borderLeftWidth; set => Set(ref _borderLeftWidth, value); }

    /// <summary><c>border-top-width</c>: how thick the top border is. It counts as zero while <see cref="BorderStyle"/> is <c>none</c> or <c>hidden</c>.</summary>
    public Length? BorderTopWidth { get => _borderTopWidth; set => Set(ref _borderTopWidth, value); }

    /// <summary><c>border-right-width</c>: how thick the right border is. It counts as zero while <see cref="BorderStyle"/> is <c>none</c> or <c>hidden</c>.</summary>
    public Length? BorderRightWidth { get => _borderRightWidth; set => Set(ref _borderRightWidth, value); }

    /// <summary><c>border-bottom-width</c>: how thick the bottom border is. It counts as zero while <see cref="BorderStyle"/> is <c>none</c> or <c>hidden</c>.</summary>
    public Length? BorderBottomWidth { get => _borderBottomWidth; set => Set(ref _borderBottomWidth, value); }

    /// <summary><c>border-style</c>: how the border is drawn, like <c>solid</c> or <c>dashed</c>. Every side uses the same style.</summary>
    public BorderStyle? BorderStyle { get => _borderStyle; set => Set(ref _borderStyle, value); }

    /// <summary><c>border-left-color</c>: the color of the left border.</summary>
    public Color? BorderLeftColor { get => _borderLeftColor; set => Set(ref _borderLeftColor, value); }

    /// <summary><c>border-top-color</c>: the color of the top border.</summary>
    public Color? BorderTopColor { get => _borderTopColor; set => Set(ref _borderTopColor, value); }

    /// <summary><c>border-right-color</c>: the color of the right border.</summary>
    public Color? BorderRightColor { get => _borderRightColor; set => Set(ref _borderRightColor, value); }

    /// <summary><c>border-bottom-color</c>: the color of the bottom border.</summary>
    public Color? BorderBottomColor { get => _borderBottomColor; set => Set(ref _borderBottomColor, value); }

    /// <summary><c>font-size</c>: how big the text is. The size keywords, from <c>xx-small</c> to <c>xxx-large</c>, work too.</summary>
    public Length? FontSize { get => _fontSize; set => Set(ref _fontSize, value); }

    /// <summary><c>color</c>: the text color.</summary>
    public Color? FontColor { get => _fontColor; set => Set(ref _fontColor, value); }

    /// <summary><c>font-weight</c>: how bold the text is, from 100 to 950. Names like <c>bold</c> and <c>semibold</c> work too.</summary>
    public int? FontWeight { get => _fontWeight; set => Set(ref _fontWeight, value); }

    /// <summary><c>font-family</c>: the font the text uses. Only the first family in a list counts, and generic families like <c>monospace</c> pick a common font.</summary>
    public string? FontFamily { get => _fontFamily; set => Set(ref _fontFamily, value); }

    /// <summary><c>caret-color</c>: the color of the text cursor. Leave it null to use the text color.</summary>
    public Color? CaretColor { get => _caretColor; set => Set(ref _caretColor, value); }

    /// <summary><c>cursor</c>: the mouse cursor the panel asks for, like <c>pointer</c>. Socotra doesn't change the cursor for you, so read it from the hovered panel's <see cref="Panel.ComputedStyle"/> and set your window's cursor.</summary>
    public string? Cursor { get => _cursor; set => Set(ref _cursor, value); }

    /// <summary><c>pointer-events</c>: whether the panel reacts to the mouse. Set it to <c>all</c> on anything that should; children get the same value unless they set their own.</summary>
    public PointerEvents? PointerEvents { get => _pointerEvents; set => Set(ref _pointerEvents, value); }

    /// <summary><c>mix-blend-mode</c>: how the panel blends with what's behind it: <c>normal</c>, <c>multiply</c> or <c>lighten</c>.</summary>
    public string? MixBlendMode { get => _mixBlendMode; set => Set(ref _mixBlendMode, value); }

    /// <summary><c>position</c>: whether the panel stays in the normal layout or is placed with <see cref="Left"/>, <see cref="Top"/>, <see cref="Right"/> and <see cref="Bottom"/>.</summary>
    public PositionMode? Position { get => _position; set => Set(ref _position, value); }

    /// <summary><c>overflow-x</c>: what happens to content that's wider than the panel. If only one axis is set, the other uses the same value.</summary>
    public OverflowMode? OverflowX { get => _overflowX; set => Set(ref _overflowX, value); }

    /// <summary><c>overflow-y</c>: what happens to content that's taller than the panel. If only one axis is set, the other uses the same value.</summary>
    public OverflowMode? OverflowY { get => _overflowY; set => Set(ref _overflowY, value); }

    /// <summary><c>flex-direction</c>: whether children are laid out in a row or a column.</summary>
    public FlexDirection? FlexDirection { get => _flexDirection; set => Set(ref _flexDirection, value); }

    /// <summary><c>justify-content</c>: where children go along the main axis, and how spare space is shared between them.</summary>
    public Justify? JustifyContent { get => _justifyContent; set => Set(ref _justifyContent, value); }

    /// <summary><c>justify-items</c>: where grid items sit across their cell, unless they set <see cref="JustifySelf"/>.</summary>
    public Align? JustifyItems { get => _justifyItems; set => Set(ref _justifyItems, value); }

    /// <summary><c>justify-self</c>: where this grid item sits across its cell, overriding the parent's <see cref="JustifyItems"/>.</summary>
    public Align? JustifySelf { get => _justifySelf; set => Set(ref _justifySelf, value); }

    /// <summary><c>display</c>: how the panel lays out its children, like <c>flex</c> or <c>grid</c>, or <c>none</c> to hide it.</summary>
    public DisplayMode? Display { get => _display; set => Set(ref _display, value); }

    /// <summary><c>grid-template-columns</c>: the columns of a grid, like <c>1fr 2fr</c> or <c>repeat(3, 100px)</c>.</summary>
    public string? GridTemplateColumns { get => _gridTemplateColumns; set => Set(ref _gridTemplateColumns, value); }

    /// <summary><c>grid-template-rows</c>: the rows of a grid, like <c>auto 1fr</c>.</summary>
    public string? GridTemplateRows { get => _gridTemplateRows; set => Set(ref _gridTemplateRows, value); }

    /// <summary><c>grid-auto-columns</c>: how wide a grid's extra columns are when items don't fit the ones you set.</summary>
    public string? GridAutoColumns { get => _gridAutoColumns; set => Set(ref _gridAutoColumns, value); }

    /// <summary><c>grid-auto-rows</c>: how tall a grid's extra rows are when items don't fit the ones you set.</summary>
    public string? GridAutoRows { get => _gridAutoRows; set => Set(ref _gridAutoRows, value); }

    /// <summary><c>grid-auto-flow</c>: where grid items without a set position go.</summary>
    public GridAutoFlow? GridAutoFlow { get => _gridAutoFlow; set => Set(ref _gridAutoFlow, value); }

    /// <summary><c>grid-column-start</c>: the grid line this item starts at across, like <c>2</c> or <c>span 2</c>.</summary>
    public string? GridColumnStart { get => _gridColumnStart; set => Set(ref _gridColumnStart, value); }

    /// <summary><c>grid-column-end</c>: the grid line this item ends at across.</summary>
    public string? GridColumnEnd { get => _gridColumnEnd; set => Set(ref _gridColumnEnd, value); }

    /// <summary><c>grid-row-start</c>: the grid line this item starts at down, like <c>2</c> or <c>span 2</c>.</summary>
    public string? GridRowStart { get => _gridRowStart; set => Set(ref _gridRowStart, value); }

    /// <summary><c>grid-row-end</c>: the grid line this item ends at down.</summary>
    public string? GridRowEnd { get => _gridRowEnd; set => Set(ref _gridRowEnd, value); }

    /// <summary><c>flex-wrap</c>: whether children that don't fit move onto a new line.</summary>
    public Wrap? FlexWrap { get => _flexWrap; set => Set(ref _flexWrap, value); }

    /// <summary><c>align-content</c>: how the lines of a wrapping flex container, or a grid's tracks, share spare space on the cross axis.</summary>
    public Align? AlignContent { get => _alignContent; set => Set(ref _alignContent, value); }

    /// <summary><c>align-self</c>: where this panel sits on its parent's cross axis, overriding the parent's <see cref="AlignItems"/>.</summary>
    public Align? AlignSelf { get => _alignSelf; set => Set(ref _alignSelf, value); }

    /// <summary><c>align-items</c>: where children sit on the cross axis, like centered or stretched to fill.</summary>
    public Align? AlignItems { get => _alignItems; set => Set(ref _alignItems, value); }

    /// <summary><c>flex-basis</c>: the panel's starting size along the main axis, before it grows or shrinks.</summary>
    public Length? FlexBasis { get => _flexBasis; set => Set(ref _flexBasis, value); }

    /// <summary><c>flex-grow</c>: how much of the spare space the panel takes, compared to its siblings. 0 means it doesn't grow.</summary>
    public float? FlexGrow { get => _flexGrow; set => Set(ref _flexGrow, value); }

    /// <summary><c>flex-shrink</c>: how much the panel shrinks, compared to its siblings, when they don't fit. 0 means it doesn't shrink.</summary>
    public float? FlexShrink { get => _flexShrink; set => Set(ref _flexShrink, value); }

    /// <summary><c>row-gap</c>: the space between rows of children.</summary>
    public Length? RowGap { get => _rowGap; set => Set(ref _rowGap, value); }

    /// <summary><c>column-gap</c>: the space between columns of children.</summary>
    public Length? ColumnGap { get => _columnGap; set => Set(ref _columnGap, value); }

    /// <summary><c>aspect-ratio</c>: the shape the panel keeps, as its width divided by its height. NaN means <c>auto</c> or <c>none</c>.</summary>
    public float? AspectRatio { get => _aspectRatio; set => Set(ref _aspectRatio, value); }

    /// <summary><c>text-align</c>: how lines of text line up inside the panel.</summary>
    public TextAlign? TextAlign { get => _textAlign; set => Set(ref _textAlign, value); }

    /// <summary><c>text-overflow</c>: what text that doesn't fit looks like, like ending in an ellipsis.</summary>
    public TextOverflow? TextOverflow { get => _textOverflow; set => Set(ref _textOverflow, value); }

    /// <summary><c>text-filter</c>: how text looks when it's scaled, smooth or with hard pixel edges.</summary>
    public FilterMode? TextFilter { get => _textFilter; set => Set(ref _textFilter, value); }

    /// <summary><c>word-break</c>: whether lines can break in the middle of a word.</summary>
    public WordBreak? WordBreak { get => _wordBreak; set => Set(ref _wordBreak, value); }

    /// <summary><c>text-decoration-line</c>: which lines are drawn on the text, like an underline.</summary>
    public TextDecoration? TextDecorationLine { get => _textDecorationLine; set => Set(ref _textDecorationLine, value); }

    /// <summary><c>text-decoration-color</c>: the color of the lines from <see cref="TextDecorationLine"/>.</summary>
    public Color? TextDecorationColor { get => _textDecorationColor; set => Set(ref _textDecorationColor, value); }

    /// <summary><c>text-decoration-thickness</c>: how thick the lines from <see cref="TextDecorationLine"/> are.</summary>
    public Length? TextDecorationThickness { get => _textDecorationThickness; set => Set(ref _textDecorationThickness, value); }

    /// <summary><c>text-decoration-skip-ink</c>: whether underlines and overlines break around the letters they'd cross.</summary>
    public TextSkipInk? TextDecorationSkipInk { get => _textDecorationSkipInk; set => Set(ref _textDecorationSkipInk, value); }

    /// <summary><c>text-decoration-style</c>: how the lines from <see cref="TextDecorationLine"/> are drawn, like dashed or wavy.</summary>
    public TextDecorationStyle? TextDecorationStyle { get => _textDecorationStyle; set => Set(ref _textDecorationStyle, value); }

    /// <summary><c>text-underline-offset</c>: how far the underline is moved from where it normally sits.</summary>
    public Length? TextUnderlineOffset { get => _textUnderlineOffset; set => Set(ref _textUnderlineOffset, value); }

    /// <summary><c>text-overline-offset</c>: how far the overline is moved from where it normally sits.</summary>
    public Length? TextOverlineOffset { get => _textOverlineOffset; set => Set(ref _textOverlineOffset, value); }

    /// <summary><c>text-line-through-offset</c>: how far the line through the text is moved from where it normally sits.</summary>
    public Length? TextLineThroughOffset { get => _textLineThroughOffset; set => Set(ref _textLineThroughOffset, value); }

    /// <summary><c>font-style</c>: whether the text is italic or oblique.</summary>
    public FontStyle? FontStyle { get => _fontStyle; set => Set(ref _fontStyle, value); }

    /// <summary><c>font-variant-numeric</c>: how digits are drawn, like <c>tabular-nums</c> to give every digit the same width.</summary>
    public Topten.RichTextKit.FontVariantNumeric? FontVariantNumeric { get => _fontVariantNumeric; set => Set(ref _fontVariantNumeric, value); }

    /// <summary><c>transform</c>: moves, rotates, scales or skews the panel as it's drawn, without changing its layout.</summary>
    public PanelTransform? Transform { get => _transform; set => Set(ref _transform, value); }

    /// <summary><c>text-transform</c>: changes the text's capitals, like making it all uppercase.</summary>
    public TextTransform? TextTransform { get => _textTransform; set => Set(ref _textTransform, value); }

    /// <summary>The horizontal part of <c>transform-origin</c>: the point <see cref="Transform"/> rotates and scales around.</summary>
    public Length? TransformOriginX { get => _transformOriginX; set => Set(ref _transformOriginX, value); }

    /// <summary>The vertical part of <c>transform-origin</c>: the point <see cref="Transform"/> rotates and scales around.</summary>
    public Length? TransformOriginY { get => _transformOriginY; set => Set(ref _transformOriginY, value); }

    /// <summary><c>letter-spacing</c>: extra space between letters.</summary>
    public Length? LetterSpacing { get => _letterSpacing; set => Set(ref _letterSpacing, value); }

    /// <summary><c>line-height</c>: how tall each line of text is. A plain number multiplies the font size, and <c>normal</c> is 100%.</summary>
    public Length? LineHeight { get => _lineHeight; set => Set(ref _lineHeight, value); }

    /// <summary><c>word-spacing</c>: extra space between words.</summary>
    public Length? WordSpacing { get => _wordSpacing; set => Set(ref _wordSpacing, value); }

    /// <summary><c>white-space</c>: whether spaces and line breaks in the text are kept, and whether lines wrap.</summary>
    public WhiteSpace? WhiteSpace { get => _whiteSpace; set => Set(ref _whiteSpace, value); }

    /// <summary><c>z-index</c>: which siblings the panel is drawn over. Higher numbers go on top.</summary>
    public int? ZIndex { get => _zIndex; set => Set(ref _zIndex, value); }

    /// <summary><c>order</c>: where the panel goes among its siblings in layout. Lower numbers go first.</summary>
    public int? Order { get => _order; set => Set(ref _order, value); }

    /// <summary><c>backdrop-filter: blur()</c>: blurs what's behind the panel.</summary>
    public Length? BackdropFilterBlur { get => _backdropFilterBlur; set => Set(ref _backdropFilterBlur, value); }

    /// <summary><c>backdrop-filter: brightness()</c>: darkens what's behind the panel below 1 and brightens it above 1.</summary>
    public Length? BackdropFilterBrightness { get => _backdropFilterBrightness; set => Set(ref _backdropFilterBrightness, value); }

    /// <summary><c>backdrop-filter: contrast()</c>: lowers the contrast of what's behind the panel below 1 and raises it above 1.</summary>
    public Length? BackdropFilterContrast { get => _backdropFilterContrast; set => Set(ref _backdropFilterContrast, value); }

    /// <summary><c>backdrop-filter: saturate()</c>: washes out the colors behind the panel below 1 and deepens them above 1.</summary>
    public Length? BackdropFilterSaturate { get => _backdropFilterSaturate; set => Set(ref _backdropFilterSaturate, value); }

    /// <summary><c>backdrop-filter: sepia()</c>: tints what's behind the panel brown, from 0 (not at all) to 1 (fully).</summary>
    public Length? BackdropFilterSepia { get => _backdropFilterSepia; set => Set(ref _backdropFilterSepia, value); }

    /// <summary><c>backdrop-filter: invert()</c>: inverts the colors behind the panel, from 0 (not at all) to 1 (fully).</summary>
    public Length? BackdropFilterInvert { get => _backdropFilterInvert; set => Set(ref _backdropFilterInvert, value); }

    /// <summary><c>backdrop-filter: hue-rotate()</c>: turns the hue of what's behind the panel, in degrees.</summary>
    public Length? BackdropFilterHueRotate { get => _backdropFilterHueRotate; set => Set(ref _backdropFilterHueRotate, value); }

    /// <summary><c>filter: blur()</c>: blurs the panel.</summary>
    public Length? FilterBlur { get => _filterBlur; set => Set(ref _filterBlur, value); }

    /// <summary><c>filter: saturate()</c>: washes out the panel's colors below 1 and deepens them above 1.</summary>
    public Length? FilterSaturate { get => _filterSaturate; set => Set(ref _filterSaturate, value); }

    /// <summary><c>filter: sepia()</c>: tints the panel brown, from 0 (not at all) to 1 (fully).</summary>
    public Length? FilterSepia { get => _filterSepia; set => Set(ref _filterSepia, value); }

    /// <summary><c>filter: brightness()</c>: darkens the panel below 1 and brightens it above 1.</summary>
    public Length? FilterBrightness { get => _filterBrightness; set => Set(ref _filterBrightness, value); }

    /// <summary><c>filter: hue-rotate()</c>: turns the panel's hue, in degrees.</summary>
    public Length? FilterHueRotate { get => _filterHueRotate; set => Set(ref _filterHueRotate, value); }

    /// <summary><c>filter: invert()</c>: inverts the panel's colors, from 0 (not at all) to 1 (fully).</summary>
    public Length? FilterInvert { get => _filterInvert; set => Set(ref _filterInvert, value); }

    /// <summary><c>filter: contrast()</c>: lowers the panel's contrast below 1 and raises it above 1.</summary>
    public Length? FilterContrast { get => _filterContrast; set => Set(ref _filterContrast, value); }

    /// <summary><c>filter: tint()</c>: a color the panel is multiplied by.</summary>
    public Color? FilterTint { get => _filterTint; set => Set(ref _filterTint, value); }

    /// <summary>The width from <c>filter: border-wrap()</c>: an outline drawn around the panel's shape.</summary>
    public Length? FilterBorderWidth { get => _filterBorderWidth; set => Set(ref _filterBorderWidth, value); }

    /// <summary>The color from <c>filter: border-wrap()</c>: the color of the outline around the panel's shape.</summary>
    public Color? FilterBorderColor { get => _filterBorderColor; set => Set(ref _filterBorderColor, value); }

    /// <summary><c>mask-mode</c>: whether the mask image's transparency or brightness decides what's hidden.</summary>
    public MaskMode? MaskMode { get => _maskMode; set => Set(ref _maskMode, value); }

    /// <summary><c>mask-repeat</c>: whether the mask image tiles, and in which directions.</summary>
    public BackgroundRepeat? MaskRepeat { get => _maskRepeat; set => Set(ref _maskRepeat, value); }

    /// <summary>The horizontal part of <c>mask-size</c>: how wide the mask image is drawn.</summary>
    public Length? MaskSizeX { get => _maskSizeX; set => Set(ref _maskSizeX, value); }

    /// <summary>The vertical part of <c>mask-size</c>: how tall the mask image is drawn.</summary>
    public Length? MaskSizeY { get => _maskSizeY; set => Set(ref _maskSizeY, value); }

    /// <summary>The horizontal part of <c>mask-position</c>: where the mask image starts across.</summary>
    public Length? MaskPositionX { get => _maskPositionX; set => Set(ref _maskPositionX, value); }

    /// <summary>The vertical part of <c>mask-position</c>: where the mask image starts down.</summary>
    public Length? MaskPositionY { get => _maskPositionY; set => Set(ref _maskPositionY, value); }

    /// <summary>The angle of a <c>mask-image</c> gradient, in radians.</summary>
    public Length? MaskAngle { get => _maskAngle; set => Set(ref _maskAngle, value); }

    /// <summary><c>mask-scope</c>: whether the mask hides the panel or fades its filters in and out.</summary>
    public MaskScope? MaskScope { get => _maskScope; set => Set(ref _maskScope, value); }

    /// <summary>The horizontal part of <c>background-size</c>: how wide the background image is drawn.</summary>
    public Length? BackgroundSizeX { get => _backgroundSizeX; set => Set(ref _backgroundSizeX, value); }

    /// <summary>The vertical part of <c>background-size</c>: how tall the background image is drawn.</summary>
    public Length? BackgroundSizeY { get => _backgroundSizeY; set => Set(ref _backgroundSizeY, value); }

    /// <summary>The horizontal part of <c>background-position</c>: where the background image starts across.</summary>
    public Length? BackgroundPositionX { get => _backgroundPositionX; set => Set(ref _backgroundPositionX, value); }

    /// <summary>The vertical part of <c>background-position</c>: where the background image starts down.</summary>
    public Length? BackgroundPositionY { get => _backgroundPositionY; set => Set(ref _backgroundPositionY, value); }

    /// <summary><c>background-repeat</c>: whether the background image tiles, and in which directions.</summary>
    public BackgroundRepeat? BackgroundRepeat { get => _backgroundRepeat; set => Set(ref _backgroundRepeat, value); }

    /// <summary><c>background-clip</c>: how far out the background is drawn, like under the border or only inside the padding.</summary>
    public BackgroundClip? BackgroundClip { get => _backgroundClip; set => Set(ref _backgroundClip, value); }

    /// <summary>How much of the <c>border-image</c> is sliced off for the left edge.</summary>
    public Length? BorderImageWidthLeft { get => _borderImageWidthLeft; set => Set(ref _borderImageWidthLeft, value); }

    /// <summary>How much of the <c>border-image</c> is sliced off for the right edge.</summary>
    public Length? BorderImageWidthRight { get => _borderImageWidthRight; set => Set(ref _borderImageWidthRight, value); }

    /// <summary>How much of the <c>border-image</c> is sliced off for the top edge.</summary>
    public Length? BorderImageWidthTop { get => _borderImageWidthTop; set => Set(ref _borderImageWidthTop, value); }

    /// <summary>How much of the <c>border-image</c> is sliced off for the bottom edge.</summary>
    public Length? BorderImageWidthBottom { get => _borderImageWidthBottom; set => Set(ref _borderImageWidthBottom, value); }

    /// <summary>Whether <c>border-image</c> has <c>fill</c>, which draws the middle of the image as well as the edges.</summary>
    public BorderImageFill? BorderImageFill { get => _borderImageFill; set => Set(ref _borderImageFill, value); }

    /// <summary>Whether the edges of <c>border-image</c> stretch or tile.</summary>
    public BorderImageRepeat? BorderImageRepeat { get => _borderImageRepeat; set => Set(ref _borderImageRepeat, value); }

    /// <summary>A color the <c>border-image</c> is multiplied by.</summary>
    public Color? BorderImageTint { get => _borderImageTint; set => Set(ref _borderImageTint, value); }

    /// <summary><c>background-blend-mode</c>: how the background image blends with the background color: <c>normal</c>, <c>multiply</c> or <c>lighten</c>.</summary>
    public string? BackgroundBlendMode { get => _backgroundBlendMode; set => Set(ref _backgroundBlendMode, value); }

    /// <summary><c>background-tint</c> or <c>background-image-tint</c>: a color the background image is multiplied by.</summary>
    public Color? BackgroundTint { get => _backgroundTint; set => Set(ref _backgroundTint, value); }

    /// <summary>The angle of a <c>background-image</c> gradient, in radians.</summary>
    public Length? BackgroundAngle { get => _backgroundAngle; set => Set(ref _backgroundAngle, value); }

    /// <summary><c>text-background-angle</c>. Nothing is drawn with it yet, so setting it has no visible effect.</summary>
    public Length? TextBackgroundAngle { get => _textBackgroundAngle; set => Set(ref _textBackgroundAngle, value); }

    /// <summary>The horizontal part of <c>perspective-origin</c>: where you look at the panel from when <see cref="Transform"/> has a <c>perspective()</c>.</summary>
    public Length? PerspectiveOriginX { get => _perspectiveOriginX; set => Set(ref _perspectiveOriginX, value); }

    /// <summary>The vertical part of <c>perspective-origin</c>: where you look at the panel from when <see cref="Transform"/> has a <c>perspective()</c>.</summary>
    public Length? PerspectiveOriginY { get => _perspectiveOriginY; set => Set(ref _perspectiveOriginY, value); }

    /// <summary><c>text-stroke-color</c>: the color of the outline around each letter.</summary>
    public Color? TextStrokeColor { get => _textStrokeColor; set => Set(ref _textStrokeColor, value); }

    /// <summary><c>text-stroke-width</c>: how thick the outline around each letter is.</summary>
    public Length? TextStrokeWidth { get => _textStrokeWidth; set => Set(ref _textStrokeWidth, value); }

    /// <summary><c>image-rendering</c>: how images look when they're scaled, smooth or with hard pixel edges.</summary>
    public ImageRendering? ImageRendering { get => _imageRendering; set => Set(ref _imageRendering, value); }

    /// <summary><c>animation-delay</c>: how long the animation waits before it starts, in seconds.</summary>
    public float? AnimationDelay { get => _animationDelay; set => Set(ref _animationDelay, value); }

    /// <summary><c>animation-direction</c>: which way the animation plays: <c>normal</c>, <c>reverse</c>, <c>alternate</c> or <c>alternate-reverse</c>.</summary>
    public string? AnimationDirection { get => _animationDirection; set => Set(ref _animationDirection, value); }

    /// <summary><c>animation-duration</c>: how long one run of the animation takes, in seconds. Without one it lasts a second.</summary>
    public float? AnimationDuration { get => _animationDuration; set => Set(ref _animationDuration, value); }

    /// <summary><c>animation-fill-mode</c>: whether the animation's styles apply before it starts and after it ends: <c>none</c>, <c>forwards</c>, <c>backwards</c> or <c>both</c>.</summary>
    public string? AnimationFillMode { get => _animationFillMode; set => Set(ref _animationFillMode, value); }

    /// <summary><c>animation-iteration-count</c>: how many times the animation plays. <c>infinite</c> is positive infinity.</summary>
    public float? AnimationIterationCount { get => _animationIterationCount; set => Set(ref _animationIterationCount, value); }

    /// <summary><c>animation-name</c>: the <c>@keyframes</c> rule to play.</summary>
    public string? AnimationName { get => _animationName; set => Set(ref _animationName, value); }

    /// <summary><c>animation-play-state</c>: <c>running</c>, or <c>paused</c> to hold the animation where it is.</summary>
    public string? AnimationPlayState { get => _animationPlayState; set => Set(ref _animationPlayState, value); }

    /// <summary><c>animation-timing-function</c>: the name of the <see cref="Easing"/> function the animation moves with.</summary>
    public string? AnimationTimingFunction { get => _animationTimingFunction; set => Set(ref _animationTimingFunction, value); }

    /// <summary><c>font-smooth</c>: whether the edges of the text are smoothed.</summary>
    public FontSmooth? FontSmooth { get => _fontSmooth; set => Set(ref _fontSmooth, value); }

    /// <summary><c>object-fit</c>: how an image is sized to fit its panel.</summary>
    public ObjectFit? ObjectFit { get => _objectFit; set => Set(ref _objectFit, value); }

    /// <summary><c>outline-width</c>: how thick the outline around the panel is. Unlike a border, it doesn't take up any space in layout.</summary>
    public Length? OutlineWidth { get => _outlineWidth; set => Set(ref _outlineWidth, value); }

    /// <summary><c>outline-color</c>: the color of the outline around the panel.</summary>
    public Color? OutlineColor { get => _outlineColor; set => Set(ref _outlineColor, value); }

    /// <summary><c>outline-offset</c>: how far outside the panel's border the outline is drawn.</summary>
    public Length? OutlineOffset { get => _outlineOffset; set => Set(ref _outlineOffset, value); }

    /// <summary><c>isolation</c>: whether the panel and its children blend together first, before they blend with what's behind them.</summary>
    public Isolation? Isolation { get => _isolation; set => Set(ref _isolation, value); }

    /// <summary><c>scrollbar-width</c>: how wide scrollbars are. <c>none</c> is 0 and <c>thin</c> is 8 pixels.</summary>
    public Length? ScrollbarWidth { get => _scrollbarWidth; set => Set(ref _scrollbarWidth, value); }

    /// <summary><c>overscroll-behavior-x</c>: what happens when you scroll past the left or right end.</summary>
    public OverscrollBehavior? OverscrollBehaviorX { get => _overscrollBehaviorX; set => Set(ref _overscrollBehaviorX, value); }

    /// <summary><c>overscroll-behavior-y</c>: what happens when you scroll past the top or bottom.</summary>
    public OverscrollBehavior? OverscrollBehaviorY { get => _overscrollBehaviorY; set => Set(ref _overscrollBehaviorY, value); }

    /// <summary><c>scrollbar-gutter</c>: whether space is kept for the scrollbar, so content doesn't move when it appears.</summary>
    public ScrollbarGutter? ScrollbarGutter { get => _scrollbarGutter; set => Set(ref _scrollbarGutter, value); }

    /// <summary>The first color of <c>scrollbar-color</c>: the part you drag. Leave it null for the default look.</summary>
    public Color? ScrollbarThumbColor { get => _scrollbarThumbColor; set => Set(ref _scrollbarThumbColor, value); }

    /// <summary>The second color of <c>scrollbar-color</c>: the track behind the part you drag. Leave it null for the default look.</summary>
    public Color? ScrollbarTrackColor { get => _scrollbarTrackColor; set => Set(ref _scrollbarTrackColor, value); }

    /// <summary><c>border-shape</c>: a shape, like a circle or a polygon, to draw the panel's background and border in instead of a box.</summary>
    public BorderShape? BorderShape { get => _borderShape; set => Set(ref _borderShape, value); }

    /// <summary><c>box-shadow</c>: shadows drawn around or inside the panel's box.</summary>
    public IReadOnlyList<Shadow>? BoxShadow { get => _boxShadow; set => Set(ref _boxShadow, value); }

    /// <summary><c>text-shadow</c>: shadows drawn behind the text.</summary>
    public IReadOnlyList<Shadow>? TextShadow { get => _textShadow; set => Set(ref _textShadow, value); }

    /// <summary><c>filter: drop-shadow()</c>: shadows that follow the shape of what the panel draws, not just its box.</summary>
    public IReadOnlyList<Shadow>? FilterDropShadow { get => _filterDropShadow; set => Set(ref _filterDropShadow, value); }

    /// <summary><c>background-image</c>: a picture drawn behind the panel's content. It's null for <c>none</c> and for gradients.</summary>
    public Texture? BackgroundImage { get => _backgroundImage?.Value; set => SetImage(ref _backgroundImage, value); }

    /// <summary><c>mask-image</c>: an image that hides parts of the panel. Gradients work too.</summary>
    public Texture? MaskImage { get => _maskImage?.Value; set => SetImage(ref _maskImage, value); }

    /// <summary>The image from <c>border-image</c>, sliced up to draw the panel's border. Gradients work too.</summary>
    public Texture? BorderImageSource { get => _borderImageSource?.Value; set => SetImage(ref _borderImageSource, value); }

    /// <summary><c>overflow</c>: what happens to content that doesn't fit. Setting it sets <see cref="OverflowX"/> and <see cref="OverflowY"/>, and reading it gives <see cref="OverflowMode.Scroll"/> if either one scrolls.</summary>
    public OverflowMode? Overflow
    {
        get => _overflowX == OverflowMode.Scroll || _overflowY == OverflowMode.Scroll ? OverflowMode.Scroll : _overflowX ?? _overflowY;
        set
        {
            OverflowX = value;
            OverflowY = value;
        }
    }

    /// <summary><c>padding</c>: sets the space between the border and the content on every side.</summary>
    public Length? Padding
    {
        set
        {
            PaddingLeft = value;
            PaddingTop = value;
            PaddingRight = value;
            PaddingBottom = value;
        }
    }

    /// <summary><c>margin</c>: sets the space kept clear outside every edge.</summary>
    public Length? Margin
    {
        set
        {
            MarginLeft = value;
            MarginTop = value;
            MarginRight = value;
            MarginBottom = value;
        }
    }

    /// <summary><c>border-width</c>: sets how thick the border is on every side.</summary>
    public Length? BorderWidth
    {
        set
        {
            BorderLeftWidth = value;
            BorderTopWidth = value;
            BorderRightWidth = value;
            BorderBottomWidth = value;
        }
    }

    /// <summary><c>border-color</c>: sets the border color on every side.</summary>
    public Color? BorderColor
    {
        set
        {
            BorderLeftColor = value;
            BorderTopColor = value;
            BorderRightColor = value;
            BorderBottomColor = value;
        }
    }

    /// <summary><c>transition</c> and its longhands: which properties animate smoothly when they change.</summary>
    public TransitionList? Transitions { get => _transitions; set => Set(ref _transitions, value); }

    /// <summary>Whether any transitions are set.</summary>
    public bool HasTransitions => _transitions is { List.Count: > 0 };

    internal bool IsVisible => _display != DisplayMode.None && !(_opacity <= 0);

    internal bool IsEmpty =>
        !Properties.Any(p => p.IsSet(this)) && _transitions is null && BackgroundGradient.IsEmpty && TextGradient.IsEmpty && _cssWide is not { Count: > 0 };

    /// <summary>Runs every time a property changes. Override it to react to changes.</summary>
    public virtual void Dirty()
    {
    }

    /// <summary>Copies over the properties <paramref name="other"/> sets, and leaves the rest alone.</summary>
    public virtual void Add(Styles other)
    {
        foreach (var property in Properties)
        {
            property.Add(this, other);
        }

        if (other.HasTransitions)
        {
            _transitions ??= new TransitionList();
            _transitions.AddTransitions(other._transitions!);
        }

        if (!other.TextGradient.IsEmpty)
        {
            TextGradient = other.TextGradient;
        }

        if (other._backgroundImage is not null || !other.BackgroundGradient.IsEmpty)
        {
            BackgroundGradient = other.BackgroundGradient;
        }

        if (_cssWide is not null || other._cssWide is not null)
        {
            MergeCssWide(other);
        }
    }

    /// <summary>Makes this a copy of <paramref name="other"/>, including the properties it leaves unset.</summary>
    public virtual void From(Styles other)
    {
        foreach (var property in Properties)
        {
            property.Copy(this, other);
        }

        _transitions?.Clear();
        if (other.HasTransitions)
        {
            _transitions ??= new TransitionList();
            _transitions.AddTransitions(other._transitions!);
        }

        TextGradient = other.TextGradient;
        BackgroundGradient = other.BackgroundGradient;
        _cssWide = other._cssWide is null ? null : new(other._cssWide);
    }

    /// <summary>Sets every property that can animate to a blend of <paramref name="from"/> and <paramref name="to"/>: 0 gives <paramref name="from"/>, 1 gives <paramref name="to"/>.</summary>
    public virtual void FromLerp(Styles from, Styles to, float delta)
    {
        foreach (var property in Properties)
        {
            if (property.IsTransitionable)
            {
                property.Lerp(this, from, to, delta);
            }
        }
    }

    /// <summary>
    /// Sets the property called <paramref name="name"/>, like <c>opacity</c> or <c>padding</c>, to a blend of
    /// <paramref name="from"/> and <paramref name="to"/>: 0 gives <paramref name="from"/>, 1 gives <paramref name="to"/>.
    /// </summary>
    public virtual void LerpProperty(string name, Styles from, Styles to, float delta)
    {
        if (PropertiesByName.TryGetValue(name, out var property))
        {
            property.Lerp(this, from, to, delta);
            if (PropertiesByName.TryGetValue(name + "-v", out var vertical))
            {
                vertical.Lerp(this, from, to, delta);
            }

            return;
        }

        foreach (var longhand in ShorthandExpansions.GetValueOrDefault(name) ?? [])
        {
            LerpProperty(longhand, from, to, delta);
        }
    }

    /// <summary>Places the panel at <paramref name="rect"/> by setting <see cref="Left"/>, <see cref="Top"/>, <see cref="Width"/> and <see cref="Height"/> in pixels, multiplied by <paramref name="scale"/>.</summary>
    public void SetRect(Rect rect, float scale = 1)
    {
        Top = Length.Pixels(rect.Top * scale);
        Left = Length.Pixels(rect.Left * scale);
        Width = Length.Pixels(rect.Width * scale);
        Height = Length.Pixels(rect.Height * scale);
    }

    /// <summary>
    /// The matrix <see cref="Transform"/> makes for a box of <paramref name="size"/>. It doesn't include
    /// <c>transform-origin</c>, so move to <see cref="TransformOriginX"/> and <see cref="TransformOriginY"/> yourself if you need it.
    /// </summary>
    public Matrix4x4 BuildTransformMatrix(Vector2 size)
    {
        if (_transform is not { IsEmpty: false } transform)
        {
            return Matrix4x4.Identity;
        }

        var center = size * 0.5f;
        var origin = new Vector2(_perspectiveOriginX?.GetPixels(size.X) ?? center.X, _perspectiveOriginY?.GetPixels(size.Y) ?? center.Y);
        return transform.BuildTransform(size.X, size.Y, origin - center);
    }

    internal bool IsDefault(string name) => PropertiesByName.TryGetValue(name, out var property)
        ? property.IsDefault(this)
        : throw new ArgumentException($"'{name}' isn't a CSS property.", nameof(name));

    internal void Inherit(Styles parent)
    {
        foreach (var property in Properties)
        {
            if (property.Inherited)
            {
                property.Inherit(this, parent);
            }
        }

        if (TextGradient.IsEmpty)
        {
            TextGradient = parent.TextGradient;
        }
    }

    internal void FillDefaults()
    {
        _overflowX ??= Overflow ?? OverflowMode.Visible;
        _overflowY ??= Overflow ?? OverflowMode.Visible;
        foreach (var property in Properties)
        {
            property.FillDefault(this);
        }
    }

    internal void ResolveCurrentColor(Styles? parent)
    {
        if (_fontColor is { IsCurrentColor: true })
        {
            _fontColor = parent?._fontColor ?? Color.Black;
        }

        var color = _fontColor ?? Color.Black;
        foreach (var property in Properties)
        {
            property.ResolveCurrentColor(this, color);
        }
    }

    internal void ApplyScale(float scale)
    {
        foreach (var property in Properties)
        {
            property.Scale(this, scale);
        }
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var property in Properties)
        {
            hash.Add(property.GetHashCode(this));
        }

        foreach (var transition in _transitions?.List ?? [])
        {
            hash.Add(transition);
        }

        hash.Add(BackgroundGradient);
        hash.Add(TextGradient);
        return hash.ToHashCode();
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => ReferenceEquals(this, obj);

    private void Set<T>(ref T field, T value)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        Dirty();
    }

    private void SetImage(ref Lazy<Texture?>? field, Texture? value)
    {
        if (field?.Value == value)
        {
            return;
        }

        field = new Lazy<Texture?>(value);
        Dirty();
    }
}
