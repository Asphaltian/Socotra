namespace Socotra;

/// <summary>Values for <c>overflow</c>: what happens to children that don't fit inside the panel.</summary>
public enum OverflowMode
{
    /// <summary><c>visible</c>: they're drawn outside the panel.</summary>
    Visible,

    /// <summary><c>hidden</c>: they're cut off at the panel's edge.</summary>
    Hidden,

    /// <summary><c>scroll</c> or <c>auto</c>: they're cut off, and the panel scrolls.</summary>
    Scroll,

    /// <summary><c>clip</c>: they're cut off, and the panel can't scroll.</summary>
    Clip,

    /// <summary><c>clip-whole</c>: children that reach outside the panel are hidden entirely instead of cut off.</summary>
    ClipWhole,
}

/// <summary>Values for <c>align-items</c>, <c>align-self</c>, <c>align-content</c>, <c>justify-items</c> and <c>justify-self</c>: where things sit on an axis.</summary>
public enum Align
{
    /// <summary><c>auto</c>: do what the parent says.</summary>
    Auto,

    /// <summary><c>flex-start</c>, <c>start</c> or <c>self-start</c>: at the start.</summary>
    FlexStart,

    /// <summary><c>center</c>: in the middle.</summary>
    Center,

    /// <summary><c>flex-end</c>, <c>end</c> or <c>self-end</c>: at the end.</summary>
    FlexEnd,

    /// <summary><c>stretch</c> or <c>normal</c>: stretched to fill the space.</summary>
    Stretch,

    /// <summary><c>baseline</c>: lined up so the first lines of text sit on the same line.</summary>
    Baseline,

    /// <summary><c>space-between</c>: spread out, with the first and last against the edges.</summary>
    SpaceBetween,

    /// <summary><c>space-around</c>: spread out, with half as much space at the edges as between each one.</summary>
    SpaceAround,

    /// <summary><c>space-evenly</c>: spread out, with the same space at the edges as between each one.</summary>
    SpaceEvenly,
}

/// <summary>Values for <c>position</c>: how the panel is placed.</summary>
public enum PositionMode
{
    /// <summary><c>static</c>: <c>top</c>, <c>right</c>, <c>bottom</c> and <c>left</c> have no effect.</summary>
    Static,

    /// <summary><c>relative</c>: offset from where the panel would otherwise be.</summary>
    Relative,

    /// <summary><c>absolute</c>: placed inside the parent without affecting its siblings.</summary>
    Absolute,

    /// <summary><c>fixed</c>: placed against the root panel, ignoring ancestor scrolling, transforms and clipping.</summary>
    Fixed,
}

/// <summary>Values for <c>flex-direction</c>: which way children are laid out.</summary>
public enum FlexDirection
{
    /// <summary><c>column</c>: top to bottom.</summary>
    Column,

    /// <summary><c>column-reverse</c>: bottom to top.</summary>
    ColumnReverse,

    /// <summary><c>row</c>: left to right.</summary>
    Row,

    /// <summary><c>row-reverse</c>: right to left.</summary>
    RowReverse,
}

/// <summary>Values for <c>justify-content</c>: where children go along the main axis.</summary>
public enum Justify
{
    /// <summary><c>flex-start</c>, <c>start</c> or <c>left</c>: packed at the start.</summary>
    FlexStart,

    /// <summary><c>center</c>: packed in the middle.</summary>
    Center,

    /// <summary><c>flex-end</c>, <c>end</c> or <c>right</c>: packed at the end.</summary>
    FlexEnd,

    /// <summary><c>space-between</c>: spread out, with the first and last against the edges.</summary>
    SpaceBetween,

    /// <summary><c>space-around</c>: spread out, with half as much space at the edges as between each one.</summary>
    SpaceAround,

    /// <summary><c>space-evenly</c>: spread out, with the same space at the edges as between each one.</summary>
    SpaceEvenly,

    /// <summary><c>stretch</c> or <c>normal</c>, the default. In a flex container it acts like <see cref="FlexStart"/>; in a grid, <c>auto</c> columns and rows stretch to fill the space.</summary>
    Stretch,
}

/// <summary>Values for <c>display</c>: how a panel lays out its children, or whether it shows at all.</summary>
public enum DisplayMode
{
    /// <summary><c>flex</c>, the default: children go in a row or a column, as <c>flex-direction</c> says.</summary>
    Flex,

    /// <summary><c>none</c>: the panel isn't shown or laid out.</summary>
    None,

    /// <summary><c>contents</c>: the panel's children are laid out as if they were its parent's.</summary>
    Contents,

    /// <summary><c>block</c> or <c>flow-root</c>: children stack vertically and fill the width.</summary>
    Block,

    /// <summary><c>grid</c>: children go into the rows and columns of a grid.</summary>
    Grid,

    /// <summary><c>inline</c>: text that flows inside a block's paragraph.</summary>
    Inline,
}

/// <summary>Values for <c>grid-auto-flow</c>: where grid items without a set position go.</summary>
public enum GridAutoFlow
{
    /// <summary><c>row</c>: fill each row in turn.</summary>
    Row,

    /// <summary><c>column</c>: fill each column in turn.</summary>
    Column,

    /// <summary><c>row dense</c>: like <see cref="Row"/>, filling holes left earlier.</summary>
    RowDense,

    /// <summary><c>column dense</c>: like <see cref="Column"/>, filling holes left earlier.</summary>
    ColumnDense,
}

/// <summary>Values for <c>flex-wrap</c>: whether children that don't fit start a new line.</summary>
public enum Wrap
{
    /// <summary><c>nowrap</c>: everything stays on one line.</summary>
    NoWrap,

    /// <summary><c>wrap</c>: children that don't fit move to the next line.</summary>
    Wrap,

    /// <summary><c>wrap-reverse</c>: like <see cref="Wrap"/>, with the lines in reverse order.</summary>
    WrapReverse,
}

/// <summary>Values for <c>pointer-events</c>: whether a panel reacts to the mouse. Write <c>auto</c> in a stylesheet to take the parent's value.</summary>
public enum PointerEvents
{
    /// <summary><c>all</c>: the panel takes mouse input.</summary>
    All,

    /// <summary><c>none</c>, the default: the mouse goes through to whatever is under it.</summary>
    None,
}

/// <summary>Values for <c>text-align</c>: how lines of text line up.</summary>
public enum TextAlign
{
    /// <summary>Not a <c>text-align</c> value. Stylesheets never set it, so you won't need it.</summary>
    Auto,

    /// <summary><c>left</c> or <c>start</c>: lined up on the left.</summary>
    Left,

    /// <summary><c>center</c>: centered.</summary>
    Center,

    /// <summary><c>right</c> or <c>end</c>: lined up on the right.</summary>
    Right,

    /// <summary><c>justify</c>: every line but the last is stretched to the full width by spacing out words.</summary>
    Justify,
}

/// <summary>Values for <c>text-overflow</c>: what text that doesn't fit looks like.</summary>
public enum TextOverflow
{
    /// <summary>Text that doesn't fit is still drawn. This is the default.</summary>
    None,

    /// <summary><c>ellipsis</c>: text that doesn't fit ends in an ellipsis.</summary>
    Ellipsis,

    /// <summary><c>clip</c>: overflowing text is cut off.</summary>
    Clip,
}

/// <summary>Values for <c>word-break</c>: where lines can break.</summary>
public enum WordBreak
{
    /// <summary><c>normal</c>, <c>break-word</c> or <c>keep-all</c>: lines break between words.</summary>
    Normal,

    /// <summary><c>break-all</c>: lines can break inside words.</summary>
    BreakAll,
}

/// <summary>Values for <c>text-transform</c>: how the text's capitals are changed.</summary>
public enum TextTransform
{
    /// <summary><c>none</c>: the text is left as it is.</summary>
    None,

    /// <summary><c>capitalize</c>: the first letter of each word is capitalized.</summary>
    Capitalize,

    /// <summary><c>uppercase</c>: every letter is a capital.</summary>
    Uppercase,

    /// <summary><c>lowercase</c>: no letter is a capital.</summary>
    Lowercase,
}

/// <summary>Values for <c>text-decoration-skip-ink</c>: whether lines on the text break around letters.</summary>
public enum TextSkipInk
{
    /// <summary><c>auto</c> or <c>all</c>: lines skip over the glyphs they'd cross.</summary>
    All,

    /// <summary><c>none</c>: lines go straight through the glyphs.</summary>
    None,
}

/// <summary>Values for <c>text-decoration-style</c>: how lines on the text are drawn.</summary>
public enum TextDecorationStyle
{
    /// <summary><c>solid</c>: one line.</summary>
    Solid,

    /// <summary><c>double</c>: two lines.</summary>
    Double,

    /// <summary><c>dotted</c>: a line of dots.</summary>
    Dotted,

    /// <summary><c>dashed</c>: a line of dashes.</summary>
    Dashed,

    /// <summary><c>wavy</c>: a wavy line.</summary>
    Wavy,
}

/// <summary>Values for <c>text-decoration-line</c>: which lines are drawn on the text. Combine them to draw several.</summary>
[Flags]
public enum TextDecoration
{
    /// <summary><c>none</c>: no lines.</summary>
    None = 0,

    /// <summary><c>underline</c>: a line under the text.</summary>
    Underline = 2,

    /// <summary><c>line-through</c>: a line through the middle of the text.</summary>
    LineThrough = 4,

    /// <summary><c>overline</c>: a line above the text.</summary>
    Overline = 8,
}

/// <summary>Values for <c>white-space</c>: whether spaces and line breaks are kept and lines wrap.</summary>
public enum WhiteSpace
{
    /// <summary><c>normal</c>: spaces collapse and lines wrap.</summary>
    Normal,

    /// <summary><c>nowrap</c>: spaces collapse and lines don't wrap.</summary>
    NoWrap,

    /// <summary><c>pre-line</c>: spaces collapse, line breaks are kept and lines wrap.</summary>
    PreLine,

    /// <summary><c>pre</c>: spaces and line breaks are kept and lines don't wrap.</summary>
    Pre,

    /// <summary><c>pre-wrap</c>: spaces and line breaks are kept and lines wrap.</summary>
    PreWrap,

    /// <summary><c>break-spaces</c>: like <see cref="PreWrap"/>, and lines can also break inside runs of spaces.</summary>
    BreakSpaces,
}

/// <summary>Values for <c>font-style</c>: whether text is slanted.</summary>
[Flags]
public enum FontStyle
{
    /// <summary><c>normal</c>: upright.</summary>
    None = 0,

    /// <summary><c>italic</c>: the font's italic letters.</summary>
    Italic = 2,

    /// <summary><c>oblique</c>: slanted, using italics if the font has no oblique face.</summary>
    Oblique = 4,
}

/// <summary>Values for <c>font-smooth</c>: whether the edges of text are smoothed.</summary>
public enum FontSmooth
{
    /// <summary><c>auto</c>: smoothed where it can be.</summary>
    Auto,

    /// <summary><c>never</c> or <c>none</c>: hard, pixelated edges.</summary>
    Never,

    /// <summary><c>always</c>: always smoothed.</summary>
    Always,
}

/// <summary>Values for <c>object-fit</c>: how an image is sized to fit its panel.</summary>
public enum ObjectFit
{
    /// <summary><c>fill</c>: stretched to fill the content box, ignoring the aspect ratio.</summary>
    Fill,

    /// <summary><c>contain</c> or <c>scale-down</c>: scaled to fit inside the content box, keeping its aspect ratio.</summary>
    Contain,

    /// <summary><c>cover</c>, the default: scaled to fill the content box, keeping its aspect ratio.</summary>
    Cover,

    /// <summary><c>none</c>: not resized.</summary>
    None,
}

/// <summary>Values for <c>isolation</c>: whether a panel and its children blend together before they blend with what's behind them.</summary>
public enum Isolation
{
    /// <summary><c>auto</c>: only when something else about the panel needs it.</summary>
    Auto,

    /// <summary><c>isolate</c>: always.</summary>
    Isolate,
}

/// <summary>Values for <c>scrollbar-gutter</c>: whether space is kept for the scrollbar.</summary>
public enum ScrollbarGutter
{
    /// <summary><c>auto</c>: the scrollbar sits over the content.</summary>
    Auto,

    /// <summary><c>stable</c>: space for the scrollbar is kept on the right, whether it shows or not.</summary>
    Stable,

    /// <summary><c>stable both-edges</c>: the space is kept on both sides, so the content stays centered.</summary>
    StableBothEdges,
}

/// <summary>Values for <c>overscroll-behavior</c>: what happens when scrolling reaches the end.</summary>
public enum OverscrollBehavior
{
    /// <summary><c>auto</c>: scrolling carries on to the parent, and the edge bounces.</summary>
    Auto,

    /// <summary><c>contain</c>: the edge bounces, but scrolling doesn't carry on to the parent.</summary>
    Contain,

    /// <summary><c>none</c>: no bounce, and scrolling doesn't carry on to the parent.</summary>
    None,
}

/// <summary>Values for <c>background-repeat</c> and <c>mask-repeat</c>: whether an image tiles.</summary>
public enum BackgroundRepeat
{
    /// <summary><c>repeat</c>: tiled across and down.</summary>
    Repeat,

    /// <summary><c>repeat-x</c>: tiled across only.</summary>
    RepeatX,

    /// <summary><c>repeat-y</c>: tiled down only.</summary>
    RepeatY,

    /// <summary><c>no-repeat</c>: drawn once.</summary>
    NoRepeat,

    /// <summary><c>clamp</c> or <c>round</c>: the image's edge pixels stretch out to fill the rest.</summary>
    Clamp,
}

/// <summary>Values for <c>image-rendering</c>: how images look when they're scaled.</summary>
public enum ImageRendering
{
    /// <summary><c>auto</c> or <c>anisotropic</c>: smooth, and stays sharp when tilted in 3D.</summary>
    Anisotropic,

    /// <summary><c>bilinear</c>: smooth.</summary>
    Bilinear,

    /// <summary><c>trilinear</c>: smooth, and stays smooth when shrunk a lot.</summary>
    Trilinear,

    /// <summary><c>point</c>, <c>pixelated</c>, <c>crisp-edges</c> or <c>nearest-neighbor</c>: hard pixel edges.</summary>
    Point,
}

/// <summary>Values for <c>border-style</c>: how the border is drawn.</summary>
public enum BorderStyle
{
    /// <summary><c>solid</c>, the default: one solid line.</summary>
    Solid,

    /// <summary><c>none</c>: no border, and its width counts as zero.</summary>
    None,

    /// <summary><c>hidden</c>: no border, and its width counts as zero.</summary>
    Hidden,

    /// <summary><c>dotted</c>: round dots.</summary>
    Dotted,

    /// <summary><c>dashed</c>: a line of dashes.</summary>
    Dashed,

    /// <summary><c>double</c>: two lines.</summary>
    Double,

    /// <summary><c>groove</c>: looks carved in.</summary>
    Groove,

    /// <summary><c>ridge</c>: looks raised.</summary>
    Ridge,

    /// <summary><c>inset</c>: the box looks pressed in.</summary>
    Inset,

    /// <summary><c>outset</c>: the box looks raised.</summary>
    Outset,
}

/// <summary>Values for <c>background-clip</c>: how far out the background is drawn.</summary>
public enum BackgroundClip
{
    /// <summary><c>border-box</c>: everything, under the border too.</summary>
    BorderBox,

    /// <summary><c>padding-box</c>: inside the border.</summary>
    PaddingBox,

    /// <summary><c>content-box</c>: inside the padding.</summary>
    ContentBox,

    /// <summary><c>text</c>: only where the panel's text is.</summary>
    Text,
}

/// <summary>Values for <c>mask-mode</c>: what in the mask image decides which parts of the panel show.</summary>
public enum MaskMode
{
    /// <summary><c>match-source</c>: the image's luminance.</summary>
    MatchSource,

    /// <summary><c>alpha</c>: the image's transparency.</summary>
    Alpha,

    /// <summary><c>luminance</c>: the image's brightness.</summary>
    Luminance,
}

/// <summary>Values for <c>mask-scope</c>: what the mask controls.</summary>
public enum MaskScope
{
    /// <summary><c>default</c>: the mask hides the panel.</summary>
    Default,

    /// <summary><c>filter</c>: the mask blends between the filtered and unfiltered panel.</summary>
    Filter,
}

/// <summary>Values for how <c>border-image</c> fills the edges.</summary>
public enum BorderImageRepeat
{
    /// <summary><c>stretch</c>: each edge is stretched to fit.</summary>
    Stretch,

    /// <summary><c>round</c>: each edge is tiled a whole number of times.</summary>
    Round,
}

/// <summary>Values for whether <c>border-image</c> draws the middle of the image.</summary>
public enum BorderImageFill
{
    /// <summary>Only the edges are drawn. This is the default.</summary>
    Unfilled,

    /// <summary><c>fill</c>: the middle is drawn too.</summary>
    Filled,
}
