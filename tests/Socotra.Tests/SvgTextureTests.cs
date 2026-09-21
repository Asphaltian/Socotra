namespace Socotra.Tests;

public sealed class SvgTextureTests : IDisposable
{
    private const string Square = """
        <svg width="64" height="32" xmlns="http://www.w3.org/2000/svg">
          <rect x="0" y="0" width="64" height="32" fill="#FF0000"/>
        </svg>
        """;

    private readonly string _folder = Directory.CreateTempSubdirectory().FullName;
    private readonly string _path;

    public SvgTextureTests()
    {
        _path = Path.Combine(_folder, "square.svg");
        File.WriteAllText(_path, Square);
    }

    public void Dispose() => Directory.Delete(_folder, true);

    private static (byte R, byte G, byte B, byte A) Pixel(Texture texture, int x, int y)
    {
        var i = ((y * texture.Width) + x) * 4;
        return (texture.Pixels[i], texture.Pixels[i + 1], texture.Pixels[i + 2], texture.Pixels[i + 3]);
    }

    [Fact]
    public void SvgFilesDrawAtTheirOwnSize()
    {
        var texture = Texture.FromFile(_path);

        Assert.Equal((64, 32), (texture.Width, texture.Height));
        Assert.Equal((255, 0, 0, 255), Pixel(texture, 32, 16));
    }

    [Fact]
    public void TheSizeAfterTheNameSetsTheTextureSize()
    {
        var texture = Texture.FromFile($"{_path}?w=20&h=10");

        Assert.Equal((20, 10), (texture.Width, texture.Height));
    }

    [Fact]
    public void OneSideKeepsThePicturesShape()
    {
        var wide = Texture.FromFile($"{_path}?w=32");
        var tall = Texture.FromFile($"{_path}?h=8");

        Assert.Equal((32, 16), (wide.Width, wide.Height));
        Assert.Equal((16, 8), (tall.Width, tall.Height));
    }

    [Fact]
    public void AColorPaintsThePicture()
    {
        var texture = Texture.FromFile($"{_path}?w=16&h=8&color=%2300FF00");

        Assert.Equal((0, 255, 0, 255), Pixel(texture, 8, 4));
    }

    [Fact]
    public void TheSameNameAndSizeGiveTheSameTexture()
    {
        Assert.Same(Texture.FromFile($"{_path}?w=12"), Texture.FromFile($"{_path}?w=12"));
        Assert.NotSame(Texture.FromFile($"{_path}?w=12"), Texture.FromFile($"{_path}?w=14"));
    }

    [Fact]
    public void MarkupThatIsNotSvgFailsToLoad()
    {
        var broken = Path.Combine(_folder, "broken.svg");
        File.WriteAllText(broken, "not svg");

        Assert.Throws<IOException>(() => Texture.FromFile(broken));
        Assert.Throws<FormatException>(() => Texture.FromSvg("not svg"));
    }
}
