namespace Socotra.Layout;

internal enum Direction : byte
{
    Inherit,
    LTR,
    RTL,
}

internal enum FlexDirection : byte
{
    Column,
    ColumnReverse,
    Row,
    RowReverse,
}

internal enum Justify : byte
{
    FlexStart,
    Center,
    FlexEnd,
    SpaceBetween,
    SpaceAround,
    SpaceEvenly,

    Stretch,
}

internal enum Align : byte
{
    Auto,
    FlexStart,
    Center,
    FlexEnd,
    Stretch,
    Baseline,
    SpaceBetween,
    SpaceAround,
    SpaceEvenly,
}

internal enum PositionType : byte
{
    Static,
    Relative,
    Absolute,
    Fixed,
}

internal enum Wrap : byte
{
    NoWrap,
    Wrap,
    WrapReverse,
}

internal enum Overflow : byte
{
    Visible,
    Hidden,
    Scroll,
}

internal enum Display : byte
{
    Flex,
    None,
    Contents,
    Block,
    Grid,
    Inline,
}

internal enum BoxSizing : byte
{
    BorderBox,
    ContentBox,
}

internal enum Edge : byte
{
    Left,
    Top,
    Right,
    Bottom,
    Start,
    End,
    Horizontal,
    Vertical,
    All,
}

internal enum PhysicalEdge : byte
{
    Left = Edge.Left,
    Top = Edge.Top,
    Right = Edge.Right,
    Bottom = Edge.Bottom,
}

internal enum Gutter : byte
{
    Column,
    Row,
    All,
}

internal enum Dimension : byte
{
    Width,
    Height,
}

internal enum Unit : byte
{
    Undefined,
    Point,
    Percent,
    Auto,
}

internal enum MeasureMode : byte
{
    Undefined,

    Exactly,

    AtMost,

    MinContent,
}

internal enum GridAutoFlow : byte
{
    Row,
    Column,
    RowDense,
    ColumnDense,
}

internal enum SizingMode : byte
{
    StretchFit,

    MaxContent,

    FitContent,

    MinContent,
}

internal enum MeasureScope : byte
{
    Both,
    Width,
    Height,
}
