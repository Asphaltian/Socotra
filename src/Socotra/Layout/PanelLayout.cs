using Socotra.Layout;

namespace Socotra;

internal sealed partial class PanelLayout
{
    private readonly LayoutNode _node;
    private readonly Panel _panel;
    private Func<float, MeasureMode, float, MeasureMode, Vector2>? _measure;
    private Margin _gutter;
    private StyleLength _borderLeft;
    private StyleLength _borderRight;
    private Dependencies _dependencies;
    private Dependencies _gridDependencies;
    private ResolutionContext _context;
    private TrackCache<TrackList> _templateColumns;
    private TrackCache<TrackList> _templateRows;
    private TrackCache<TrackSizingFunction[]> _autoColumns;
    private TrackCache<TrackSizingFunction[]> _autoRows;

    [Flags]
    private enum Dependencies
    {
        None = 0,
        Width = 1,
        Height = 2,
        Font = 4,
        RootFont = 8,
        ViewWidth = 16,
        ViewHeight = 32,
        Direction = 64,
    }

    private enum Axis
    {
        Width,
        Height,
    }

    public PanelLayout(Panel panel)
    {
        _node = new LayoutNode { Context = panel };
        _panel = panel;
    }

    public LayoutNode Node => _node;

    public bool IsDirty => _node.IsDirty;

    public bool HasNewLayout => _node.HasNewLayout;

    public bool HasMeasure => _measure is not null;

    public bool Initialized { get; private set; }

    public bool ReferenceSizeChanged => _dependencies != Dependencies.None && _context.Changed(CurrentContext, _dependencies);

    private float ParentWidth => _panel.IsFixed ? _panel.FindRootPanel()?.Bounds.Width ?? 0 : _panel.Parent?.LayoutTree._node.LayoutWidth ?? 0;

    private float ParentHeight => _panel.IsFixed ? _panel.FindRootPanel()?.Bounds.Height ?? 0 : _panel.Parent?.LayoutTree._node.LayoutHeight ?? 0;

    private Layout.FlexDirection ParentDirection => _node.Owner?.Style.FlexDirection ?? Layout.FlexDirection.Row;

    private ResolutionContext CurrentContext => new(ParentWidth, ParentHeight, _panel.ComputedStyle?.FontSize?.Value ?? Length.CurrentFontSize,
        Length.RootFontSize, Length.RootSize, ParentDirection);

    public Rect Rect => new(_node.LayoutLeft, _node.LayoutTop, _node.LayoutWidth, _node.LayoutHeight);

    public Margin Margin => new(_node.LayoutMargin(PhysicalEdge.Left), _node.LayoutMargin(PhysicalEdge.Top), _node.LayoutMargin(PhysicalEdge.Right), _node.LayoutMargin(PhysicalEdge.Bottom));

    public Margin Padding => new(_node.LayoutPadding(PhysicalEdge.Left), _node.LayoutPadding(PhysicalEdge.Top), _node.LayoutPadding(PhysicalEdge.Right), _node.LayoutPadding(PhysicalEdge.Bottom));

    public Margin Border => new(_node.LayoutBorder(PhysicalEdge.Left), _node.LayoutBorder(PhysicalEdge.Top), _node.LayoutBorder(PhysicalEdge.Right), _node.LayoutBorder(PhysicalEdge.Bottom));

    public Margin Gutter
    {
        get => _gutter;
        set
        {
            if (_gutter.Left == value.Left && _gutter.Right == value.Right)
            {
                return;
            }

            _gutter = value;
            ApplyHorizontalBorders();
        }
    }

    public void AddChild(PanelLayout child) => _node.AddChild(child._node);

    public void InsertChild(int index, PanelLayout child) => _node.InsertChild(child._node, Math.Clamp(index, 0, _node.ChildCount));

    public void RemoveChild(PanelLayout child)
    {
        _inlineContext?.Invalidate();
        _node.RemoveChild(child._node);
    }

    public void Detach()
    {
        ReleaseInlineContext();
        _node.Owner?.RemoveChild(_node);
        _node.RemoveAllChildren();
    }

    public void CalculateLayout(float width, float height) => _node.CalculateLayout(width, height, Direction.LTR);

    public void ClearNewLayout() => _node.HasNewLayout = false;

    public void MarkDirty() => _node.MarkDirty();

    public void SetMeasure(Func<float, MeasureMode, float, MeasureMode, Vector2>? measure)
    {
        _measure = measure;
        _node.MeasureFunc = measure is null ? null : Measure;
    }

    public void Apply(Styles style)
    {
        _dependencies = Dependencies.None;
        var s = _node.Style;
        s.Width = Resolve(style.Width, Axis.Width);
        s.Height = Resolve(style.Height, Axis.Height);
        s.MinWidth = ResolveNoAuto(style.MinWidth, Axis.Width);
        s.MinHeight = ResolveNoAuto(style.MinHeight, Axis.Height);
        s.MaxWidth = ResolveNoAuto(style.MaxWidth, Axis.Width);
        s.MaxHeight = ResolveNoAuto(style.MaxHeight, Axis.Height);
        s.Display = (Display)style.Display!.Value;
        s.PositionType = (PositionType)style.Position!.Value;
        s.SetPosition(Edge.Left, ResolveNoAuto(style.Left, Axis.Width));
        s.SetPosition(Edge.Top, ResolveNoAuto(style.Top, Axis.Height));
        s.SetPosition(Edge.Right, ResolveNoAuto(style.Right, Axis.Width));
        s.SetPosition(Edge.Bottom, ResolveNoAuto(style.Bottom, Axis.Height));
        s.SetMargin(Edge.Left, Resolve(style.MarginLeft, Axis.Width));
        s.SetMargin(Edge.Top, Resolve(style.MarginTop, Axis.Height));
        s.SetMargin(Edge.Right, Resolve(style.MarginRight, Axis.Width));
        s.SetMargin(Edge.Bottom, Resolve(style.MarginBottom, Axis.Height));
        s.SetPadding(Edge.Left, ResolveNoAuto(style.PaddingLeft, Axis.Width));
        s.SetPadding(Edge.Top, ResolveNoAuto(style.PaddingTop, Axis.Height));
        s.SetPadding(Edge.Right, ResolveNoAuto(style.PaddingRight, Axis.Width));
        s.SetPadding(Edge.Bottom, ResolveNoAuto(style.PaddingBottom, Axis.Height));
        _borderLeft = ResolveBorder(style.UsedBorderLeftWidth, Axis.Width);
        _borderRight = ResolveBorder(style.UsedBorderRightWidth, Axis.Width);
        ApplyHorizontalBorders();
        s.SetBorder(Edge.Top, ResolveBorder(style.UsedBorderTopWidth, Axis.Height));
        s.SetBorder(Edge.Bottom, ResolveBorder(style.UsedBorderBottomWidth, Axis.Height));
        s.FlexDirection = (Layout.FlexDirection)style.FlexDirection!.Value;
        s.FlexWrap = (Layout.Wrap)style.FlexWrap!.Value;
        s.FlexGrow = style.FlexGrow!.Value;
        s.FlexShrink = style.FlexShrink!.Value;
        if (style.FlexBasis?.Unit == LengthUnit.Expression)
        {
            _dependencies |= Dependencies.Direction;
        }

        s.FlexBasis = Resolve(style.FlexBasis, ParentDirection is Layout.FlexDirection.Column or Layout.FlexDirection.ColumnReverse ? Axis.Height : Axis.Width);
        s.JustifyContent = (Layout.Justify)style.JustifyContent!.Value;
        s.AlignItems = (Layout.Align)style.AlignItems!.Value;
        s.AlignSelf = (Layout.Align)style.AlignSelf!.Value;
        s.AlignContent = (Layout.Align)style.AlignContent!.Value;
        s.JustifyItems = (Layout.Align)style.JustifyItems!.Value;
        s.JustifySelf = (Layout.Align)style.JustifySelf!.Value;
        s.SetGap(Layout.Gutter.Row, ResolveGap(style.RowGap));
        s.SetGap(Layout.Gutter.Column, ResolveGap(style.ColumnGap));
        s.AspectRatio = style.AspectRatio ?? float.NaN;
        s.Overflow = style.Overflow switch
        {
            OverflowMode.Scroll => Overflow.Scroll,
            OverflowMode.Hidden => Overflow.Hidden,
            _ => Overflow.Visible,
        };
        s.GridTemplateColumns = ParseTracks(style.GridTemplateColumns, ref _templateColumns, ParseTrackList);
        s.GridTemplateRows = ParseTracks(style.GridTemplateRows, ref _templateRows, ParseTrackList);
        s.GridAutoColumns = ParseTracks(style.GridAutoColumns, ref _autoColumns, ParseTrackSizes);
        s.GridAutoRows = ParseTracks(style.GridAutoRows, ref _autoRows, ParseTrackSizes);
        s.GridAutoFlow = (Layout.GridAutoFlow)style.GridAutoFlow!.Value;
        s.GridColumnStart = ParsePlacement(style.GridColumnStart);
        s.GridColumnEnd = ParsePlacement(style.GridColumnEnd);
        s.GridRowStart = ParsePlacement(style.GridRowStart);
        s.GridRowEnd = ParsePlacement(style.GridRowEnd);
        _context = CurrentContext;
        Initialized = true;
    }

    private LayoutSize Measure(LayoutNode node, float width, MeasureMode widthMode, float height, MeasureMode heightMode)
    {
        var size = _measure!(width, widthMode, height, heightMode);
        return new LayoutSize(size.X, size.Y);
    }

    private static Dependencies GetDependencies(Length length, Axis axis) => length.Unit switch
    {
        LengthUnit.Em => Dependencies.Font,
        LengthUnit.RootEm => Dependencies.RootFont,
        LengthUnit.ViewWidth => Dependencies.ViewWidth,
        LengthUnit.ViewHeight => Dependencies.ViewHeight,
        LengthUnit.ViewMin or LengthUnit.ViewMax => Dependencies.ViewWidth | Dependencies.ViewHeight,
        LengthUnit.Expression => GetExpressionDependencies(length.ToString(), axis),
        _ => Dependencies.None,
    };

    private static Dependencies GetExpressionDependencies(string expression, Axis axis)
    {
        var dependencies = Dependencies.None;
        var text = expression.AsSpan();
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '%')
            {
                dependencies |= axis == Axis.Width ? Dependencies.Width : Dependencies.Height;
            }

            if (!char.IsLetter(text[i]))
            {
                continue;
            }

            var start = i;
            while (i + 1 < text.Length && char.IsLetter(text[i + 1]))
            {
                i++;
            }

            dependencies |= text.Slice(start, i - start + 1).ToString().ToLowerInvariant() switch
            {
                "em" => Dependencies.Font,
                "rem" => Dependencies.RootFont,
                "vw" or "dvw" or "svw" or "lvw" => Dependencies.ViewWidth,
                "vh" or "dvh" or "svh" or "lvh" => Dependencies.ViewHeight,
                "vmin" or "vmax" => Dependencies.ViewWidth | Dependencies.ViewHeight,
                _ => Dependencies.None,
            };
        }

        return dependencies;
    }

    private float Reference(Axis axis) => axis == Axis.Width ? ParentWidth : ParentHeight;

    private StyleLength Resolve(Length? value, Axis axis)
    {
        if (value is not { } length)
        {
            return StyleLength.Undefined;
        }

        _dependencies |= GetDependencies(length, axis);
        return length.Unit switch
        {
            LengthUnit.Undefined => StyleLength.Undefined,
            LengthUnit.Auto => StyleLength.Auto,
            LengthUnit.Percentage => StyleLength.Percent(length.Value),
            LengthUnit.Pixels => StyleLength.Points(length.Value),
            LengthUnit.ViewWidth or LengthUnit.ViewHeight or LengthUnit.ViewMin or LengthUnit.ViewMax => StyleLength.Points(length.GetPixels(0)),
            _ => StyleLength.Points(length.GetPixels(Reference(axis))),
        };
    }

    private StyleLength ResolveNoAuto(Length? value, Axis axis)
    {
        var resolved = Resolve(value, axis);
        return resolved.IsAuto ? StyleLength.Undefined : resolved;
    }

    private StyleLength ResolveBorder(Length? value, Axis axis)
    {
        var resolved = ResolveNoAuto(value, axis);
        if (!resolved.IsPercent)
        {
            return resolved;
        }

        _dependencies |= axis == Axis.Width ? Dependencies.Width : Dependencies.Height;
        return StyleLength.Points(value!.Value.GetPixels(Reference(axis)));
    }

    private StyleLength ResolveGap(Length? value)
    {
        if (value is not { Unit: not (LengthUnit.Auto or LengthUnit.Undefined) } length)
        {
            return StyleLength.Undefined;
        }

        if (length.Unit == LengthUnit.Percentage)
        {
            return StyleLength.Percent(length.Value);
        }

        _dependencies |= GetDependencies(length, Axis.Width) & ~(Dependencies.Width | Dependencies.Height);
        return StyleLength.Points(length.GetPixels(0));
    }

    private static StyleLength WithGutter(StyleLength border, float gutter) =>
        gutter <= 0 ? border : StyleLength.Points((border.IsDefined ? border.Value : 0) + gutter);

    private void ApplyHorizontalBorders()
    {
        _node.Style.SetBorder(Edge.Left, WithGutter(_borderLeft, _gutter.Left));
        _node.Style.SetBorder(Edge.Right, WithGutter(_borderRight, _gutter.Right));
    }

    private T ParseTracks<T>(string? value, ref TrackCache<T> cache, Func<string?, T> parse)
    {
        var context = CurrentContext;
        if (!cache.Initialized || !ReferenceEquals(value, cache.Text) || cache.Context.Changed(context, cache.Dependencies))
        {
            _gridDependencies = Dependencies.None;
            cache.Value = parse(value);
            cache.Text = value;
            cache.Initialized = true;
            cache.Dependencies = _gridDependencies;
            cache.Context = context;
        }

        _dependencies |= cache.Dependencies;
        return cache.Value;
    }

    private TrackList ParseTrackList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return TrackList.None;
        }

        if (GridParser.TryParseTrackList(value, out var list, ResolveGridUnit))
        {
            return list;
        }

        Log.Warning($"Invalid grid track list: {value}");
        return TrackList.None;
    }

    private TrackSizingFunction[] ParseTrackSizes(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        if (GridParser.TryParseTrackSizes(value, out var sizes, ResolveGridUnit))
        {
            return sizes;
        }

        Log.Warning($"Invalid grid track sizes: {value}");
        return [];
    }

    private float? ResolveGridUnit(float value, string unit)
    {
        var length = Length.Parse(FormattableString.Invariant($"{value}{unit}"));
        if (length is { } parsed)
        {
            _gridDependencies |= GetDependencies(parsed, Axis.Width);
        }

        return length?.GetPixels(0);
    }

    private static GridPlacement ParsePlacement(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return GridPlacement.Auto;
        }

        if (GridParser.TryParsePlacement(value, out var placement))
        {
            return placement;
        }

        Log.Warning($"Invalid grid placement: {value}");
        return GridPlacement.Auto;
    }

    private readonly record struct ResolutionContext(float Width, float Height, float Font, float RootFont, Vector2 Viewport, Layout.FlexDirection Direction)
    {
        public bool Changed(ResolutionContext other, Dependencies dependencies) =>
            ((dependencies & Dependencies.Width) != 0 && Width != other.Width)
            || ((dependencies & Dependencies.Height) != 0 && Height != other.Height)
            || ((dependencies & Dependencies.Font) != 0 && Font != other.Font)
            || ((dependencies & Dependencies.RootFont) != 0 && RootFont != other.RootFont)
            || ((dependencies & Dependencies.ViewWidth) != 0 && Viewport.X != other.Viewport.X)
            || ((dependencies & Dependencies.ViewHeight) != 0 && Viewport.Y != other.Viewport.Y)
            || ((dependencies & Dependencies.Direction) != 0 && Direction != other.Direction);
    }

    private struct TrackCache<T>
    {
        public string? Text;
        public T Value;
        public bool Initialized;
        public Dependencies Dependencies;
        public ResolutionContext Context;
    }
}
