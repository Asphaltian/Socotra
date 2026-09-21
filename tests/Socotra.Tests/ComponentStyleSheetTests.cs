using System.Reflection;
using Socotra.Tests.Razor;
using static Socotra.Tests.ControlTesting;

namespace Socotra.Tests;

public class ComponentStyleSheetTests
{
    private static float TextSize<T>()
        where T : Panel, new()
    {
        var root = Root();
        var component = root.AddChild<T>();
        Update(root);
        Update(root);
        return component.Descendants.OfType<Label>().Single().ComputedStyle!.FontSize!.Value.Value;
    }

    [Fact]
    public void TheGeneratorRecordsWhereEachComponentIs()
    {
        Assert.Equal("Razor/Scoped.razor", typeof(Scoped).GetCustomAttribute<ClassFileLocationAttribute>()?.Path);
    }

    [Fact]
    public void AStylesheetBesideTheComponentLoadsByItself() => Assert.Equal(30, TextSize<Scoped>());

    [Fact]
    public void RelativePathsStartBesideTheComponent() => Assert.Equal(40, TextSize<Pointed>());

    [Fact]
    public void LeavingThePathOutLoadsTheOneBesideIt() => Assert.Equal(50, TextSize<Unnamed>());

    [Fact]
    public void PathsStartingWithASlashStartFromTheProgramFolder() => Assert.Equal(60, TextSize<Rooted>());
}
