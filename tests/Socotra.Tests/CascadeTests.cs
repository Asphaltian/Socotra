namespace Socotra.Tests;

public class CascadeTests
{
    private static readonly string[] PropertyNames =
    [
        "width", "min-width", "max-width", "height", "min-height", "max-height", "left", "top", "right", "bottom", "opacity", "background-color",
        "padding-left", "padding-top", "padding-right", "padding-bottom", "margin-left", "margin-top", "margin-right", "margin-bottom",
        "border-top-left-radius", "border-top-right-radius", "border-bottom-right-radius", "border-bottom-left-radius", "border-top-left-radius-v",
        "border-top-right-radius-v", "border-bottom-right-radius-v", "border-bottom-left-radius-v", "border-left-width", "border-top-width",
        "border-right-width", "border-bottom-width", "border-style", "border-left-color", "border-top-color", "border-right-color",
        "border-bottom-color", "font-size", "font-color", "font-weight", "font-family", "caret-color", "cursor", "pointer-events", "mix-blend-mode",
        "position", "overflow-x", "overflow-y", "flex-direction", "justify-content", "justify-items", "justify-self", "display",
        "grid-template-columns", "grid-template-rows", "grid-auto-columns", "grid-auto-rows", "grid-auto-flow", "grid-column-start",
        "grid-column-end", "grid-row-start", "grid-row-end", "flex-wrap", "align-content", "align-self", "align-items", "flex-basis", "flex-grow",
        "flex-shrink", "row-gap", "column-gap", "aspect-ratio", "text-align", "text-overflow", "text-filter", "word-break", "text-decoration-line",
        "text-decoration-color", "text-decoration-thickness", "text-decoration-skip-ink", "text-decoration-style", "text-underline-offset",
        "text-overline-offset", "text-line-through-offset", "font-style", "font-variant-numeric", "transform", "text-transform", "transform-origin-x",
        "transform-origin-y", "letter-spacing", "line-height", "word-spacing", "white-space", "z-index", "order", "backdrop-filter-blur",
        "backdrop-filter-brightness", "backdrop-filter-contrast", "backdrop-filter-saturate", "backdrop-filter-sepia", "backdrop-filter-invert",
        "backdrop-filter-hue-rotate", "filter-blur", "filter-saturate", "filter-sepia", "filter-brightness", "filter-hue-rotate", "filter-invert",
        "filter-contrast", "filter-tint", "filter-border-width", "filter-border-color", "mask-mode", "mask-repeat", "mask-size-x", "mask-size-y",
        "mask-position-x", "mask-position-y", "mask-angle", "mask-scope", "background-size-x", "background-size-y", "background-position-x",
        "background-position-y", "background-repeat", "background-clip", "border-image-width-left", "border-image-width-right",
        "border-image-width-top", "border-image-width-bottom", "border-image-fill", "border-image-repeat", "border-image-tint",
        "background-blend-mode", "background-tint", "background-angle", "text-background-angle", "perspective-origin-x", "perspective-origin-y",
        "text-stroke-color", "text-stroke-width", "image-rendering", "animation-direction", "animation-fill-mode", "animation-iteration-count",
        "animation-name", "animation-play-state", "animation-timing-function", "font-smooth", "object-fit", "outline-width", "outline-color",
        "outline-offset", "isolation", "scrollbar-width", "overscroll-behavior-x", "overscroll-behavior-y", "scrollbar-gutter",
        "scrollbar-thumb-color", "scrollbar-track-color", "border-shape", "box-shadow", "text-shadow", "filter-drop-shadow", "background-image",
        "mask-image", "border-image-source",
    ];

    private static (RootPanel Root, Panel Parent, Panel Child) Tree(string styles)
    {
        var root = new RootPanel();
        root.StyleSheet.Parse(styles);
        var parent = root.AddChild<Panel>("parent");
        var child = parent.AddChild<Panel>("child");
        Update(root);
        return (root, parent, child);
    }

    private static void Update(RootPanel root, float deltaTime = 0) => root.Update(new Rect(0, 0, 1920, 1080), deltaTime);

    [Fact]
    public void EveryPropertyStartsAtItsDefault()
    {
        var (_, _, child) = Tree("");

        Assert.All(PropertyNames, name => Assert.True(child.ComputedStyle!.IsDefault(name), name));
        Assert.Throws<ArgumentException>(() => child.ComputedStyle!.IsDefault("wobble"));
    }

    [Fact]
    public void DefaultsFollowSbox()
    {
        var (_, _, child) = Tree("");
        var style = child.ComputedStyle!;

        Assert.Equal(PointerEvents.None, style.PointerEvents);
        Assert.Equal(Color.White, style.BorderTopColor);
        Assert.Equal(Color.Black, style.FontColor);
        Assert.Equal(Length.Pixels(13), style.FontSize);
        Assert.Equal("Arial", style.FontFamily);
        Assert.Equal(400, style.FontWeight);
        Assert.Equal(Length.Percent(100), style.LineHeight);
        Assert.Equal(Length.Percent(0), style.LetterSpacing);
        Assert.Equal(Justify.Stretch, style.JustifyContent);
        Assert.Equal(Align.Stretch, style.AlignItems);
        Assert.Equal(TextOverflow.None, style.TextOverflow);
        Assert.Equal(ObjectFit.Cover, style.ObjectFit);
        Assert.Equal(Length.Auto, style.RowGap);
        Assert.Equal(Length.Undefined, style.BackgroundSizeX);
        Assert.Equal(BorderStyle.Solid, style.BorderStyle);
        Assert.Equal("default", style.MixBlendMode);
        Assert.Equal("auto", style.Cursor);
        Assert.Equal(FilterMode.Bilinear, style.TextFilter);
        Assert.Equal(Color.Transparent, style.OutlineColor);
        Assert.Same(BorderShape.None, style.BorderShape);
        Assert.Empty(style.BoxShadow!);
        Assert.Null(style.Content);
        Assert.Null(style.AnimationDuration);
        Assert.Null(style.CaretColor);
        Assert.Null(style.BorderTopLeftRadiusV);
        Assert.True(float.IsNaN(style.AspectRatio!.Value));
        Assert.False(style.HasAnimation);
        Assert.False(style.HasTransitions);
    }

    [Fact]
    public void InheritedPropertiesPassDownAndOthersDoNot()
    {
        var (_, parent, child) = Tree("""
            .parent {
                color: red; font-size: 20px; font-weight: bold; font-family: Roboto; cursor: pointer; pointer-events: all;
                text-align: center; text-overflow: ellipsis; white-space: nowrap; letter-spacing: 1px; line-height: 2;
                text-shadow: 1px 1px blue; text-stroke: 1px green; scrollbar-width: 4px; image-rendering: point; font-smooth: always;
                text-decoration: underline; font-style: italic; text-transform: lowercase; word-break: break-all; mix-blend-mode: screen;
                width: 50px; margin: 3px; background-color: red; opacity: 0.5; border-width: 2px;
            }
            """);
        var style = child.ComputedStyle!;

        Assert.Equal(Color.Parse("red"), style.FontColor);
        Assert.Equal(Length.Pixels(20), style.FontSize);
        Assert.Equal(700, style.FontWeight);
        Assert.Equal("Roboto", style.FontFamily);
        Assert.Equal("pointer", style.Cursor);
        Assert.Equal(PointerEvents.All, style.PointerEvents);
        Assert.Equal(TextAlign.Center, style.TextAlign);
        Assert.Equal(TextOverflow.Ellipsis, style.TextOverflow);
        Assert.Equal(WhiteSpace.NoWrap, style.WhiteSpace);
        Assert.Equal(Length.Pixels(1), style.LetterSpacing);
        Assert.Equal(Length.Percent(200), style.LineHeight);
        Assert.Single(style.TextShadow!);
        Assert.Equal(Length.Pixels(1), style.TextStrokeWidth);
        Assert.Equal(Length.Pixels(4), style.ScrollbarWidth);
        Assert.Equal(ImageRendering.Point, style.ImageRendering);
        Assert.Equal(FontSmooth.Always, style.FontSmooth);
        Assert.Equal(TextDecoration.Underline, style.TextDecorationLine);
        Assert.Equal(FontStyle.Italic, style.FontStyle);
        Assert.Equal(TextTransform.Lowercase, style.TextTransform);
        Assert.Equal(WordBreak.BreakAll, style.WordBreak);
        Assert.Equal("screen", style.MixBlendMode);

        Assert.Equal(Length.Undefined, style.Width);
        Assert.Equal(Length.Pixels(0), style.MarginLeft);
        Assert.Equal(Color.Transparent, style.BackgroundColor);
        Assert.Equal(1, style.Opacity);
        Assert.Equal(Length.Pixels(0), style.BorderTopWidth);
        Assert.Equal(0.5f, parent.Opacity);
    }

    [Fact]
    public void TextGradientsPassDown()
    {
        var (_, _, child) = Tree(".parent { color: linear-gradient(red, blue); }");

        Assert.Equal(GradientType.Linear, child.ComputedStyle!.TextGradient.Type);
        Assert.False(child.ComputedStyle.TextGradient.IsEmpty);
    }

    [Fact]
    public void CssWideKeywordsResolveAgainstTheParentOrTheDefault()
    {
        var (_, _, child) = Tree("""
            .parent { font-size: 20px; width: 40px; margin: 7px; color: red; background-color: blue; }
            .child { font-size: initial; width: inherit; margin: inherit; color: unset; background-color: unset; opacity: revert; line-height: inherit; }
            """);
        var style = child.ComputedStyle!;

        Assert.Equal(Length.Pixels(13), style.FontSize);
        Assert.Equal(Length.Pixels(40), style.Width);
        Assert.Equal(Length.Pixels(7), style.MarginTop);
        Assert.Equal(Length.Pixels(7), style.MarginLeft);
        Assert.Equal(Color.Parse("red"), style.FontColor);
        Assert.Equal(Color.Transparent, style.BackgroundColor);
        Assert.Equal(1, style.Opacity);
        Assert.Equal(Length.Percent(100), style.LineHeight);
    }

    [Fact]
    public void CssWideKeywordsCascadeInSourceOrder()
    {
        var (_, parent, child) = Tree("""
            .parent { width: 10px; }
            .child { width: inherit; height: 5px; }
            .parent .child { width: 20px; height: initial; }
            """);

        Assert.Equal(Length.Pixels(20), child.ComputedStyle!.Width);
        Assert.Equal(Length.Undefined, child.ComputedStyle.Height);

        child.Style.Width = null;
        child.Style.Set("width", "inherit");
        Update(parent.FindRootPanel()!);
        Assert.Equal(Length.Pixels(10), child.ComputedStyle!.Width);
    }

    [Fact]
    public void FlexKeepsItsOwnInitialKeyword()
    {
        var styles = new Styles();

        Assert.True(styles.Set("flex", "initial"));

        Assert.Equal((0f, 1f), (styles.FlexGrow!.Value, styles.FlexShrink!.Value));
        Assert.Equal(Length.Auto, styles.FlexBasis);
    }

    [Fact]
    public void CurrentColorResolvesToTheTextColor()
    {
        var (_, _, child) = Tree("""
            .parent { color: blue; }
            .child {
                color: currentColor; border-color: currentColor; outline-color: currentColor; text-decoration-color: currentColor;
                text-stroke-color: currentColor; background-tint: currentColor; filter: tint(currentColor); caret-color: currentColor;
                box-shadow: 1px 1px currentColor, 2px 2px; text-shadow: 1px 1px;
            }
            """);
        var style = child.ComputedStyle!;
        var blue = Color.Parse("blue");

        Assert.Equal(blue, style.FontColor);
        Assert.Equal(blue, style.BorderBottomColor);
        Assert.Equal(blue, style.OutlineColor);
        Assert.Equal(blue, style.TextDecorationColor);
        Assert.Equal(blue, style.TextStrokeColor);
        Assert.Equal(blue, style.BackgroundTint);
        Assert.Equal(blue, style.FilterTint);
        Assert.Equal(blue, style.CaretColor);
        Assert.All(style.BoxShadow!, shadow => Assert.Equal(blue, shadow.Color));
        Assert.Equal(blue, Assert.Single(style.TextShadow!).Color);
    }

    [Fact]
    public void AnUnsetOverflowAxisCopiesTheOtherOne()
    {
        var (_, parent, child) = Tree(".parent { overflow-y: scroll; } .child { overflow-x: hidden; }");

        Assert.Equal((OverflowMode.Scroll, OverflowMode.Scroll), (parent.ComputedStyle!.OverflowX, parent.ComputedStyle.OverflowY));
        Assert.Equal((OverflowMode.Hidden, OverflowMode.Hidden), (child.ComputedStyle!.OverflowX, child.ComputedStyle.OverflowY));
    }

    [Fact]
    public void RootScaleRoundsLayoutSizesUpAndScalesTextExactly()
    {
        var root = new RootPanel();
        root.StyleSheet.Parse("""
            .a {
                width: 10.3px; font-size: 10.3px; flex-basis: 10.3px; letter-spacing: 1.3px; border-width: 1.2px; border-radius: 2.2px;
                outline-width: 1.1px; background-size: 5px; box-shadow: 1px 2px 3px red; transform: translate(1.2px, 0);
                border-shape: circle(1.2px); mask-size: 3px; text-stroke-width: 0.3px;
            }
            """);
        var panel = root.AddChild<Panel>("a");
        root.Update(new Rect(0, 0, 3840, 2160), 0);
        var style = panel.ComputedStyle!;

        Assert.Equal(Length.Pixels(21), style.Width);
        Assert.Equal(20.6f, style.FontSize!.Value.Value, 0.001f);
        Assert.Equal(20.6f, style.FlexBasis!.Value.Value, 0.001f);
        Assert.Equal(2.6f, style.LetterSpacing!.Value.Value, 0.001f);
        Assert.Equal(0.6f, style.TextStrokeWidth!.Value.Value, 0.001f);
        Assert.Equal(Length.Pixels(3), style.BorderTopWidth);
        Assert.Equal(Length.Pixels(5), style.BorderTopLeftRadius);
        Assert.Equal(Length.Pixels(5), style.BorderTopLeftRadiusV);
        Assert.Equal(Length.Pixels(3), style.OutlineWidth);
        Assert.Equal(Length.Pixels(5), style.BackgroundSizeX);
        Assert.Equal(Length.Pixels(3), style.MaskSizeX);
        Assert.Equal(new Shadow(2, 4, 6, 0, Color.Parse("red")!.Value, false), Assert.Single(style.BoxShadow!));
        Assert.Equal(Length.Pixels(3), style.BorderShape!.CircleRadius);
        Assert.Equal(3, Vector2.Transform(Vector2.Zero, style.BuildTransformMatrix(Vector2.Zero)).X);
    }

    [Fact]
    public void BeforeAndAfterElementsAreMadeForRulesThatTargetThem()
    {
        var root = new RootPanel();
        root.StyleSheet.Parse("""
            .a { width: 50px; }
            .a::before { content: "first"; width: 5px; }
            .a:hover::after { width: 6px; }
            """);
        var panel = root.AddChild<Panel>("a");
        panel.AddChild<Panel>("middle");
        Update(root);
        Update(root);

        Assert.Equal(Length.Pixels(50), panel.ComputedStyle!.Width);
        var before = Assert.IsType<Label>(panel.Children[0]);
        Assert.Equal("element", before.ElementName);
        Assert.Equal(PseudoClass.Before, before.PseudoClass & PseudoClass.Before);
        Assert.Equal("first", before.ComputedStyle!.Content);
        Assert.Equal(Length.Pixels(5), before.ComputedStyle.Width);
        Assert.Equal(2, panel.Children.Count);

        panel.Switch(PseudoClass.Hover, true);
        Update(root);
        Update(root);

        var after = Assert.IsType<Label>(panel.Children[^1]);
        Assert.Equal(PseudoClass.After, after.PseudoClass & PseudoClass.After);
        Assert.Equal(Length.Pixels(6), after.ComputedStyle!.Width);
        Assert.Null(after.ComputedStyle.Content);
        Assert.Same(before, panel.Children[0]);

        panel.RemoveClass("a");
        Update(root);
        Update(root);
        Update(root);

        Assert.Single(panel.Children);
    }

    [Fact]
    public void BeforeAndAfterSelectorsParseWithOneOrTwoColons()
    {
        var styles = StyleSheet.FromString(".a:before { width: 1px; } .b::after { width: 2px; }");
        var root = new RootPanel();
        root.StyleSheet.Add(styles);
        var a = root.AddChild<Panel>("a");
        var b = root.AddChild<Panel>("b");
        Update(root);

        Assert.True(a.Style.HasBeforeElement);
        Assert.False(a.Style.HasAfterElement);
        Assert.True(b.Style.HasAfterElement);
        Assert.NotEqual(Length.Pixels(1), a.ComputedStyle!.Width);
    }
}
