namespace Socotra.Tests;

public class ValueTests
{
    public ValueTests() => new RootPanel().Update(new Rect(0, 0, 1920, 1080), 0);

    [Theory]
    [InlineData("10px", 10)]
    [InlineData("10", 10)]
    [InlineData("50%", 100)]
    [InlineData("-2.5px", -2.5)]
    [InlineData("45deg", 45)]
    [InlineData("10 px wide", 10)]
    [InlineData("calc(100% - 20px)", 180)]
    [InlineData("calc((10px + 20px) * 2)", 60)]
    [InlineData("calc(10px + 20px * 2)", 50)]
    [InlineData("calc(2 * pi)", 6.2832)]
    [InlineData("min(50%, 150px)", 100)]
    [InlineData("max(50%, 150px)", 150)]
    [InlineData("clamp(10px, 50%, 40px)", 40)]
    [InlineData("from", 0)]
    [InlineData("to", 200)]
    public void LengthsResolveToPixels(string text, float pixels)
    {
        var length = Length.Parse(text);

        Assert.NotNull(length);
        Assert.Equal(pixels, length.Value.GetPixels(200), 0.001f);
    }

    [Theory]
    [InlineData("10vh", LengthUnit.ViewHeight)]
    [InlineData("10dvh", LengthUnit.ViewHeight)]
    [InlineData("10svh", LengthUnit.ViewHeight)]
    [InlineData("10lvh", LengthUnit.ViewHeight)]
    [InlineData("10vw", LengthUnit.ViewWidth)]
    [InlineData("10dvw", LengthUnit.ViewWidth)]
    [InlineData("10svw", LengthUnit.ViewWidth)]
    [InlineData("10lvw", LengthUnit.ViewWidth)]
    [InlineData("10vmin", LengthUnit.ViewMin)]
    [InlineData("10vmax", LengthUnit.ViewMax)]
    [InlineData("2rem", LengthUnit.RootEm)]
    [InlineData("2em", LengthUnit.Em)]
    [InlineData("auto", LengthUnit.Auto)]
    [InlineData("cover", LengthUnit.Cover)]
    [InlineData("contain", LengthUnit.Contain)]
    [InlineData("left", LengthUnit.Start)]
    [InlineData("top", LengthUnit.Start)]
    [InlineData("right", LengthUnit.End)]
    [InlineData("bottom", LengthUnit.End)]
    [InlineData("center", LengthUnit.Center)]
    [InlineData("calc(1px + 1%)", LengthUnit.Expression)]
    public void LengthUnitsParse(string text, LengthUnit unit)
    {
        Assert.Equal(unit, Length.Parse(text)?.Unit);
    }

    [Fact]
    public void TheSameCalcIsEqual()
    {
        var a = Length.Parse("calc(100% - 20px)");
        var b = Length.Parse("calc(100% - 20px)");

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, Length.Parse("calc(100% - 10px)"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("px")]
    [InlineData("10furlongs")]
    [InlineData("calc(10px +)")]
    [InlineData("clamp(1px, 2px)")]
    public void InvalidLengthsAreRejected(string text)
    {
        Assert.Null(Length.Parse(text));
    }

    [Theory]
    [InlineData("#f00", 1, 0, 0, 1)]
    [InlineData("#f008", 1, 0, 0, 136 / 255.0f)]
    [InlineData("#ff000080", 1, 0, 0, 128 / 255.0f)]
    [InlineData("rgb(255, 0, 0)", 1, 0, 0, 1)]
    [InlineData("rgb(100%, 0%, 0%)", 1, 0, 0, 1)]
    [InlineData("rgb(0 0 255 / 50%)", 0, 0, 1, 0.5f)]
    [InlineData("rgba(0, 255, 0, 0.25)", 0, 1, 0, 0.25f)]
    [InlineData("rgba(red, 0.5)", 1, 0, 0, 0.5f)]
    [InlineData("rgba(#00f, 50%)", 0, 0, 1, 0.5f)]
    [InlineData("hsl(120 100% 50%)", 0, 1, 0, 1)]
    [InlineData("hsla(240deg, 100%, 50%, 0.5)", 0, 0, 1, 0.5f)]
    [InlineData("hsl(0.5turn, 100%, 50%)", 0, 1, 1, 1)]
    [InlineData("hwb(0 0% 0%)", 1, 0, 0, 1)]
    [InlineData("hwb(120 50% 50%)", 0.5f, 0.5f, 0.5f, 1)]
    [InlineData("lab(100 0 0)", 1, 1, 1, 1)]
    [InlineData("lab(0 0 0 / 0.5)", 0, 0, 0, 0.5f)]
    [InlineData("oklch(1 0 0)", 1, 1, 1, 1)]
    [InlineData("oklch(0% 0 0)", 0, 0, 0, 1)]
    [InlineData("white", 1, 1, 1, 1)]
    [InlineData("Transparent", 0, 0, 0, 0)]
    [InlineData("darken(white, 25%)", 0.75f, 0.75f, 0.75f, 1)]
    [InlineData("lighten(#808080, 50%)", 0.7529f, 0.7529f, 0.7529f, 1)]
    [InlineData("invert(black)", 1, 1, 1, 1)]
    [InlineData("mix(black, white, 25%)", 0.25f, 0.25f, 0.25f, 1)]
    [InlineData("lerp(black, white, 0.5)", 0.5f, 0.5f, 0.5f, 1)]
    [InlineData("adjust-hue(red, 120deg)", 0, 1, 0, 1)]
    [InlineData("desaturate(red, 100%)", 1, 1, 1, 1)]
    [InlineData("saturate(#ff8080, 100%)", 1, 0.0039f, 0.0039f, 1)]
    [InlineData("color(rgb 255 0 0)", 1, 0, 0, 1)]
    [InlineData("color(blue)", 0, 0, 1, 1)]
    [InlineData("1, 0.5, 0, 1", 1, 0.5f, 0, 1)]
    [InlineData("255 128 0", 1, 128 / 255.0f, 0, 1)]
    [InlineData("black * 2", 0, 0, 0, 1)]
    public void ColorsParse(string text, float r, float g, float b, float a)
    {
        var color = Color.Parse(text);

        Assert.NotNull(color);
        Assert.Equal(r, color.Value.R, 0.002f);
        Assert.Equal(g, color.Value.G, 0.002f);
        Assert.Equal(b, color.Value.B, 0.002f);
        Assert.Equal(a, color.Value.A, 0.002f);
    }

    [Fact]
    public void BrightnessMultipliesInLinearSpace()
    {
        var color = Color.Parse("#808080 * 2")!.Value;

        Assert.Equal(0.688f, color.R, 0.002f);
        Assert.Equal(1, color.A);
    }

    [Theory]
    [InlineData("#ff")]
    [InlineData("rgb(1, 2)")]
    [InlineData("notacolor")]
    [InlineData("red blue")]
    [InlineData("currentColor")]
    public void InvalidColorsAreRejected(string text)
    {
        Assert.Null(Color.Parse(text));
    }

    [Theory]
    [InlineData("linear", 0.3f, 0.3f)]
    [InlineData("ease", 0.25f, 0.125f)]
    [InlineData("ease-in", 0.5f, 0.25f)]
    [InlineData("ease-out", 0.5f, 0.75f)]
    [InlineData("ease-in-out", 0.5f, 0.5f)]
    [InlineData("bounce-out", 1, 1)]
    [InlineData("bounce-in", 0, 0)]
    [InlineData("bounce-in-out", 0.5f, 0.5f)]
    [InlineData("sin-ease-in", 1, 1)]
    [InlineData("sin-ease-out", 0.5f, 0.7071f)]
    [InlineData("sin-ease-in-out", 0.5f, 0.5f)]
    [InlineData("step-start", 0.1f, 1)]
    [InlineData("step-end", 0.9f, 0)]
    [InlineData("steps(4)", 0.3f, 0.25f)]
    [InlineData("steps(4, start)", 0.3f, 0.5f)]
    [InlineData("cubic-bezier(0, 0, 1, 1)", 0.7f, 0.7f)]
    public void EasingFunctionsMatchTheirNames(string name, float progress, float expected)
    {
        Assert.True(Easing.TryGetFunction(name, out var function));
        Assert.Equal(expected, function(progress), 0.001f);
    }

    [Fact]
    public void UnknownEasingFallsBackToEase()
    {
        Assert.False(Easing.TryGetFunction("wobble", out _));
        Assert.Equal(Easing.QuadraticInOut(0.3f), Easing.GetFunction("wobble")(0.3f));
    }

    [Fact]
    public void TransformsBuildThreeDimensionalMatrices()
    {
        var styles = new Styles();
        Assert.True(styles.Set("transform", "translate(10px, 50%) rotate(90deg) scale(2)"));

        var matrix = styles.BuildTransformMatrix(new Vector2(100, 40));
        var point = Vector2.Transform(new Vector2(1, 0), matrix);

        Assert.Equal(10, point.X, 0.001f);
        Assert.Equal(22, point.Y, 0.001f);
    }

    [Fact]
    public void PerspectiveUsesThePerspectiveOrigin()
    {
        var styles = new Styles();
        Assert.True(styles.Set("transform", "perspective(100px) translateZ(50px)"));
        Assert.True(styles.Set("perspective-origin", "0 0"));

        var matrix = styles.BuildTransformMatrix(new Vector2(100, 100));
        var projected = Vector4.Transform(new Vector4(0, 0, 0, 1), matrix);

        Assert.Equal(0.5f, projected.W, 0.001f);
        Assert.NotEqual(Matrix4x4.Identity, matrix);
    }

    [Theory]
    [InlineData("rotateX(180deg)")]
    [InlineData("rotateY(0.5turn)")]
    [InlineData("rotate3d(10deg, 20deg, 30deg)")]
    [InlineData("scale3d(1, 2, 3)")]
    [InlineData("scaleZ(2)")]
    [InlineData("skew(10deg, 20deg)")]
    [InlineData("skewY(1rad)")]
    [InlineData("translate3d(1px, 2px, 3px)")]
    [InlineData("matrix(1, 0, 0, 1, 5, 6)")]
    [InlineData("matrix3d(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1)")]
    public void EveryTransformFunctionParses(string text)
    {
        var styles = new Styles();

        Assert.True(styles.Set("transform", text));
        Assert.False(styles.Transform!.Value.IsEmpty);
    }

    [Fact]
    public void MatrixFunctionMapsToTwoDimensions()
    {
        var styles = new Styles();
        styles.Set("transform", "matrix(2, 0, 0, 3, 5, 6)");

        var point = Vector2.Transform(new Vector2(1, 1), styles.BuildTransformMatrix(Vector2.Zero));

        Assert.Equal(new Vector2(7, 9), point);
    }

    [Fact]
    public void TransformNoneIsEmptyButSet()
    {
        var styles = new Styles();

        Assert.True(styles.Set("transform", "none"));
        Assert.True(styles.Transform!.Value.IsEmpty);
        Assert.False(styles.Set("transform", "wobble(3)"));
    }

    [Fact]
    public void ShadowListsParseInAnyOrder()
    {
        var styles = new Styles();

        Assert.True(styles.Set("box-shadow", "inset 1px 2px 3px 4px red, blue 5px 6px, 7px 8px 9px green inset"));

        Assert.Collection(styles.BoxShadow!,
            s => Assert.Equal(new Shadow(1, 2, 3, 4, Color.Parse("red")!.Value, true), s),
            s => Assert.Equal(new Shadow(5, 6, 0, 0, Color.Parse("blue")!.Value, false), s),
            s => Assert.Equal(new Shadow(7, 8, 9, 0, Color.Parse("green")!.Value, true), s));
        Assert.True(styles.Set("text-shadow", "none"));
        Assert.Empty(styles.TextShadow!);
        Assert.False(styles.Set("box-shadow", "red"));
    }

    [Fact]
    public void TransitionsParseInMilliseconds()
    {
        var styles = new Styles();

        Assert.True(styles.Set("transition", "opacity 0.2s linear 50ms, transform 1s, 300ms ease-out"));

        Assert.Collection(styles.Transitions!.List,
            t =>
            {
                Assert.Equal("opacity", t.Property);
                Assert.Equal(200, t.Duration!.Value, 0.01f);
                Assert.Equal(50, t.Delay!.Value, 0.01f);
                Assert.Equal("linear", t.TimingFunction);
            },
            t =>
            {
                Assert.Equal("transform", t.Property);
                Assert.Equal(1000, t.Duration);
                Assert.Equal("ease", t.TimingFunction);
            },
            t =>
            {
                Assert.Equal("all", t.Property);
                Assert.Equal(300, t.Duration!.Value, 0.01f);
                Assert.Equal("ease-out", t.TimingFunction);
            });
    }

    [Fact]
    public void TransitionLonghandsFillEntriesByIndex()
    {
        var styles = new Styles();

        styles.Set("transition-property", "opacity, width");
        styles.Set("transition-duration", "1s, 2s");
        styles.Set("transition-delay", "0s, 100ms");
        styles.Set("transition-timing-function", "linear, cubic-bezier(0, 0, 1, 1)");

        Assert.Collection(styles.Transitions!.List,
            t => Assert.Equal(("opacity", 1000f, 0f, "linear"), (t.Property, t.Duration!.Value, t.Delay!.Value, t.TimingFunction)),
            t => Assert.Equal(("width", 2000f, 100f, "cubic-bezier(0, 0, 1, 1)"), (t.Property, t.Duration!.Value, t.Delay!.Value, t.TimingFunction)));
    }

    [Fact]
    public void BorderShapesParse()
    {
        var styles = new Styles();

        Assert.True(styles.Set("border-shape", "polygon(0 0, 100% 0, 50% 100%)"));
        Assert.Equal(BorderShapeKind.Polygon, styles.BorderShape!.Kind);
        Assert.Equal(new BorderShapePoint(Length.Percent(50), Length.Percent(100)), styles.BorderShape.Points[2]);

        Assert.True(styles.Set("border-shape", "circle(10px at 20% 30%)"));
        Assert.Equal(BorderShapeKind.Circle, styles.BorderShape!.Kind);
        Assert.Equal(Length.Pixels(10), styles.BorderShape.CircleRadius);
        Assert.Equal(Length.Percent(30), styles.BorderShape.CircleCenterY);

        Assert.False(styles.Set("border-shape", "polygon(0 0, 1px 1px)"));
    }
}
