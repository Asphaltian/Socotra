using static Socotra.Tests.ControlTesting;

namespace Socotra.Tests;

public class SvgPanelTests
{
    [Fact]
    public void MarkupSetsTheFileAndColor()
    {
        var svg = Assert.IsType<SvgPanel>(Panel.CreateElement("svg"));

        svg.SetProperty("src", "icons/save.svg");
        svg.SetProperty("color", "white");

        Assert.Equal("icons/save.svg", svg.Src);
        Assert.Equal("white", svg.Color);
    }

    [Fact]
    public void MissingOrBrokenFilesDrawNothing()
    {
        var root = new RootPanel();
        var path = Path.Combine(Path.GetTempPath(), $"socotra-{Guid.NewGuid():N}.svg");
        File.WriteAllText(path, "<svg");
        try
        {
            var broken = root.AddChild(new SvgPanel { Src = path });
            var missing = root.AddChild(new SvgPanel { Src = path + ".missing" });
            broken.Style.Set("width: 20px; height: 20px;");
            missing.Style.Set("width: 20px; height: 20px;");

            Update(root);
            Update(root);
            var list = new DrawList();
            root.Paint(list);

            Assert.Empty(list.Textures);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TheFileIsReadOnceAndDrawnAgainAtEachSize()
    {
        var root = new RootPanel();
        var path = Path.Combine(Path.GetTempPath(), $"socotra-{Guid.NewGuid():N}.svg");
        File.WriteAllText(path, """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 10 10"><rect width="10" height="10" fill="red"/></svg>""");
        var svg = root.AddChild(new SvgPanel { Src = path });
        svg.Style.Set("width: 20px; height: 20px;");
        Update(root);
        File.Delete(path);

        svg.Style.Set("width: 40px; height: 40px;");
        Update(root);
        var list = new DrawList();
        root.Paint(list);

        Assert.Contains(list.Textures, texture => texture.Width == 40);
    }
}
