namespace Socotra.Tests;

public class EnumParityTests
{
    private static void AssertParity<TFrom, TTo>()
        where TFrom : struct, Enum
        where TTo : struct, Enum
    {
        foreach (var value in Enum.GetValues<TFrom>())
        {
            var name = value.ToString();
            Assert.True(Enum.TryParse<TTo>(name, out var other), $"{typeof(TTo).FullName} is missing {name}");
            Assert.Equal(Convert.ToInt32(value), Convert.ToInt32(other));
        }
    }

    [Fact]
    public void Display() => AssertParity<DisplayMode, Layout.Display>();

    [Fact]
    public void Position() => AssertParity<PositionMode, Layout.PositionType>();

    [Fact]
    public void FlexDirection() => AssertParity<Socotra.FlexDirection, Layout.FlexDirection>();

    [Fact]
    public void Justify() => AssertParity<Socotra.Justify, Layout.Justify>();

    [Fact]
    public void Align() => AssertParity<Socotra.Align, Layout.Align>();

    [Fact]
    public void Wrap() => AssertParity<Socotra.Wrap, Layout.Wrap>();

    [Fact]
    public void GridAutoFlow()
    {
        AssertParity<Socotra.GridAutoFlow, Layout.GridAutoFlow>();
        AssertParity<Layout.GridAutoFlow, Socotra.GridAutoFlow>();
    }
}
