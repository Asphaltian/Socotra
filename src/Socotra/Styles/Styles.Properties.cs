using System.Globalization;

namespace Socotra;

public partial class Styles
{
    private string? _content;
    private Length? _width;
    private Length? _minWidth;
    private Length? _maxWidth;
    private Length? _height;
    private Length? _minHeight;
    private Length? _maxHeight;
    private Length? _left;
    private Length? _top;
    private Length? _right;
    private Length? _bottom;
    private float? _opacity;
    private Color? _backgroundColor;
    private Length? _paddingLeft;
    private Length? _paddingTop;
    private Length? _paddingRight;
    private Length? _paddingBottom;
    private Length? _marginLeft;
    private Length? _marginTop;
    private Length? _marginRight;
    private Length? _marginBottom;
    private Length? _borderTopLeftRadius;
    private Length? _borderTopRightRadius;
    private Length? _borderBottomRightRadius;
    private Length? _borderBottomLeftRadius;
    private Length? _borderTopLeftRadiusV;
    private Length? _borderTopRightRadiusV;
    private Length? _borderBottomRightRadiusV;
    private Length? _borderBottomLeftRadiusV;
    private Length? _borderLeftWidth;
    private Length? _borderTopWidth;
    private Length? _borderRightWidth;
    private Length? _borderBottomWidth;
    private Socotra.BorderStyle? _borderStyle;
    private Color? _borderLeftColor;
    private Color? _borderTopColor;
    private Color? _borderRightColor;
    private Color? _borderBottomColor;
    private Length? _fontSize;
    private Color? _fontColor;
    private int? _fontWeight;
    private string? _fontFamily;
    private Color? _caretColor;
    private string? _cursor;
    private Socotra.PointerEvents? _pointerEvents;
    private string? _mixBlendMode;
    private Socotra.PositionMode? _position;
    private Socotra.OverflowMode? _overflowX;
    private Socotra.OverflowMode? _overflowY;
    private Socotra.FlexDirection? _flexDirection;
    private Socotra.Justify? _justifyContent;
    private Socotra.Align? _justifyItems;
    private Socotra.Align? _justifySelf;
    private Socotra.DisplayMode? _display;
    private string? _gridTemplateColumns;
    private string? _gridTemplateRows;
    private string? _gridAutoColumns;
    private string? _gridAutoRows;
    private Socotra.GridAutoFlow? _gridAutoFlow;
    private string? _gridColumnStart;
    private string? _gridColumnEnd;
    private string? _gridRowStart;
    private string? _gridRowEnd;
    private Socotra.Wrap? _flexWrap;
    private Socotra.Align? _alignContent;
    private Socotra.Align? _alignSelf;
    private Socotra.Align? _alignItems;
    private Length? _flexBasis;
    private float? _flexGrow;
    private float? _flexShrink;
    private Length? _rowGap;
    private Length? _columnGap;
    private float? _aspectRatio;
    private Socotra.TextAlign? _textAlign;
    private Socotra.TextOverflow? _textOverflow;
    private Socotra.FilterMode? _textFilter;
    private Socotra.WordBreak? _wordBreak;
    private Socotra.TextDecoration? _textDecorationLine;
    private Color? _textDecorationColor;
    private Length? _textDecorationThickness;
    private Socotra.TextSkipInk? _textDecorationSkipInk;
    private Socotra.TextDecorationStyle? _textDecorationStyle;
    private Length? _textUnderlineOffset;
    private Length? _textOverlineOffset;
    private Length? _textLineThroughOffset;
    private Socotra.FontStyle? _fontStyle;
    private Topten.RichTextKit.FontVariantNumeric? _fontVariantNumeric;
    private PanelTransform? _transform;
    private Socotra.TextTransform? _textTransform;
    private Length? _transformOriginX;
    private Length? _transformOriginY;
    private Length? _letterSpacing;
    private Length? _lineHeight;
    private Length? _wordSpacing;
    private Socotra.WhiteSpace? _whiteSpace;
    private int? _zIndex;
    private int? _order;
    private Length? _backdropFilterBlur;
    private Length? _backdropFilterBrightness;
    private Length? _backdropFilterContrast;
    private Length? _backdropFilterSaturate;
    private Length? _backdropFilterSepia;
    private Length? _backdropFilterInvert;
    private Length? _backdropFilterHueRotate;
    private Length? _filterBlur;
    private Length? _filterSaturate;
    private Length? _filterSepia;
    private Length? _filterBrightness;
    private Length? _filterHueRotate;
    private Length? _filterInvert;
    private Length? _filterContrast;
    private Color? _filterTint;
    private Length? _filterBorderWidth;
    private Color? _filterBorderColor;
    private Socotra.MaskMode? _maskMode;
    private Socotra.BackgroundRepeat? _maskRepeat;
    private Length? _maskSizeX;
    private Length? _maskSizeY;
    private Length? _maskPositionX;
    private Length? _maskPositionY;
    private Length? _maskAngle;
    private Socotra.MaskScope? _maskScope;
    private Length? _backgroundSizeX;
    private Length? _backgroundSizeY;
    private Length? _backgroundPositionX;
    private Length? _backgroundPositionY;
    private Socotra.BackgroundRepeat? _backgroundRepeat;
    private Socotra.BackgroundClip? _backgroundClip;
    private Length? _borderImageWidthLeft;
    private Length? _borderImageWidthRight;
    private Length? _borderImageWidthTop;
    private Length? _borderImageWidthBottom;
    private Socotra.BorderImageFill? _borderImageFill;
    private Socotra.BorderImageRepeat? _borderImageRepeat;
    private Color? _borderImageTint;
    private string? _backgroundBlendMode;
    private Color? _backgroundTint;
    private Length? _backgroundAngle;
    private Length? _textBackgroundAngle;
    private Length? _perspectiveOriginX;
    private Length? _perspectiveOriginY;
    private Color? _textStrokeColor;
    private Length? _textStrokeWidth;
    private Socotra.ImageRendering? _imageRendering;
    private float? _animationDelay;
    private string? _animationDirection;
    private float? _animationDuration;
    private string? _animationFillMode;
    private float? _animationIterationCount;
    private string? _animationName;
    private string? _animationPlayState;
    private string? _animationTimingFunction;
    private Socotra.FontSmooth? _fontSmooth;
    private Socotra.ObjectFit? _objectFit;
    private Length? _outlineWidth;
    private Color? _outlineColor;
    private Length? _outlineOffset;
    private Socotra.Isolation? _isolation;
    private Length? _scrollbarWidth;
    private Socotra.OverscrollBehavior? _overscrollBehaviorX;
    private Socotra.OverscrollBehavior? _overscrollBehaviorY;
    private Socotra.ScrollbarGutter? _scrollbarGutter;
    private Color? _scrollbarThumbColor;
    private Color? _scrollbarTrackColor;
    private BorderShape? _borderShape;
    private IReadOnlyList<Shadow>? _boxShadow;
    private IReadOnlyList<Shadow>? _textShadow;
    private IReadOnlyList<Shadow>? _filterDropShadow;
    private Lazy<Texture?>? _backgroundImage;
    private Lazy<Texture?>? _maskImage;
    private Lazy<Texture?>? _borderImageSource;

    private static readonly StyleProperty[] Properties =
    [
        new StyleProperty<string?>("content", static s => ref s._content, ParseQuoted, "") { FillsDefault = false },
        new LengthProperty("width", static s => ref s._width, Length.Undefined, Scaling.Ceiling),
        new LengthProperty("min-width", static s => ref s._minWidth, 0, Scaling.Ceiling),
        new LengthProperty("max-width", static s => ref s._maxWidth, Length.Undefined, Scaling.Ceiling),
        new LengthProperty("height", static s => ref s._height, Length.Undefined, Scaling.Ceiling),
        new LengthProperty("min-height", static s => ref s._minHeight, 0, Scaling.Ceiling),
        new LengthProperty("max-height", static s => ref s._maxHeight, Length.Undefined, Scaling.Ceiling),
        new LengthProperty("left", static s => ref s._left, Length.Undefined, Scaling.Ceiling),
        new LengthProperty("top", static s => ref s._top, Length.Undefined, Scaling.Ceiling),
        new LengthProperty("right", static s => ref s._right, Length.Undefined, Scaling.Ceiling),
        new LengthProperty("bottom", static s => ref s._bottom, Length.Undefined, Scaling.Ceiling),
        new NumberProperty("opacity", static s => ref s._opacity, 1, parse: ParseOpacity),
        new ColorProperty("background-color", static s => ref s._backgroundColor, Color.Transparent),
        new LengthProperty("padding-left", static s => ref s._paddingLeft, 0, Scaling.Ceiling),
        new LengthProperty("padding-top", static s => ref s._paddingTop, 0, Scaling.Ceiling),
        new LengthProperty("padding-right", static s => ref s._paddingRight, 0, Scaling.Ceiling),
        new LengthProperty("padding-bottom", static s => ref s._paddingBottom, 0, Scaling.Ceiling),
        new LengthProperty("margin-left", static s => ref s._marginLeft, 0, Scaling.Ceiling),
        new LengthProperty("margin-top", static s => ref s._marginTop, 0, Scaling.Ceiling),
        new LengthProperty("margin-right", static s => ref s._marginRight, 0, Scaling.Ceiling),
        new LengthProperty("margin-bottom", static s => ref s._marginBottom, 0, Scaling.Ceiling),
        new LengthProperty("border-top-left-radius", static s => ref s._borderTopLeftRadius, 0, Scaling.Ceiling),
        new LengthProperty("border-top-right-radius", static s => ref s._borderTopRightRadius, 0, Scaling.Ceiling),
        new LengthProperty("border-bottom-right-radius", static s => ref s._borderBottomRightRadius, 0, Scaling.Ceiling),
        new LengthProperty("border-bottom-left-radius", static s => ref s._borderBottomLeftRadius, 0, Scaling.Ceiling),
        new LengthProperty("border-top-left-radius-v", static s => ref s._borderTopLeftRadiusV, null, Scaling.Ceiling) { LerpFallback = static s => ref s._borderTopLeftRadius },
        new LengthProperty("border-top-right-radius-v", static s => ref s._borderTopRightRadiusV, null, Scaling.Ceiling) { LerpFallback = static s => ref s._borderTopRightRadius },
        new LengthProperty("border-bottom-right-radius-v", static s => ref s._borderBottomRightRadiusV, null, Scaling.Ceiling) { LerpFallback = static s => ref s._borderBottomRightRadius },
        new LengthProperty("border-bottom-left-radius-v", static s => ref s._borderBottomLeftRadiusV, null, Scaling.Ceiling) { LerpFallback = static s => ref s._borderBottomLeftRadius },
        new LengthProperty("border-left-width", static s => ref s._borderLeftWidth, 0, Scaling.Ceiling),
        new LengthProperty("border-top-width", static s => ref s._borderTopWidth, 0, Scaling.Ceiling),
        new LengthProperty("border-right-width", static s => ref s._borderRightWidth, 0, Scaling.Ceiling),
        new LengthProperty("border-bottom-width", static s => ref s._borderBottomWidth, 0, Scaling.Ceiling),
        new ValueProperty<Socotra.BorderStyle>("border-style", static s => ref s._borderStyle, ParseBorderStyle, Socotra.BorderStyle.Solid),
        new ColorProperty("border-left-color", static s => ref s._borderLeftColor, Color.White),
        new ColorProperty("border-top-color", static s => ref s._borderTopColor, Color.White),
        new ColorProperty("border-right-color", static s => ref s._borderRightColor, Color.White),
        new ColorProperty("border-bottom-color", static s => ref s._borderBottomColor, Color.White),
        new LengthProperty("font-size", static s => ref s._fontSize, Length.InitialFontSize, Scaling.Exact, inherited: true, parse: ParseFontSize),
        new ColorProperty("font-color", static s => ref s._fontColor, Color.Black, true),
        new IntegerProperty("font-weight", static s => ref s._fontWeight, 400, inherited: true, parse: ParseFontWeight),
        new StyleProperty<string?>("font-family", static s => ref s._fontFamily, ParseFontFamily, "Arial", true),
        new ColorProperty("caret-color", static s => ref s._caretColor, null),
        new StyleProperty<string?>("cursor", static s => ref s._cursor, ParseQuoted, "auto", true),
        new EnumProperty<Socotra.PointerEvents>("pointer-events", static s => ref s._pointerEvents, Socotra.PointerEvents.None, true, PointerEventsNames),
        new StyleProperty<string?>("mix-blend-mode", static s => ref s._mixBlendMode, ParseQuoted, "default", true),
        new EnumProperty<Socotra.PositionMode>("position", static s => ref s._position, Socotra.PositionMode.Static, false, PositionNames),
        new EnumProperty<Socotra.OverflowMode>("overflow-x", static s => ref s._overflowX, Socotra.OverflowMode.Visible, false, OverflowNames),
        new EnumProperty<Socotra.OverflowMode>("overflow-y", static s => ref s._overflowY, Socotra.OverflowMode.Visible, false, OverflowNames),
        new EnumProperty<Socotra.FlexDirection>("flex-direction", static s => ref s._flexDirection, Socotra.FlexDirection.Row, false, FlexDirectionNames),
        new EnumProperty<Socotra.Justify>("justify-content", static s => ref s._justifyContent, Socotra.Justify.Stretch, false, JustifyNames),
        new EnumProperty<Socotra.Align>("justify-items", static s => ref s._justifyItems, Socotra.Align.Auto, false, AlignNames),
        new EnumProperty<Socotra.Align>("justify-self", static s => ref s._justifySelf, Socotra.Align.Auto, false, AlignNames),
        new EnumProperty<Socotra.DisplayMode>("display", static s => ref s._display, Socotra.DisplayMode.Flex, false, DisplayNames),
        new StyleProperty<string?>("grid-template-columns", static s => ref s._gridTemplateColumns, ParseQuoted, "none"),
        new StyleProperty<string?>("grid-template-rows", static s => ref s._gridTemplateRows, ParseQuoted, "none"),
        new StyleProperty<string?>("grid-auto-columns", static s => ref s._gridAutoColumns, ParseQuoted, "auto"),
        new StyleProperty<string?>("grid-auto-rows", static s => ref s._gridAutoRows, ParseQuoted, "auto"),
        new ValueProperty<Socotra.GridAutoFlow>("grid-auto-flow", static s => ref s._gridAutoFlow, ParseGridAutoFlow, Socotra.GridAutoFlow.Row),
        new StyleProperty<string?>("grid-column-start", static s => ref s._gridColumnStart, ParseQuoted, "auto"),
        new StyleProperty<string?>("grid-column-end", static s => ref s._gridColumnEnd, ParseQuoted, "auto"),
        new StyleProperty<string?>("grid-row-start", static s => ref s._gridRowStart, ParseQuoted, "auto"),
        new StyleProperty<string?>("grid-row-end", static s => ref s._gridRowEnd, ParseQuoted, "auto"),
        new EnumProperty<Socotra.Wrap>("flex-wrap", static s => ref s._flexWrap, Socotra.Wrap.NoWrap, false, WrapNames),
        new EnumProperty<Socotra.Align>("align-content", static s => ref s._alignContent, Socotra.Align.Auto, false, AlignNames),
        new EnumProperty<Socotra.Align>("align-self", static s => ref s._alignSelf, Socotra.Align.Auto, false, AlignNames),
        new EnumProperty<Socotra.Align>("align-items", static s => ref s._alignItems, Socotra.Align.Stretch, false, AlignNames),
        new LengthProperty("flex-basis", static s => ref s._flexBasis, Length.Auto, Scaling.Exact),
        new NumberProperty("flex-grow", static s => ref s._flexGrow, 0),
        new NumberProperty("flex-shrink", static s => ref s._flexShrink, 1),
        new LengthProperty("row-gap", static s => ref s._rowGap, Length.Auto, Scaling.Ceiling, parse: ParseGap),
        new LengthProperty("column-gap", static s => ref s._columnGap, Length.Auto, Scaling.Ceiling, parse: ParseGap),
        new ValueProperty<float>("aspect-ratio", static s => ref s._aspectRatio, ParseAspectRatio, float.NaN),
        new EnumProperty<Socotra.TextAlign>("text-align", static s => ref s._textAlign, Socotra.TextAlign.Left, true, TextAlignNames),
        new EnumProperty<Socotra.TextOverflow>("text-overflow", static s => ref s._textOverflow, Socotra.TextOverflow.None, true, TextOverflowNames),
        new EnumProperty<Socotra.FilterMode>("text-filter", static s => ref s._textFilter, Socotra.FilterMode.Bilinear, true, TextFilterNames),
        new EnumProperty<Socotra.WordBreak>("word-break", static s => ref s._wordBreak, Socotra.WordBreak.Normal, true, WordBreakNames),
        new ValueProperty<Socotra.TextDecoration>("text-decoration-line", static s => ref s._textDecorationLine, ParseTextDecorationLine, Socotra.TextDecoration.None, true),
        new ColorProperty("text-decoration-color", static s => ref s._textDecorationColor, Color.White, true),
        new LengthProperty("text-decoration-thickness", static s => ref s._textDecorationThickness, 1, inherited: true),
        new EnumProperty<Socotra.TextSkipInk>("text-decoration-skip-ink", static s => ref s._textDecorationSkipInk, Socotra.TextSkipInk.All, true, SkipInkNames),
        new EnumProperty<Socotra.TextDecorationStyle>("text-decoration-style", static s => ref s._textDecorationStyle, Socotra.TextDecorationStyle.Solid, true, TextDecorationStyleNames),
        new LengthProperty("text-underline-offset", static s => ref s._textUnderlineOffset, 0, inherited: true),
        new LengthProperty("text-overline-offset", static s => ref s._textOverlineOffset, 0, inherited: true),
        new LengthProperty("text-line-through-offset", static s => ref s._textLineThroughOffset, 0, inherited: true),
        new ValueProperty<Socotra.FontStyle>("font-style", static s => ref s._fontStyle, ParseFontStyle, Socotra.FontStyle.None, true),
        new EnumProperty<Topten.RichTextKit.FontVariantNumeric>("font-variant-numeric", static s => ref s._fontVariantNumeric, Topten.RichTextKit.FontVariantNumeric.Normal, true, FontVariantNumericNames),
        new TransformProperty("transform", static s => ref s._transform),
        new EnumProperty<Socotra.TextTransform>("text-transform", static s => ref s._textTransform, Socotra.TextTransform.None, true, TextTransformNames),
        new LengthProperty("transform-origin-x", static s => ref s._transformOriginX, Length.Percent(50), Scaling.Ceiling),
        new LengthProperty("transform-origin-y", static s => ref s._transformOriginY, Length.Percent(50), Scaling.Ceiling),
        new LengthProperty("letter-spacing", static s => ref s._letterSpacing, Length.Percent(0), Scaling.Exact, inherited: true, parse: ParseSpacing),
        new LengthProperty("line-height", static s => ref s._lineHeight, Length.Percent(100), Scaling.Exact, inherited: true, parse: ParseLineHeight),
        new LengthProperty("word-spacing", static s => ref s._wordSpacing, Length.Percent(0), inherited: true, parse: ParseSpacing),
        new EnumProperty<Socotra.WhiteSpace>("white-space", static s => ref s._whiteSpace, Socotra.WhiteSpace.Normal, true, WhiteSpaceNames),
        new IntegerProperty("z-index", static s => ref s._zIndex, 0),
        new IntegerProperty("order", static s => ref s._order, 0),
        new LengthProperty("backdrop-filter-blur", static s => ref s._backdropFilterBlur, 0),
        new LengthProperty("backdrop-filter-brightness", static s => ref s._backdropFilterBrightness, 1),
        new LengthProperty("backdrop-filter-contrast", static s => ref s._backdropFilterContrast, 1),
        new LengthProperty("backdrop-filter-saturate", static s => ref s._backdropFilterSaturate, 1),
        new LengthProperty("backdrop-filter-sepia", static s => ref s._backdropFilterSepia, 0),
        new LengthProperty("backdrop-filter-invert", static s => ref s._backdropFilterInvert, 0),
        new LengthProperty("backdrop-filter-hue-rotate", static s => ref s._backdropFilterHueRotate, 0),
        new LengthProperty("filter-blur", static s => ref s._filterBlur, 0),
        new LengthProperty("filter-saturate", static s => ref s._filterSaturate, 1),
        new LengthProperty("filter-sepia", static s => ref s._filterSepia, 0),
        new LengthProperty("filter-brightness", static s => ref s._filterBrightness, 1),
        new LengthProperty("filter-hue-rotate", static s => ref s._filterHueRotate, 0),
        new LengthProperty("filter-invert", static s => ref s._filterInvert, 0),
        new LengthProperty("filter-contrast", static s => ref s._filterContrast, 1),
        new ColorProperty("filter-tint", static s => ref s._filterTint, Color.White),
        new LengthProperty("filter-border-width", static s => ref s._filterBorderWidth, 0),
        new ColorProperty("filter-border-color", static s => ref s._filterBorderColor, Color.White),
        new EnumProperty<Socotra.MaskMode>("mask-mode", static s => ref s._maskMode, Socotra.MaskMode.MatchSource, false, MaskModeNames),
        new EnumProperty<Socotra.BackgroundRepeat>("mask-repeat", static s => ref s._maskRepeat, Socotra.BackgroundRepeat.Repeat, false, RepeatNames),
        new LengthProperty("mask-size-x", static s => ref s._maskSizeX, Length.Undefined),
        new LengthProperty("mask-size-y", static s => ref s._maskSizeY, Length.Undefined),
        new LengthProperty("mask-position-x", static s => ref s._maskPositionX, Length.Percent(0)),
        new LengthProperty("mask-position-y", static s => ref s._maskPositionY, Length.Percent(0)),
        new LengthProperty("mask-angle", static s => ref s._maskAngle, 0),
        new EnumProperty<Socotra.MaskScope>("mask-scope", static s => ref s._maskScope, Socotra.MaskScope.Default, false, MaskScopeNames),
        new LengthProperty("background-size-x", static s => ref s._backgroundSizeX, Length.Undefined),
        new LengthProperty("background-size-y", static s => ref s._backgroundSizeY, Length.Undefined),
        new LengthProperty("background-position-x", static s => ref s._backgroundPositionX, Length.Percent(0)),
        new LengthProperty("background-position-y", static s => ref s._backgroundPositionY, Length.Percent(0)),
        new EnumProperty<Socotra.BackgroundRepeat>("background-repeat", static s => ref s._backgroundRepeat, Socotra.BackgroundRepeat.Repeat, false, RepeatNames),
        new EnumProperty<Socotra.BackgroundClip>("background-clip", static s => ref s._backgroundClip, Socotra.BackgroundClip.BorderBox, false, BackgroundClipNames),
        new LengthProperty("border-image-width-left", static s => ref s._borderImageWidthLeft, 1),
        new LengthProperty("border-image-width-right", static s => ref s._borderImageWidthRight, 1),
        new LengthProperty("border-image-width-top", static s => ref s._borderImageWidthTop, 1),
        new LengthProperty("border-image-width-bottom", static s => ref s._borderImageWidthBottom, 1),
        new ValueProperty<Socotra.BorderImageFill>("border-image-fill", static s => ref s._borderImageFill, null, Socotra.BorderImageFill.Unfilled),
        new ValueProperty<Socotra.BorderImageRepeat>("border-image-repeat", static s => ref s._borderImageRepeat, null, Socotra.BorderImageRepeat.Stretch),
        new ColorProperty("border-image-tint", static s => ref s._borderImageTint, Color.White),
        new StyleProperty<string?>("background-blend-mode", static s => ref s._backgroundBlendMode, ParseQuoted, "normal"),
        new ColorProperty("background-tint", static s => ref s._backgroundTint, Color.White),
        new LengthProperty("background-angle", static s => ref s._backgroundAngle, 0),
        new LengthProperty("text-background-angle", static s => ref s._textBackgroundAngle, 0),
        new LengthProperty("perspective-origin-x", static s => ref s._perspectiveOriginX, Length.Percent(50)),
        new LengthProperty("perspective-origin-y", static s => ref s._perspectiveOriginY, Length.Percent(50)),
        new ColorProperty("text-stroke-color", static s => ref s._textStrokeColor, Color.White, true),
        new LengthProperty("text-stroke-width", static s => ref s._textStrokeWidth, 0, Scaling.Exact, inherited: true),
        new EnumProperty<Socotra.ImageRendering>("image-rendering", static s => ref s._imageRendering, Socotra.ImageRendering.Anisotropic, true, ImageRenderingNames),
        new ValueProperty<float>("animation-delay", static s => ref s._animationDelay, ParseSeconds, 0) { FillsDefault = false },
        new StyleProperty<string?>("animation-direction", static s => ref s._animationDirection, Keyword("normal", "reverse", "alternate", "alternate-reverse"), "normal"),
        new ValueProperty<float>("animation-duration", static s => ref s._animationDuration, ParseSeconds, 0) { FillsDefault = false },
        new StyleProperty<string?>("animation-fill-mode", static s => ref s._animationFillMode, Keyword("none", "forwards", "backwards", "both"), "none"),
        new NumberProperty("animation-iteration-count", static s => ref s._animationIterationCount, 1, parse: ParseIterationCount),
        new StyleProperty<string?>("animation-name", static s => ref s._animationName, ParseQuoted, "none"),
        new StyleProperty<string?>("animation-play-state", static s => ref s._animationPlayState, Keyword("running", "paused"), "running"),
        new StyleProperty<string?>("animation-timing-function", static s => ref s._animationTimingFunction, ParseQuoted, "ease"),
        new EnumProperty<Socotra.FontSmooth>("font-smooth", static s => ref s._fontSmooth, Socotra.FontSmooth.Auto, true, FontSmoothNames),
        new EnumProperty<Socotra.ObjectFit>("object-fit", static s => ref s._objectFit, Socotra.ObjectFit.Cover, false, ObjectFitNames),
        new LengthProperty("outline-width", static s => ref s._outlineWidth, 0, Scaling.Ceiling),
        new ColorProperty("outline-color", static s => ref s._outlineColor, Color.Transparent),
        new LengthProperty("outline-offset", static s => ref s._outlineOffset, 0, Scaling.Ceiling),
        new EnumProperty<Socotra.Isolation>("isolation", static s => ref s._isolation, Socotra.Isolation.Auto, false, IsolationNames),
        new LengthProperty("scrollbar-width", static s => ref s._scrollbarWidth, 0, Scaling.Ceiling, inherited: true, parse: ParseScrollbarWidth),
        new EnumProperty<Socotra.OverscrollBehavior>("overscroll-behavior-x", static s => ref s._overscrollBehaviorX, Socotra.OverscrollBehavior.Auto, false, OverscrollNames),
        new EnumProperty<Socotra.OverscrollBehavior>("overscroll-behavior-y", static s => ref s._overscrollBehaviorY, Socotra.OverscrollBehavior.Auto, false, OverscrollNames),
        new ValueProperty<Socotra.ScrollbarGutter>("scrollbar-gutter", static s => ref s._scrollbarGutter, ParseScrollbarGutter, Socotra.ScrollbarGutter.Auto),
        new ColorProperty("scrollbar-thumb-color", static s => ref s._scrollbarThumbColor, null, true),
        new ColorProperty("scrollbar-track-color", static s => ref s._scrollbarTrackColor, null, true),
        new BorderShapeProperty("border-shape", static s => ref s._borderShape),
        new ShadowProperty("box-shadow", static s => ref s._boxShadow),
        new ShadowProperty("text-shadow", static s => ref s._textShadow, true),
        new ShadowProperty("filter-drop-shadow", static s => ref s._filterDropShadow),
        new StyleProperty<Lazy<Texture?>?>("background-image", static s => ref s._backgroundImage, null, null),
        new StyleProperty<Lazy<Texture?>?>("mask-image", static s => ref s._maskImage, null, null),
        new StyleProperty<Lazy<Texture?>?>("border-image-source", static s => ref s._borderImageSource, null, null),
    ];

    private static readonly Dictionary<string, StyleProperty> PropertiesByName = Properties.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

    private static readonly Styles InitialValues = CreateInitialValues();

    private static (string, Socotra.DisplayMode)[] DisplayNames =>
    [
        ("none", Socotra.DisplayMode.None), ("flex", Socotra.DisplayMode.Flex), ("contents", Socotra.DisplayMode.Contents), ("block", Socotra.DisplayMode.Block),
        ("flow-root", Socotra.DisplayMode.Block), ("grid", Socotra.DisplayMode.Grid), ("inline", Socotra.DisplayMode.Inline),
    ];

    private static (string, Socotra.PointerEvents)[] PointerEventsNames => [("none", Socotra.PointerEvents.None), ("all", Socotra.PointerEvents.All)];

    private static (string, Socotra.PositionMode)[] PositionNames =>
    [
        ("static", Socotra.PositionMode.Static), ("absolute", Socotra.PositionMode.Absolute), ("fixed", Socotra.PositionMode.Fixed), ("relative", Socotra.PositionMode.Relative),
    ];

    private static (string, Socotra.OverflowMode)[] OverflowNames =>
    [
        ("hidden", Socotra.OverflowMode.Hidden), ("auto", Socotra.OverflowMode.Scroll), ("scroll", Socotra.OverflowMode.Scroll), ("clip", Socotra.OverflowMode.Clip),
        ("clip-whole", Socotra.OverflowMode.ClipWhole), ("visible", Socotra.OverflowMode.Visible),
    ];

    private static (string, Socotra.FlexDirection)[] FlexDirectionNames =>
    [
        ("column", Socotra.FlexDirection.Column), ("column-reverse", Socotra.FlexDirection.ColumnReverse), ("row", Socotra.FlexDirection.Row), ("row-reverse", Socotra.FlexDirection.RowReverse),
    ];

    private static (string, Socotra.Justify)[] JustifyNames =>
    [
        ("flex-start", Socotra.Justify.FlexStart), ("start", Socotra.Justify.FlexStart), ("left", Socotra.Justify.FlexStart), ("normal", Socotra.Justify.Stretch),
        ("stretch", Socotra.Justify.Stretch), ("center", Socotra.Justify.Center), ("flex-end", Socotra.Justify.FlexEnd), ("end", Socotra.Justify.FlexEnd),
        ("right", Socotra.Justify.FlexEnd), ("space-between", Socotra.Justify.SpaceBetween), ("space-around", Socotra.Justify.SpaceAround), ("space-evenly", Socotra.Justify.SpaceEvenly),
    ];

    private static (string, Socotra.Align)[] AlignNames =>
    [
        ("auto", Socotra.Align.Auto), ("flex-end", Socotra.Align.FlexEnd), ("end", Socotra.Align.FlexEnd), ("self-end", Socotra.Align.FlexEnd),
        ("flex-start", Socotra.Align.FlexStart), ("start", Socotra.Align.FlexStart), ("self-start", Socotra.Align.FlexStart), ("center", Socotra.Align.Center),
        ("stretch", Socotra.Align.Stretch), ("normal", Socotra.Align.Stretch), ("space-between", Socotra.Align.SpaceBetween), ("space-around", Socotra.Align.SpaceAround),
        ("space-evenly", Socotra.Align.SpaceEvenly), ("baseline", Socotra.Align.Baseline),
    ];

    private static (string, Socotra.Wrap)[] WrapNames => [("nowrap", Socotra.Wrap.NoWrap), ("wrap", Socotra.Wrap.Wrap), ("wrap-reverse", Socotra.Wrap.WrapReverse)];

    private static (string, Socotra.TextAlign)[] TextAlignNames =>
    [
        ("center", Socotra.TextAlign.Center), ("left", Socotra.TextAlign.Left), ("start", Socotra.TextAlign.Left), ("right", Socotra.TextAlign.Right),
        ("end", Socotra.TextAlign.Right), ("justify", Socotra.TextAlign.Justify),
    ];

    private static (string, Socotra.TextOverflow)[] TextOverflowNames => [("ellipsis", Socotra.TextOverflow.Ellipsis), ("clip", Socotra.TextOverflow.Clip)];

    private static (string, Socotra.FilterMode)[] TextFilterNames =>
    [
        ("linear", Socotra.FilterMode.Bilinear), ("bilinear", Socotra.FilterMode.Bilinear), ("point", Socotra.FilterMode.Point), ("trilinear", Socotra.FilterMode.Trilinear),
        ("anisotropic", Socotra.FilterMode.Anisotropic),
    ];

    private static (string, Socotra.WordBreak)[] WordBreakNames =>
    [
        ("normal", Socotra.WordBreak.Normal), ("break-word", Socotra.WordBreak.Normal), ("keep-all", Socotra.WordBreak.Normal), ("break-all", Socotra.WordBreak.BreakAll),
    ];

    private static (string, TextSkipInk)[] SkipInkNames => [("auto", TextSkipInk.All), ("all", TextSkipInk.All), ("none", TextSkipInk.None)];

    private static (string, Socotra.TextDecorationStyle)[] TextDecorationStyleNames =>
    [
        ("solid", Socotra.TextDecorationStyle.Solid), ("double", Socotra.TextDecorationStyle.Double), ("dotted", Socotra.TextDecorationStyle.Dotted),
        ("dashed", Socotra.TextDecorationStyle.Dashed), ("wavy", Socotra.TextDecorationStyle.Wavy),
    ];

    private static (string, Topten.RichTextKit.FontVariantNumeric)[] FontVariantNumericNames =>
    [
        ("normal", Topten.RichTextKit.FontVariantNumeric.Normal), ("tabular-nums", Topten.RichTextKit.FontVariantNumeric.TabularNums),
    ];

    private static (string, Socotra.TextTransform)[] TextTransformNames =>
    [
        ("capitalize", Socotra.TextTransform.Capitalize), ("uppercase", Socotra.TextTransform.Uppercase), ("lowercase", Socotra.TextTransform.Lowercase), ("none", Socotra.TextTransform.None),
    ];

    private static (string, Socotra.WhiteSpace)[] WhiteSpaceNames =>
    [
        ("normal", Socotra.WhiteSpace.Normal), ("nowrap", Socotra.WhiteSpace.NoWrap), ("pre-line", Socotra.WhiteSpace.PreLine), ("pre", Socotra.WhiteSpace.Pre),
        ("pre-wrap", Socotra.WhiteSpace.PreWrap), ("break-spaces", Socotra.WhiteSpace.BreakSpaces),
    ];

    private static (string, Socotra.MaskMode)[] MaskModeNames =>
    [
        ("match-source", Socotra.MaskMode.MatchSource), ("alpha", Socotra.MaskMode.Alpha), ("luminance", Socotra.MaskMode.Luminance),
    ];

    private static (string, Socotra.BackgroundRepeat)[] RepeatNames =>
    [
        ("no-repeat", Socotra.BackgroundRepeat.NoRepeat), ("repeat-x", Socotra.BackgroundRepeat.RepeatX), ("repeat-y", Socotra.BackgroundRepeat.RepeatY),
        ("repeat", Socotra.BackgroundRepeat.Repeat), ("round", Socotra.BackgroundRepeat.Clamp), ("clamp", Socotra.BackgroundRepeat.Clamp),
    ];

    private static (string, Socotra.MaskScope)[] MaskScopeNames => [("default", Socotra.MaskScope.Default), ("filter", Socotra.MaskScope.Filter)];

    private static (string, Socotra.BackgroundClip)[] BackgroundClipNames =>
    [
        ("border-box", Socotra.BackgroundClip.BorderBox), ("padding-box", Socotra.BackgroundClip.PaddingBox), ("content-box", Socotra.BackgroundClip.ContentBox),
        ("text", Socotra.BackgroundClip.Text),
    ];

    private static (string, Socotra.ImageRendering)[] ImageRenderingNames =>
    [
        ("auto", Socotra.ImageRendering.Anisotropic), ("anisotropic", Socotra.ImageRendering.Anisotropic), ("bilinear", Socotra.ImageRendering.Bilinear),
        ("trilinear", Socotra.ImageRendering.Trilinear), ("point", Socotra.ImageRendering.Point), ("pixelated", Socotra.ImageRendering.Point),
        ("crisp-edges", Socotra.ImageRendering.Point), ("nearest-neighbor", Socotra.ImageRendering.Point),
    ];

    private static (string, Socotra.FontSmooth)[] FontSmoothNames =>
    [
        ("none", Socotra.FontSmooth.Never), ("never", Socotra.FontSmooth.Never), ("auto", Socotra.FontSmooth.Auto), ("always", Socotra.FontSmooth.Always),
    ];

    private static (string, Socotra.ObjectFit)[] ObjectFitNames =>
    [
        ("fill", Socotra.ObjectFit.Fill), ("contain", Socotra.ObjectFit.Contain), ("scale-down", Socotra.ObjectFit.Contain), ("cover", Socotra.ObjectFit.Cover),
        ("none", Socotra.ObjectFit.None),
    ];

    private static (string, Socotra.Isolation)[] IsolationNames => [("auto", Socotra.Isolation.Auto), ("isolate", Socotra.Isolation.Isolate)];

    private static (string, OverscrollBehavior)[] OverscrollNames =>
    [
        ("auto", OverscrollBehavior.Auto), ("contain", OverscrollBehavior.Contain), ("none", OverscrollBehavior.None),
    ];

    internal static float? ParseFloat(string value) =>
        Translation.TryParseFloat(value, out var result) ? result : null;

    internal static int? ParseInt(string value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : null;

    private static string? ParseQuoted(string value) =>
        value.Length >= 2 && value[0] == value[^1] && value[0] is '"' or '\'' ? value[1..^1] : value;

    private static float? ParseSeconds(string value)
    {
        var p = new Parse(value);
        return p.TryReadTime(out var milliseconds) && p.SkipWhitespaceAndNewlines().IsEnd ? milliseconds / 1000.0f : null;
    }

    private static float? ParseAspectRatio(string value)
    {
        if (value.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            return float.NaN;
        }

        if (value.StartsWith("auto", StringComparison.OrdinalIgnoreCase))
        {
            value = value[4..].Trim();
            if (value.Length == 0)
            {
                return float.NaN;
            }
        }

        return value.Split([' ', ':', '/'], StringSplitOptions.RemoveEmptyEntries) switch
        {
            [var ratio] => ParseFloat(ratio),
            [var width, var height] => ParseFloat(width) / ParseFloat(height),
            _ => null,
        };
    }

    private static float? ParseOpacity(string value) => value.EndsWith('%') ? ParseFloat(value[..^1]) / 100.0f : ParseFloat(value);

    private static float? ParseIterationCount(string value) => value == "infinite" ? float.PositiveInfinity : ParseFloat(value);

    private static readonly Dictionary<string, float> FontSizeKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["xx-small"] = 10,
        ["x-small"] = 12,
        ["small"] = 14,
        ["medium"] = 16,
        ["large"] = 18,
        ["x-large"] = 24,
        ["xx-large"] = 32,
        ["xxx-large"] = 48,
    };

    private static Length? ParseFontSize(string value) => FontSizeKeywords.TryGetValue(value, out var pixels) ? pixels : Length.Parse(value);

    private static Length? ParseSpacing(string value) => value.Equals("normal", StringComparison.OrdinalIgnoreCase) ? 0 : Length.Parse(value);

    private static Length? ParseLineHeight(string value) => value == "normal" ? Length.Percent(100)
        : ParseFloat(value) is { } multiplier ? Length.Percent(multiplier * 100)
        : Length.Parse(value);

    private static Length? ParseScrollbarWidth(string value) => value switch
    {
        "none" => 0,
        "thin" => 8,
        _ => Length.Parse(value),
    };

    private static int? ParseFontWeight(string value) => ParseInt(value) ?? value.ToLowerInvariant() switch
    {
        "hairline" or "thin" => 100,
        "ultralight" or "extralight" => 200,
        "light" => 300,
        "regular" or "normal" => 400,
        "medium" => 500,
        "demibold" or "semibold" => 600,
        "bold" => 700,
        "ultrabold" or "extrabold" => 800,
        "heavy" or "black" => 900,
        "extrablack" or "ultrablack" => 950,
        "bolder" => 900,
        "lighter" => 200,
        _ => null,
    };

    private static string? ParseFontFamily(string value)
    {
        var comma = value.IndexOf(',');
        var family = ParseQuoted((comma < 0 ? value : value[..comma]).Trim())!;
        return family.ToLowerInvariant() switch
        {
            "sans-serif" or "system-ui" or "ui-sans-serif" => "Arial",
            "serif" or "ui-serif" => "Times New Roman",
            "monospace" or "ui-monospace" => "Consolas",
            "ui-rounded" => "Poppins",
            "cursive" => "Comic Sans MS",
            "fantasy" => "Impact",
            _ => family,
        };
    }

    private static Socotra.BorderStyle? ParseBorderStyle(string value)
    {
        var p = new Parse(value);
        return p.TryReadLineStyle(out var style) && p.SkipWhitespaceAndNewlines().IsEnd ? Enum.Parse<Socotra.BorderStyle>(style, true) : null;
    }

    private static Length? ParseGap(string value) => value.Trim().Equals("normal", StringComparison.OrdinalIgnoreCase) ? Length.Pixels(0) : Length.Parse(value);

    private static Func<string, string?> Keyword(params string[] keywords) =>
        value => keywords.FirstOrDefault(keyword => keyword.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase));

    private static Socotra.FontStyle? ParseFontStyle(string value) => value.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries) switch
    {
        ["normal"] => Socotra.FontStyle.None,
        ["italic"] => Socotra.FontStyle.Italic,
        ["oblique"] => Socotra.FontStyle.Oblique,
        ["oblique", var angle] when angle.EndsWith("deg", StringComparison.Ordinal) && ParseFloat(angle[..^3]) is not null => Socotra.FontStyle.Oblique,
        _ => null,
    };

    private static TextDecoration? ParseTextDecorationLine(string value)
    {
        var line = TextDecoration.None;
        var words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var word in words)
        {
            switch (word.ToLowerInvariant())
            {
                case "none" when words.Length == 1:
                    break;
                case "underline":
                    line |= TextDecoration.Underline;
                    break;
                case "line-through":
                    line |= TextDecoration.LineThrough;
                    break;
                case "overline":
                    line |= TextDecoration.Overline;
                    break;
                default:
                    return null;
            }
        }

        return words.Length > 0 ? line : null;
    }

    private static Socotra.GridAutoFlow? ParseGridAutoFlow(string value) =>
        Layout.GridParser.TryParseAutoFlow(value, out var flow) ? (Socotra.GridAutoFlow)flow : null;

    private static Socotra.ScrollbarGutter? ParseScrollbarGutter(string value) => SplitValues(value) switch
    {
        ["auto"] => Socotra.ScrollbarGutter.Auto,
        ["stable"] => Socotra.ScrollbarGutter.Stable,
        ["stable", "both-edges"] or ["both-edges", "stable"] => Socotra.ScrollbarGutter.StableBothEdges,
        _ => null,
    };
}
