namespace Socotra.Tests;

public class StylePropertyTests
{
    private static Styles Parse(string declarations)
    {
        var styles = new Styles();
        Assert.True(styles.Set(declarations), declarations);
        return styles;
    }

    [Fact]
    public void BoxShorthandsExpand()
    {
        var styles = Parse("margin: 1px 2px 3px; padding: 4px 5px; inset: 6px; margin-inline: 7px 8px; padding-block: 9px");

        Assert.Equal((Length.Pixels(1), Length.Pixels(8), Length.Pixels(3), Length.Pixels(7)), (styles.MarginTop, styles.MarginRight, styles.MarginBottom, styles.MarginLeft));
        Assert.Equal((Length.Pixels(9), Length.Pixels(5), Length.Pixels(9), Length.Pixels(5)), (styles.PaddingTop, styles.PaddingRight, styles.PaddingBottom, styles.PaddingLeft));
        Assert.Equal((Length.Pixels(6), Length.Pixels(6), Length.Pixels(6), Length.Pixels(6)), (styles.Top, styles.Right, styles.Bottom, styles.Left));
        Assert.True(styles.Set("margin", "1px 2px 3px 4px"));
        Assert.Equal(Length.Pixels(4), styles.MarginLeft);
        Assert.False(styles.Set("padding", "1px 2px 3px 4px 5px"));
        Assert.False(styles.Set("padding", "10px foo"));
        Assert.Equal(Length.Pixels(9), styles.PaddingTop);
        Assert.False(styles.Set("overflow", "hidden foo"));
        Assert.Null(styles.OverflowX);
    }

    [Theory]
    [InlineData("font-style", "bogus")]
    [InlineData("font-style", "oblique sideways")]
    [InlineData("text-decoration-line", "bogus")]
    [InlineData("text-decoration-line", "none underline")]
    [InlineData("animation-direction", "sideways")]
    [InlineData("animation-fill-mode", "all")]
    [InlineData("animation-play-state", "stopped")]
    public void KeywordPropertiesRejectOtherWords(string property, string value)
    {
        Assert.False(new Styles().Set(property, value));
    }

    [Fact]
    public void KeywordPropertiesTakeTheirKeywords()
    {
        var styles = Parse("font-style: oblique 10deg; text-decoration-line: underline overline; animation-direction: Alternate; animation-play-state: paused");

        Assert.Equal(Socotra.FontStyle.Oblique, styles.FontStyle);
        Assert.Equal(TextDecoration.Underline | TextDecoration.Overline, styles.TextDecorationLine);
        Assert.Equal("alternate", styles.AnimationDirection);
        Assert.Equal("paused", styles.AnimationPlayState);
    }

    [Fact]
    public void GapAndFilterKeywordsMatchTheirShorthands()
    {
        var styles = Parse("row-gap: normal; backdrop-filter: greyscale(1); filter: drop-shadow(1px 1px red)");
        Assert.Equal(Length.Pixels(0), styles.RowGap);
        Assert.Equal(Length.Pixels(0), styles.BackdropFilterSaturate);

        Assert.True(styles.Set("filter", "none"));
        Assert.Empty(styles.FilterDropShadow!);
    }

    [Fact]
    public void LogicalLonghandsAlias()
    {
        var styles = Parse("margin-inline-start: 1px; margin-block-end: 2px; padding-inline-end: 3px; inset-block-start: 4px; color: red; background-image-tint: blue");

        Assert.Equal(Length.Pixels(1), styles.MarginLeft);
        Assert.Equal(Length.Pixels(2), styles.MarginBottom);
        Assert.Equal(Length.Pixels(3), styles.PaddingRight);
        Assert.Equal(Length.Pixels(4), styles.Top);
        Assert.Equal(Color.Parse("red"), styles.FontColor);
        Assert.Equal(Color.Parse("blue"), styles.BackgroundTint);
    }

    [Fact]
    public void BorderShorthandsSetWidthColorAndSharedStyle()
    {
        var styles = Parse("border: 2px dashed red; border-left: 5px blue");

        Assert.Equal(Length.Pixels(2), styles.BorderTopWidth);
        Assert.Equal(Length.Pixels(5), styles.BorderLeftWidth);
        Assert.Equal(Color.Parse("red"), styles.BorderRightColor);
        Assert.Equal(Color.Parse("blue"), styles.BorderLeftColor);
        Assert.Equal(BorderStyle.Dashed, styles.BorderStyle);

        styles.Set("border-top", "none");
        Assert.Equal(Length.Pixels(0), styles.BorderTopWidth);
        Assert.Equal(BorderStyle.Dashed, styles.BorderStyle);
        Assert.False(styles.Set("border", "1px 2px"));
        Assert.False(styles.Set("border", "-1px solid"));
    }

    [Fact]
    public void BorderStyleNoneHidesTheWidthsWithoutLosingThem()
    {
        var styles = Parse("border-width: 3px; border-style: hidden");

        Assert.Equal(Length.Pixels(3), styles.BorderTopWidth);
        Assert.Equal(Length.Pixels(0), styles.UsedBorderTopWidth);
        Assert.False(styles.HasBorder);

        styles.Set("border-style", "groove");
        Assert.Equal(Length.Pixels(3), styles.UsedBorderTopWidth);
        Assert.True(styles.HasBorder);
        Assert.Equal(new Vector4(3), styles.GetBorderWidths(100));
        Assert.False(styles.Set("border-style", "wobbly"));
    }

    [Fact]
    public void BorderWidthAndColorTakeOneToFourValues()
    {
        var styles = Parse("border-width: 1px 2px 3px 4px; border-color: red blue");

        Assert.Equal((Length.Pixels(1), Length.Pixels(2), Length.Pixels(3), Length.Pixels(4)),
            (styles.BorderTopWidth, styles.BorderRightWidth, styles.BorderBottomWidth, styles.BorderLeftWidth));
        Assert.Equal(Color.Parse("red"), styles.BorderTopColor);
        Assert.Equal(Color.Parse("blue"), styles.BorderLeftColor);
    }

    [Fact]
    public void BorderRadiusTakesEllipticalCorners()
    {
        var styles = Parse("border-radius: 10px 20px / 5px");

        Assert.Equal(Length.Pixels(10), styles.BorderTopLeftRadius);
        Assert.Equal(Length.Pixels(20), styles.BorderTopRightRadius);
        Assert.Equal(Length.Pixels(5), styles.BorderTopLeftRadiusV);
        Assert.True(styles.HasBorderRadius);

        styles.Set("border-bottom-left-radius", "8px 4px");
        Assert.Equal(Length.Pixels(8), styles.BorderBottomLeftRadius);
        Assert.Equal(Length.Pixels(4), styles.BorderBottomLeftRadiusV);

        var radii = styles.GetBorderRadii(new Rect(0, 0, 100, 100));
        Assert.Equal(new Vector2(10, 5), radii.TopLeft);
        Assert.Equal(new Vector2(8, 4), radii.BottomLeft);
    }

    [Fact]
    public void BorderRadiiShrinkTogetherWhenTheyOverlap()
    {
        var styles = Parse("border-radius: 100px");

        var radii = styles.GetBorderRadii(new Rect(0, 0, 100, 50));

        Assert.Equal(new Vector2(25, 25), radii.TopLeft);
        Assert.Equal(new Vector2(25, 25), radii.BottomRight);
    }

    [Fact]
    public void OutlineSetsWidthAndColor()
    {
        var styles = Parse("outline: 2px solid red; outline-offset: 3px");

        Assert.Equal(Length.Pixels(2), styles.OutlineWidth);
        Assert.Equal(Color.Parse("red"), styles.OutlineColor);
        Assert.Equal(Length.Pixels(3), styles.OutlineOffset);
        Assert.Null(styles.BorderStyle);
    }

    [Theory]
    [InlineData("flex: 2", 2, 1, "0px")]
    [InlineData("flex: 2 3", 2, 3, "0px")]
    [InlineData("flex: 2 3 10px", 2, 3, "10px")]
    [InlineData("flex: 2 3 4", 2, 3, "4px")]
    [InlineData("flex: 2 50%", 2, 1, "50%")]
    [InlineData("flex: 30px", 0, 1, "30px")]
    [InlineData("flex: none", 0, 0, "auto")]
    [InlineData("flex: auto", 1, 1, "auto")]
    [InlineData("flex: initial", 0, 1, "auto")]
    public void FlexShorthandExpands(string declaration, float grow, float shrink, string basis)
    {
        var styles = Parse(declaration);

        Assert.Equal(grow, styles.FlexGrow);
        Assert.Equal(shrink, styles.FlexShrink);
        Assert.Equal(Length.Parse(basis), styles.FlexBasis);
    }

    [Fact]
    public void FlexboxKeywordsParse()
    {
        var styles = Parse("flex-flow: column-reverse wrap; justify-content: space-evenly; align-items: self-end; align-self: normal; align-content: start; justify-items: baseline; justify-self: center; gap: 4px 8px");

        Assert.Equal(FlexDirection.ColumnReverse, styles.FlexDirection);
        Assert.Equal(Wrap.Wrap, styles.FlexWrap);
        Assert.Equal(Justify.SpaceEvenly, styles.JustifyContent);
        Assert.Equal(Align.FlexEnd, styles.AlignItems);
        Assert.Equal(Align.Stretch, styles.AlignSelf);
        Assert.Equal(Align.FlexStart, styles.AlignContent);
        Assert.Equal(Align.Baseline, styles.JustifyItems);
        Assert.Equal(Align.Center, styles.JustifySelf);
        Assert.Equal((Length.Pixels(4), Length.Pixels(8)), (styles.RowGap, styles.ColumnGap));

        Assert.True(styles.Set("justify-content", "normal"));
        Assert.Equal(Justify.Stretch, styles.JustifyContent);
        Assert.True(styles.Set("gap", "normal"));
        Assert.Equal(Length.Pixels(0), styles.RowGap);
        Assert.True(styles.Set("place-items", "center end"));
        Assert.Equal((Align.Center, Align.FlexEnd), (styles.AlignItems, styles.JustifyItems));
        Assert.True(styles.Set("place-self", "stretch"));
        Assert.Equal((Align.Stretch, Align.Stretch), (styles.AlignSelf, styles.JustifySelf));
    }

    [Theory]
    [InlineData("flex", DisplayMode.Flex)]
    [InlineData("none", DisplayMode.None)]
    [InlineData("contents", DisplayMode.Contents)]
    [InlineData("block", DisplayMode.Block)]
    [InlineData("flow-root", DisplayMode.Block)]
    [InlineData("grid", DisplayMode.Grid)]
    [InlineData("inline", DisplayMode.Inline)]
    public void DisplayModesParse(string value, DisplayMode mode)
    {
        Assert.Equal(mode, Parse($"display: {value}").Display);
    }

    [Fact]
    public void GridShorthandsKeepTheirText()
    {
        var styles = Parse("grid-template: 1fr auto / repeat(3, 10px); grid-area: header; grid-auto-flow: column dense; grid-auto-rows: 20px");

        Assert.Equal("1fr auto", styles.GridTemplateRows);
        Assert.Equal("repeat(3, 10px)", styles.GridTemplateColumns);
        Assert.Equal(("header", "header", "header", "header"), (styles.GridRowStart, styles.GridColumnStart, styles.GridRowEnd, styles.GridColumnEnd));
        Assert.Equal(GridAutoFlow.ColumnDense, styles.GridAutoFlow);
        Assert.Equal("20px", styles.GridAutoRows);

        styles.Set("grid-column", "2 / span 3");
        Assert.Equal(("2", "span 3"), (styles.GridColumnStart, styles.GridColumnEnd));
        styles.Set("grid-row", "3");
        Assert.Equal(("3", "auto"), (styles.GridRowStart, styles.GridRowEnd));
        Assert.False(styles.Set("grid-auto-flow", "row column"));
    }

    [Theory]
    [InlineData("hidden", OverflowMode.Hidden)]
    [InlineData("scroll", OverflowMode.Scroll)]
    [InlineData("auto", OverflowMode.Scroll)]
    [InlineData("clip", OverflowMode.Clip)]
    [InlineData("clip-whole", OverflowMode.ClipWhole)]
    [InlineData("visible", OverflowMode.Visible)]
    public void OverflowModesParse(string value, OverflowMode mode)
    {
        var styles = Parse($"overflow: {value}");

        Assert.Equal((mode, mode), (styles.OverflowX, styles.OverflowY));
        Assert.Equal(mode, styles.Overflow);
    }

    [Fact]
    public void ScrollingPropertiesParse()
    {
        var styles = Parse("overflow-x: hidden; overflow-y: scroll; overscroll-behavior: contain none; scrollbar-width: thin; scrollbar-gutter: stable both-edges; scrollbar-color: red blue");

        Assert.Equal(OverflowMode.Scroll, styles.Overflow);
        Assert.Equal((OverscrollBehavior.Contain, OverscrollBehavior.None), (styles.OverscrollBehaviorX, styles.OverscrollBehaviorY));
        Assert.Equal(Length.Pixels(8), styles.ScrollbarWidth);
        Assert.Equal(ScrollbarGutter.StableBothEdges, styles.ScrollbarGutter);
        Assert.Equal((Color.Parse("red"), Color.Parse("blue")), (styles.ScrollbarThumbColor, styles.ScrollbarTrackColor));

        styles.Set("scrollbar-width", "none");
        styles.Set("scrollbar-color", "auto");
        styles.Set("scrollbar-gutter", "stable");
        Assert.Equal(Length.Pixels(0), styles.ScrollbarWidth);
        Assert.Null(styles.ScrollbarThumbColor);
        Assert.Equal(ScrollbarGutter.Stable, styles.ScrollbarGutter);
    }

    [Theory]
    [InlineData("aspect-ratio: 16 / 9", 16 / 9.0f)]
    [InlineData("aspect-ratio: 2", 2)]
    [InlineData("aspect-ratio: auto 4/2", 2)]
    [InlineData("aspect-ratio: 3:1", 3)]
    public void AspectRatiosParse(string declaration, float ratio)
    {
        Assert.Equal(ratio, Parse(declaration).AspectRatio!.Value, 0.0001f);
    }

    [Theory]
    [InlineData("auto")]
    [InlineData("none")]
    public void AspectRatioKeywordsMeanNoRatio(string value)
    {
        Assert.True(float.IsNaN(Parse($"aspect-ratio: {value}").AspectRatio!.Value));
    }

    [Fact]
    public void PositionAndStackingParse()
    {
        var styles = Parse("position: fixed; z-index: -3; order: 2; isolation: isolate; opacity: 40%; mix-blend-mode: multiply");

        Assert.Equal(PositionMode.Fixed, styles.Position);
        Assert.Equal(-3, styles.ZIndex);
        Assert.Equal(2, styles.Order);
        Assert.Equal(Isolation.Isolate, styles.Isolation);
        Assert.Equal(0.4f, styles.Opacity!.Value, 0.0001f);
        Assert.Equal("multiply", styles.MixBlendMode);
    }

    [Fact]
    public void PointerEventsAutoLeavesItUnset()
    {
        var styles = Parse("pointer-events: all");
        Assert.Equal(PointerEvents.All, styles.PointerEvents);

        styles.Set("pointer-events", "auto");
        Assert.Null(styles.PointerEvents);
        Assert.True(styles.Set("pointer-events", "none"));
        Assert.Equal(PointerEvents.None, styles.PointerEvents);
    }

    [Fact]
    public void FontShorthandSetsItsParts()
    {
        var styles = Parse("font: italic bold 20px/1.5 \"Open Sans\", sans-serif");

        Assert.Equal(FontStyle.Italic, styles.FontStyle);
        Assert.Equal(700, styles.FontWeight);
        Assert.Equal(Length.Pixels(20), styles.FontSize);
        Assert.Equal(Length.Percent(150), styles.LineHeight);
        Assert.Equal("Open Sans", styles.FontFamily);
        Assert.False(styles.Set("font", "bold"));
    }

    [Theory]
    [InlineData("thin", 100)]
    [InlineData("extralight", 200)]
    [InlineData("light", 300)]
    [InlineData("regular", 400)]
    [InlineData("medium", 500)]
    [InlineData("semibold", 600)]
    [InlineData("bold", 700)]
    [InlineData("extrabold", 800)]
    [InlineData("black", 900)]
    [InlineData("ultrablack", 950)]
    [InlineData("bolder", 900)]
    [InlineData("lighter", 200)]
    [InlineData("350", 350)]
    public void FontWeightNamesParse(string value, int weight)
    {
        Assert.Equal(weight, Parse($"font-weight: {value}").FontWeight);
    }

    [Theory]
    [InlineData("xx-small", 10)]
    [InlineData("small", 14)]
    [InlineData("medium", 16)]
    [InlineData("xxx-large", 48)]
    [InlineData("2rem", 26)]
    public void FontSizeKeywordsParse(string value, float pixels)
    {
        new RootPanel().Update(new Rect(0, 0, 1920, 1080), 0);
        Assert.Equal(pixels, Parse($"font-size: {value}").FontSize!.Value.GetPixels(0));
    }

    [Theory]
    [InlineData("'Comic Neue'", "Comic Neue")]
    [InlineData("Roboto, Arial", "Roboto")]
    [InlineData("sans-serif", "Arial")]
    [InlineData("serif", "Times New Roman")]
    [InlineData("monospace", "Consolas")]
    [InlineData("ui-rounded", "Poppins")]
    [InlineData("cursive", "Comic Sans MS")]
    [InlineData("fantasy", "Impact")]
    public void FontFamiliesUseTheFirstInTheList(string value, string family)
    {
        Assert.Equal(family, Parse($"font-family: {value}").FontFamily);
    }

    [Fact]
    public void TextPropertiesParse()
    {
        var styles = Parse("""
            text-align: justify; text-overflow: ellipsis; text-filter: point; word-break: break-all; text-transform: uppercase;
            white-space: break-spaces; font-style: oblique; font-variant-numeric: tabular-nums; font-smooth: none;
            letter-spacing: 2px; word-spacing: normal; line-height: normal; caret-color: red; text-background-angle: 5
            """);

        Assert.Equal(TextAlign.Justify, styles.TextAlign);
        Assert.Equal(TextOverflow.Ellipsis, styles.TextOverflow);
        Assert.Equal(FilterMode.Point, styles.TextFilter);
        Assert.Equal(WordBreak.BreakAll, styles.WordBreak);
        Assert.Equal(TextTransform.Uppercase, styles.TextTransform);
        Assert.Equal(WhiteSpace.BreakSpaces, styles.WhiteSpace);
        Assert.Equal(FontStyle.Oblique, styles.FontStyle);
        Assert.Equal(Topten.RichTextKit.FontVariantNumeric.TabularNums, styles.FontVariantNumeric);
        Assert.Equal(FontSmooth.Never, styles.FontSmooth);
        Assert.Equal(Length.Pixels(2), styles.LetterSpacing);
        Assert.Equal(Length.Pixels(0), styles.WordSpacing);
        Assert.Equal(Length.Percent(100), styles.LineHeight);
        Assert.Equal(Color.Parse("red"), styles.CaretColor);
        Assert.Equal(Length.Pixels(5), styles.TextBackgroundAngle);
    }

    [Fact]
    public void TextDecorationShorthandCombinesLines()
    {
        var styles = Parse("text-decoration: underline overline wavy red 3px; text-decoration-skip-ink: none; text-underline-offset: 2px");

        Assert.Equal(TextDecoration.Underline | TextDecoration.Overline, styles.TextDecorationLine);
        Assert.Equal(TextDecorationStyle.Wavy, styles.TextDecorationStyle);
        Assert.Equal(Color.Parse("red"), styles.TextDecorationColor);
        Assert.Equal(Length.Pixels(3), styles.TextDecorationThickness);
        Assert.Equal(TextSkipInk.None, styles.TextDecorationSkipInk);
        Assert.Equal(Length.Pixels(2), styles.TextUnderlineOffset);

        styles.Set("text-decoration", "none");
        Assert.Equal(TextDecoration.None, styles.TextDecorationLine);
        styles.Set("text-decoration-line", "line-through");
        Assert.Equal(TextDecoration.LineThrough, styles.TextDecorationLine);
        Assert.False(styles.Set("text-decoration", "sparkly"));
    }

    [Fact]
    public void TextStrokeAndShadowParse()
    {
        var styles = Parse("text-stroke: 2px blue; text-shadow: 1px 1px 2px black");

        Assert.Equal(Length.Pixels(2), styles.TextStrokeWidth);
        Assert.Equal(Color.Parse("blue"), styles.TextStrokeColor);
        Assert.Equal(new Shadow(1, 1, 2, 0, Color.Black, false), Assert.Single(styles.TextShadow!));
        Assert.False(styles.Set("text-stroke", "2px"));
    }

    [Fact]
    public void ColorCanBeATextGradient()
    {
        var styles = Parse("color: linear-gradient(to right, red, blue 80%)");

        Assert.Null(styles.FontColor);
        Assert.Equal(GradientType.Linear, styles.TextGradient.Type);
        Assert.Equal(90, styles.TextGradient.Angle);
        Assert.Equal([0f, 0.8f, 0.8f, 1f], styles.TextGradient.Stops.Select(s => s.Offset!.Value));

        styles.Set("color", "radial-gradient(circle at 25% 75%, red, blue)");
        Assert.Equal(GradientType.Radial, styles.TextGradient.Type);
        Assert.Equal(RadialSize.Circle, styles.TextGradient.Size);
        Assert.Equal((Length.Percent(25), Length.Percent(75)), (styles.TextGradient.CenterX, styles.TextGradient.CenterY));
    }

    [Fact]
    public void FiltersParseEveryFunction()
    {
        var styles = Parse("filter: blur(4px) saturate(50%) grayscale(1) sepia(0.5) brightness(2) contrast(3) hue-rotate(90deg) invert(1) tint(red) drop-shadow(1px 2px 3px blue) border-wrap(2px green)");

        Assert.Equal(Length.Pixels(4), styles.FilterBlur);
        Assert.Equal(0, styles.FilterSaturate!.Value.GetPixels(1));
        Assert.Equal(Length.Pixels(0.5f), styles.FilterSepia);
        Assert.Equal(Length.Pixels(2), styles.FilterBrightness);
        Assert.Equal(Length.Pixels(3), styles.FilterContrast);
        Assert.Equal(Length.Pixels(90), styles.FilterHueRotate);
        Assert.Equal(Length.Pixels(1), styles.FilterInvert);
        Assert.Equal(Color.Parse("red"), styles.FilterTint);
        Assert.Equal(new Shadow(1, 2, 3, 0, Color.Parse("blue")!.Value, false), Assert.Single(styles.FilterDropShadow!));
        Assert.Equal(Length.Pixels(2), styles.FilterBorderWidth);
        Assert.Equal(Color.Parse("green"), styles.FilterBorderColor);

        styles.Set("filter", "none");
        Assert.Equal(Length.Pixels(1), styles.FilterSaturate);
        Assert.Equal(Color.White, styles.FilterTint);
        Assert.Empty(styles.FilterDropShadow!);
        Assert.False(styles.Set("filter", "wobble(1)"));
    }

    [Fact]
    public void BackdropFiltersParse()
    {
        var styles = Parse("backdrop-filter: blur(10px) brightness(0.5) contrast(2) saturate(3) sepia(1) invert(0.25) hue-rotate(45deg) grayscale(25%)");

        Assert.Equal(Length.Pixels(10), styles.BackdropFilterBlur);
        Assert.Equal(Length.Pixels(0.5f), styles.BackdropFilterBrightness);
        Assert.Equal(Length.Pixels(2), styles.BackdropFilterContrast);
        Assert.Equal(0.75f, styles.BackdropFilterSaturate!.Value.GetPixels(1), 0.0001f);
        Assert.Equal(Length.Pixels(1), styles.BackdropFilterSepia);
        Assert.Equal(Length.Pixels(0.25f), styles.BackdropFilterInvert);
        Assert.Equal(Length.Pixels(45), styles.BackdropFilterHueRotate);

        styles.Set("backdrop-filter", "none");
        Assert.Equal(Length.Pixels(0), styles.BackdropFilterBlur);
    }

    [Fact]
    public void BackgroundShorthandResetsAndSetsTheLayer()
    {
        var styles = Parse("background-size: 5px; background: rgba(255, 0, 0, 0.5) 10px 20px 30px 40px no-repeat content-box");

        Assert.Equal(Color.Parse("rgba(255, 0, 0, 0.5)"), styles.BackgroundColor);
        Assert.Equal((Length.Pixels(10), Length.Pixels(20)), (styles.BackgroundPositionX, styles.BackgroundPositionY));
        Assert.Equal((Length.Pixels(30), Length.Pixels(40)), (styles.BackgroundSizeX, styles.BackgroundSizeY));
        Assert.Equal(BackgroundRepeat.NoRepeat, styles.BackgroundRepeat);
        Assert.Equal(BackgroundClip.ContentBox, styles.BackgroundClip);

        styles.Set("background", "none");
        Assert.Equal(Color.Transparent, styles.BackgroundColor);
        Assert.Equal(BackgroundClip.BorderBox, styles.BackgroundClip);
        Assert.Null(styles.BackgroundImage);
    }

    [Fact]
    public void BackgroundLonghandsParse()
    {
        var styles = Parse("background-size: cover; background-position: right center; background-repeat: round; background-clip: text; background-blend-mode: screen; background-tint: red; background-angle: 2");

        Assert.Equal(LengthUnit.Cover, styles.BackgroundSizeX!.Value.Unit);
        Assert.Equal(LengthUnit.End, styles.BackgroundPositionX!.Value.Unit);
        Assert.Equal(LengthUnit.Center, styles.BackgroundPositionY!.Value.Unit);
        Assert.Equal(BackgroundRepeat.Clamp, styles.BackgroundRepeat);
        Assert.Equal(BackgroundClip.Text, styles.BackgroundClip);
        Assert.Equal("screen", styles.BackgroundBlendMode);
        Assert.Equal(Color.Parse("red"), styles.BackgroundTint);
        Assert.Equal(Length.Pixels(2), styles.BackgroundAngle);
    }

    [Fact]
    public void BackgroundGradientsParseForTheShader()
    {
        (string Value, GradientType Type, float Degrees, GradientCorner Corner)[] cases =
        [
            ("linear-gradient(red, blue)", GradientType.Linear, 0, GradientCorner.None),
            ("linear-gradient(90deg, red, blue)", GradientType.Linear, 90, GradientCorner.None),
            ("linear-gradient(to top, red, blue)", GradientType.Linear, 180, GradientCorner.None),
            ("linear-gradient(0.5turn, red, blue)", GradientType.Linear, 0, GradientCorner.None),
            ("linear-gradient(to bottom right, red, blue)", GradientType.Linear, 0, GradientCorner.BottomRight),
            ("radial-gradient(red, blue)", GradientType.Radial, 0, GradientCorner.None),
            ("conic-gradient(from 90deg, red, blue)", GradientType.Conic, 90, GradientCorner.None),
        ];

        foreach (var (value, type, degrees, corner) in cases)
        {
            var styles = Parse($"background-image: {value}");

            Assert.Equal(type, styles.BackgroundGradient.Type);
            Assert.Equal(degrees, float.RadiansToDegrees(styles.BackgroundGradient.Angle), 0.01f);
            Assert.Equal(corner, styles.BackgroundGradient.Corner);
            Assert.Equal(2, styles.BackgroundGradient.Stops.Count);
            Assert.Null(styles.BackgroundImage);
        }
    }

    [Fact]
    public void GradientStopsFillInMissingOffsets()
    {
        var styles = Parse("background-image: linear-gradient(red, lime 20px, blue 50%)");

        var stops = styles.BackgroundGradient.Stops;
        Assert.Equal(4, stops.Count);
        Assert.Equal((0f, false), (stops[0].Offset!.Value, stops[0].OffsetIsPixels));
        Assert.Equal((20f, true), (stops[1].Offset!.Value, stops[1].OffsetIsPixels));
        Assert.Equal((0.5f, false), (stops[2].Offset!.Value, stops[2].OffsetIsPixels));
        Assert.Equal((1f, Color.Parse("blue")!.Value), (stops[3].Offset!.Value, stops[3].Color));
    }

    [Fact]
    public void RadialAndConicPreludesParse()
    {
        var styles = Parse("background-image: radial-gradient(circle closest-side at left 30%, red, blue)");

        Assert.True(styles.BackgroundGradient.Circle);
        Assert.Equal(RadialSize.ClosestSide, styles.BackgroundGradient.Size);
        Assert.Equal((Length.Percent(0), Length.Percent(30)), (styles.BackgroundGradient.CenterX, styles.BackgroundGradient.CenterY));

        styles.Set("background-image", "conic-gradient(at 10px 20px, red, blue)");
        Assert.Equal((Length.Pixels(10), Length.Pixels(20)), (styles.BackgroundGradient.CenterX, styles.BackgroundGradient.CenterY));
    }

    [Fact]
    public void ImageUrlsResolveAgainstTheStylesheet()
    {
        var folder = Directory.CreateTempSubdirectory("socotra");
        try
        {
            var image = Path.Combine(folder.FullName, "dot.png");
            using (var bitmap = new SkiaSharp.SKBitmap(3, 2))
            using (var file = File.OpenWrite(image))
            {
                bitmap.Encode(file, SkiaSharp.SKEncodedImageFormat.Png, 100);
            }

            var sheet = Path.Combine(folder.FullName, "sheet.scss");
            File.WriteAllText(sheet, ".a { background-image: url(dot.png); mask: url('dot.png') 1px 2px / 3px repeat-x luminance; border-image: url(dot.png) 1 2 / 5px round fill; }");
            var root = new RootPanel();
            root.StyleSheet.Load(sheet);
            var panel = root.AddChild<Panel>("a");
            root.Update(new Rect(0, 0, 1920, 1080), 0);

            var style = panel.ComputedStyle!;
            Assert.Equal(3, style.BackgroundImage!.Width);
            Assert.Equal(2, style.MaskImage!.Height);
            Assert.Equal((Length.Pixels(1), Length.Pixels(2)), (style.MaskPositionX, style.MaskPositionY));
            Assert.Equal(Length.Pixels(3), style.MaskSizeX);
            Assert.Equal(BackgroundRepeat.RepeatX, style.MaskRepeat);
            Assert.Equal(MaskMode.Luminance, style.MaskMode);
            Assert.Same(style.BackgroundImage, style.BorderImageSource);
            Assert.Equal((Length.Pixels(1), Length.Pixels(2)), (style.BorderImageWidthTop, style.BorderImageWidthRight));
            Assert.Equal(Length.Pixels(5), style.BorderLeftWidth);
            Assert.Equal(BorderImageRepeat.Round, style.BorderImageRepeat);
            Assert.Equal(BorderImageFill.Filled, style.BorderImageFill);
        }
        finally
        {
            folder.Delete(true);
        }
    }

    [Fact]
    public void MissingImagesAreNull()
    {
        var styles = Parse("background-image: url(does-not-exist.png)");

        Assert.Null(styles.BackgroundImage);
    }

    [Fact]
    public void MaskGradientsAreDrawnIntoTextures()
    {
        var styles = Parse("mask-image: linear-gradient(to right, black, transparent); mask-mode: alpha; mask-scope: filter");

        var texture = styles.MaskImage!;
        Assert.Equal((1, 256), (texture.Width, texture.Height));
        Assert.Equal(255, texture.Pixels[3]);
        Assert.True(texture.Pixels[^1] < 5);
        Assert.Equal(Length.Percent(100), styles.MaskSizeX);
        Assert.Equal(BackgroundRepeat.Clamp, styles.MaskRepeat);
        Assert.Equal(float.Pi / 2, styles.MaskAngle!.Value.Value, 0.001f);
        Assert.Equal(MaskMode.Alpha, styles.MaskMode);
        Assert.Equal(MaskScope.Filter, styles.MaskScope);

        styles.Set("mask-image", "radial-gradient(white, black)");
        Assert.Equal((256, 256), (styles.MaskImage!.Width, styles.MaskImage.Height));
    }

    [Fact]
    public void MiscellaneousPropertiesParse()
    {
        var styles = Parse("""
            cursor: pointer; object-fit: scale-down; image-rendering: pixelated; content: "hi"; perspective-origin: 10px;
            transform-origin: right bottom; border-image-tint: red; filter-tint: blue
            """);

        Assert.Equal("pointer", styles.Cursor);
        Assert.Equal(ObjectFit.Contain, styles.ObjectFit);
        Assert.Equal(ImageRendering.Point, styles.ImageRendering);
        Assert.Equal("hi", styles.Content);
        Assert.Equal((Length.Pixels(10), Length.Pixels(10)), (styles.PerspectiveOriginX, styles.PerspectiveOriginY));
        Assert.Equal(LengthUnit.End, styles.TransformOriginY!.Value.Unit);
        Assert.Equal(Color.Parse("red"), styles.BorderImageTint);
        Assert.Equal(Color.Parse("blue"), styles.FilterTint);
    }

    [Fact]
    public void AnimationShorthandSortsItsParts()
    {
        var styles = Parse("animation: spin 2s cubic-bezier(0.1, 0.7, 1, 0.1) 500ms infinite alternate-reverse both paused");

        Assert.Equal("spin", styles.AnimationName);
        Assert.Equal(2, styles.AnimationDuration);
        Assert.Equal(0.5f, styles.AnimationDelay);
        Assert.Equal("cubic-bezier(0.1, 0.7, 1, 0.1)", styles.AnimationTimingFunction);
        Assert.Equal(float.PositiveInfinity, styles.AnimationIterationCount);
        Assert.Equal("alternate-reverse", styles.AnimationDirection);
        Assert.Equal("both", styles.AnimationFillMode);
        Assert.Equal("paused", styles.AnimationPlayState);

        styles.Set("animation-duration", "250ms");
        styles.Set("animation-iteration-count", "3");
        Assert.Equal(0.25f, styles.AnimationDuration);
        Assert.Equal(3, styles.AnimationIterationCount);
        styles.Set("animation", "none");
        Assert.False(styles.HasAnimation);
    }

    [Fact]
    public void ImportantIsIgnoredAndUnknownPropertiesFail()
    {
        var styles = new Styles();

        Assert.True(styles.Set("width", "10px !important"));
        Assert.Equal(Length.Pixels(10), styles.Width);
        Assert.False(styles.Set("wobble", "3"));
        Assert.False(styles.Set("width", "wide"));
        Assert.Equal(Length.Pixels(10), styles.Width);
    }

    [Fact]
    public void SetRectSetsPositionAndSize()
    {
        var styles = new Styles();

        styles.SetRect(new Rect(1, 2, 3, 4), 2);

        Assert.Equal((Length.Pixels(2), Length.Pixels(4), Length.Pixels(6), Length.Pixels(8)), (styles.Left, styles.Top, styles.Width, styles.Height));
    }

    [Fact]
    public void InsetAndOutsetIncludeUsedBorders()
    {
        var styles = Parse("padding: 1px 2px; border-width: 10px; margin: 5% 0");

        Assert.Equal(new Margin(12, 11, 12, 11), styles.GetInset(new Vector2(100)));
        Assert.Equal(new Margin(0, 10, 0, 10), styles.GetOutset(new Vector2(200)));
    }
}
