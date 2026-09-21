using static Socotra.Tests.ControlTesting;

namespace Socotra.Tests;

public class IconPanelTests
{
    [Theory]
    [InlineData("iconpanel")]
    [InlineData("i")]
    public void MarkupIconsUseTheIconFont(string element)
    {
        var root = Root();
        var icon = root.AddChild(Assert.IsType<IconPanel>(Panel.CreateElement(element)));
        icon.Text = "close";
        Update(root);

        Assert.Equal("Material Icons", icon.ComputedStyle!.FontFamily);
    }
}
