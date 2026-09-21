namespace Socotra.Tests;

public class BackgroundClipTests
{
    public BackgroundClipTests() => Fonts.Load("Data/Fonts/Lato-Regular.ttf");

    private static BoxInstance[] Paint(RootPanel root)
    {
        root.Update(new Rect(0, 0, 400, 300), 0.016f);
        root.Update(new Rect(0, 0, 400, 300), 0.016f);
        var list = new DrawList();
        root.Paint(list);
        return list.Batcher.Boxes.Span.ToArray();
    }

    private static Label AddLabel(Panel parent, string text)
    {
        var label = parent.AddChild<Label>();
        label.Style.Set("display: block; font-family: Lato; font-size: 20px;");
        label.Text = text;
        return label;
    }

    [Fact]
    public void TextClippedBackgroundDrawsTheBorderOnce()
    {
        var root = new UnscaledRoot();
        var panel = root.AddChild<Panel>();
        panel.Style.Set("position: absolute; left: 40px; top: 30px; width: 200px; height: 80px; background-color: red; border: 3px solid green; background-clip: text;");
        AddLabel(panel, "Clipped background");

        var boxes = Paint(root);

        var border = Assert.Single(boxes, box => box.BorderSize != Vector4.Zero);
        Assert.Equal(new Vector4(3), border.BorderSize);
        Assert.Equal(0, border.TextMaskIndex);
        var fill = Assert.Single(boxes, box => box.BackgroundClip == (int)BackgroundClip.Text);
        Assert.Equal(new Color(1, 0, 0), fill.Color);
        Assert.Equal(Vector4.Zero, fill.BorderSize);
        Assert.True(fill.TextMaskIndex > 0);
        Assert.Equal(border.Rect, fill.Rect);
    }

    [Fact]
    public void EveryLabelLendsItsTextAndOtherClipsDrawNone()
    {
        var root = new UnscaledRoot();
        var panel = root.AddChild<Panel>();
        panel.Style.Set("width: 200px; height: 100px; background: linear-gradient(red, blue); border: 3px solid green; background-clip: text;");
        AddLabel(panel, "First");
        AddLabel(panel, "Second");

        for (int frame = 0; frame < 2; frame++)
        {
            var boxes = Paint(root);
            Assert.Single(boxes, box => box.BorderSize != Vector4.Zero);
            Assert.Equal(2, boxes.Count(box => box.BackgroundClip == (int)BackgroundClip.Text));
        }

        panel.Style.Set("background-clip: content-box; padding: 7px;");

        Assert.DoesNotContain(Paint(root), box => box.BackgroundClip == (int)BackgroundClip.Text);
    }

    [Fact]
    public void LabelsUnderTheClipDontDrawTheirOwnText()
    {
        var root = new UnscaledRoot();
        var panel = root.AddChild<Panel>();
        panel.Style.Set("width: 200px; height: 100px; background-color: red; background-clip: text;");
        AddLabel(panel, "Masked");
        var list = new DrawList();
        root.Update(new Rect(0, 0, 400, 300), 0.016f);
        root.Update(new Rect(0, 0, 400, 300), 0.016f);

        root.Paint(list);

        Assert.Equal(0, list.Batcher.Texts.Count);
    }
}
