using SkiaSharp;
using static Socotra.Tests.ControlTesting;

namespace Socotra.Tests;

public class ImageTests
{
    private const string Styles = "rootpanel { flex-direction: column; align-items: flex-start; }";

    private static void Update(RootPanel root, float height = 1080) => root.Update(new Rect(0, 0, height * 16 / 9, height), 0.016f);

    private static Image Add(Panel parent, string style = "")
    {
        var image = parent.AddChild<Image>();
        image.Texture = Texture.FromPixels(200, 100, new byte[200 * 100 * 4]);
        image.Style.Set(style);
        return image;
    }

    [Fact]
    public void ImagesTakeThePicturesSizeScaledWithTheUi()
    {
        var root = Root(Styles);
        var image = Add(root);
        Update(root);
        Assert.Equal(new Vector2(200, 100), image.Box.Rect.Size);

        Update(root, 540);
        Assert.Equal(new Vector2(100, 50), image.Box.Rect.Size);
    }

    [Fact]
    public void OneSetSideKeepsTheShape()
    {
        var root = Root(Styles);
        var wide = Add(root, "width: 100px;");
        var tall = Add(root, "height: 20px;");
        var both = Add(root, "width: 50px; height: 50px;");
        Update(root);

        Assert.Equal(new Vector2(100, 50), wide.Box.Rect.Size);
        Assert.Equal(new Vector2(40, 20), tall.Box.Rect.Size);
        Assert.Equal(new Vector2(50, 50), both.Box.Rect.Size);
    }

    [Fact]
    public void ImagesShrinkToFitTheirContainer()
    {
        var root = Root(Styles);
        var box = root.AddChild<Panel>();
        box.Style.Set("width: 80px; height: 200px; flex-direction: column; align-items: flex-start;");
        var image = Add(box, "max-width: 100%;");
        Update(root);

        Assert.Equal(new Vector2(80, 40), image.Box.Rect.Size);
    }

    [Fact]
    public void ChangingTheTextureRemeasures()
    {
        var root = Root(Styles);
        var image = Add(root);
        Update(root);

        image.Texture = Texture.FromPixels(10, 30, new byte[10 * 30 * 4]);
        Update(root);
        Assert.Equal(new Vector2(10, 30), image.Box.Rect.Size);

        image.Texture = null;
        Update(root);
        Assert.Equal(Vector2.Zero, image.Box.Rect.Size);
    }

    [Theory]
    [InlineData("contain", 200, 100)]
    [InlineData("cover", 400, 200)]
    [InlineData("fill", 200, 200)]
    [InlineData("none", 200, 100)]
    public void ObjectFitDecidesTheTileSize(string fit, float width, float height)
    {
        var root = Root(Styles);
        Add(root, $"width: 200px; height: 200px; object-fit: {fit};");
        Update(root);
        var list = new DrawList();
        root.Paint(list);

        var box = list.Batcher.Boxes.Span.ToArray().Single(instance => instance.TextureIndex > 0);
        Assert.Equal(new Vector2(width, height), new Vector2(box.BackgroundRect.Z, box.BackgroundRect.W));
    }

    [Fact]
    public void AMissingPictureShowsNothing()
    {
        var image = new Image();

        image.SetProperty("src", "no/such/picture.png");

        Assert.Null(image.Texture);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(-1, -4, 16)]
    [InlineData(65536, 65536, 0)]
    public void TexturesNeedAPositiveSizeThatMatchesThePixels(int width, int height, int bytes)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Texture.FromPixels(width, height, new byte[bytes]));
    }

    [Fact]
    public void ImageBytesDecodeToATexture()
    {
        using var bitmap = new SKBitmap(3, 2);
        bitmap.Erase(new SKColor(10, 20, 30));
        using var png = bitmap.Encode(SKEncodedImageFormat.Png, 100);

        var texture = Texture.FromImage(png.ToArray());

        Assert.Equal((3, 2), (texture.Width, texture.Height));
        Assert.Equal([10, 20, 30, 255], texture.Pixels[..4]);
        Assert.Throws<InvalidDataException>(() => Texture.FromImage([1, 2, 3]));
    }
}
