using SkiaSharp;
using static Socotra.Tests.ControlTesting;

namespace Socotra.Tests;

public sealed class StyleSheetFileTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory().FullName;

    public StyleSheetFileTests()
    {
        Directory.CreateDirectory(Path.Combine(_folder, "parts"));
        using (var bitmap = new SKBitmap(1, 1))
        using (var file = File.Create(Path.Combine(_folder, "parts", "pic.png")))
        {
            bitmap.Encode(file, SKEncodedImageFormat.Png, 100);
        }

        File.WriteAllText(Path.Combine(_folder, "parts", "part.scss"), ".pic { background-image: url(pic.png); }");
        File.WriteAllText(Path.Combine(_folder, "main.scss"), "@import \"parts/part\";");
        File.WriteAllText(Path.Combine(_folder, "sized.scss"), ".sized { width: $size; }");
    }

    public void Dispose() => Directory.Delete(_folder, true);

    private static Panel Styled(StyleSheet sheet, string classes)
    {
        var root = new RootPanel();
        root.StyleSheet.Add(sheet);
        var panel = root.AddChild<Panel>(classes);
        Update(root);
        return panel;
    }

    [Fact]
    public void ImportedFilesFindPicturesNextToThem()
    {
        var panel = Styled(StyleSheet.FromFile(Path.Combine(_folder, "main.scss")), "pic");

        Assert.Same(Texture.FromFile(Path.Combine(_folder, "parts", "pic.png")), panel.ComputedStyle!.BackgroundImage);
    }

    [Fact]
    public void StylesheetsFromStringsCanImport()
    {
        var sheet = StyleSheet.FromString($"@import \"{Path.Combine(_folder, "main.scss")}\";");

        Assert.NotNull(Styled(sheet, "pic").ComputedStyle!.BackgroundImage);
    }

    [Fact]
    public void AFileLoadedWithDifferentVariablesIsReadAgain()
    {
        var path = Path.Combine(_folder, "sized.scss");
        var small = Styled(StyleSheet.FromFile(path, [("$size", "5px")]), "sized");
        var large = Styled(StyleSheet.FromFile(path, [("$size", "9px")]), "sized");

        Assert.Equal(Length.Pixels(5), small.ComputedStyle!.Width);
        Assert.Equal(Length.Pixels(9), large.ComputedStyle!.Width);
    }
}
