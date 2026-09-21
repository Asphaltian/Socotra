using static Socotra.Tests.ControlTesting;

namespace Socotra.Tests;

public class PopupTests
{
    private sealed class ProbePopup : BasePopup
    {
    }

    private sealed class KeyRecorder : Panel
    {
        public string? LastKey { get; private set; }

        public override void OnButtonTyped(ButtonEvent e) => LastKey = e.Button;
    }

    [Fact]
    public void ClosePopupsClosesEveryPopupButTheOneClickedIn()
    {
        var root = Root();
        var clickedIn = new ProbePopup { Parent = root };
        var inner = new Panel { Parent = clickedIn };
        var other = new ProbePopup { Parent = root };

        root.ClosePopups(inner);

        Assert.False(clickedIn.IsDeleting);
        Assert.True(other.IsDeleting);

        root.ClosePopups();
        Assert.True(clickedIn.IsDeleting);
    }

    [Fact]
    public void StayOpenKeepsAPopupOpen()
    {
        var root = Root();
        var pinned = new ProbePopup { Parent = root, StayOpen = true };

        root.ClosePopups();

        Assert.False(pinned.IsDeleting);
    }

    [Fact]
    public void PressingInOneUIDoesNotClosePopupsInAnother()
    {
        var first = Root();
        var second = Root();
        var popup = new ProbePopup { Parent = second };

        first.ClosePopups();

        Assert.False(popup.IsDeleting);
    }

    [Fact]
    public void PressingOutsideClosesThePopupAndPressingInsideDoesNot()
    {
        var root = Root();
        var source = Placed(root, 100, 100, 80, 20);
        var popup = new Popup(source, Popup.PositionMode.BelowLeft, 0);
        var option = popup.AddChild<Panel>();
        option.Style.Width = 80;
        option.Style.Height = 40;
        Update(root);
        Update(root);

        Press(root, new Vector2(120, 130));
        Assert.False(popup.IsDeleting);

        Press(root, new Vector2(600, 600));
        Assert.True(popup.IsDeleting || popup.IsDeleted);
    }

    [Fact]
    public void PositionsPlaceThePopupAgainstItsSource()
    {
        var root = Root();
        var source = Placed(root, 100, 200, 50, 20);
        Update(root);

        Popup Open(Popup.PositionMode position, float offset)
        {
            var popup = new Popup(source, position, offset);
            popup.Style.Width = 80;
            popup.Style.Height = 40;
            return popup;
        }

        var below = Open(Popup.PositionMode.BelowLeft, 4);
        var beside = Open(Popup.PositionMode.RightTop, 4);
        var above = Open(Popup.PositionMode.AboveRight, 6);
        var stretch = Open(Popup.PositionMode.BelowStretch, 0);
        stretch.Style.Width = null;
        Update(root);
        Update(root);

        Assert.Equal(new Vector2(100, 224), below.Box.Rect.Position);
        Assert.Equal(new Vector2(154, 200), beside.Box.Rect.Position);
        Assert.Equal(150, above.Box.Rect.Right);
        Assert.Equal(194, above.Box.Rect.Bottom);
        Assert.Equal(new Rect(100, 220, 50, 40), stretch.Box.Rect);
        Assert.True(below.HasClass("popup-panel") && below.HasClass("below-left"));
    }

    [Theory]
    [InlineData(Popup.PositionMode.Left, 16, 190)]
    [InlineData(Popup.PositionMode.Right, 154, 190)]
    [InlineData(Popup.PositionMode.LeftBottom, 16, 180)]
    [InlineData(Popup.PositionMode.RightBottom, 154, 180)]
    [InlineData(Popup.PositionMode.AboveLeft, 100, 156)]
    [InlineData(Popup.PositionMode.AboveCenter, 85, 156)]
    [InlineData(Popup.PositionMode.BelowCenter, 85, 224)]
    [InlineData(Popup.PositionMode.BelowRight, 70, 224)]
    public void EveryPositionPlacesThePopupWhereItsNameSays(Popup.PositionMode position, float x, float y)
    {
        var root = Root();
        var source = Placed(root, 100, 200, 50, 20);
        Update(root);
        var popup = new Popup(source, position, 4);
        popup.Style.Width = 80;
        popup.Style.Height = 40;

        Update(root);
        Update(root);

        var corner = popup.PanelPositionToScreenPosition(Vector2.Zero);
        Assert.Equal(x, corner.X, 0.01f);
        Assert.Equal(y, corner.Y, 0.01f);
    }

    [Fact]
    public void PopupsAreKeptOnTheScreen()
    {
        var root = Root();
        var source = Placed(root, 1880, 1040, 30, 30);
        var popup = new Popup(source, Popup.PositionMode.BelowLeft, 0);
        popup.Style.Width = 200;
        popup.Style.Height = 100;
        Update(root);
        Update(root);

        Assert.Equal(1910, popup.Box.Rect.Right);
        Assert.Equal(1070, popup.Box.Rect.Bottom);
    }

    [Fact]
    public void AnchoredPopupsFlipToTheSideWithRoom()
    {
        var bounds = new Rect(0, 0, 1000, 1000);
        var size = new Vector2(200, 100);

        Assert.Equal(new Vector2(50, 130), Popup.AnchorPosition(new Rect(50, 100, 10, 20), size, bounds, above: false, 10));
        Assert.Equal(new Vector2(50, 790), Popup.AnchorPosition(new Rect(50, 900, 10, 20), size, bounds, above: false, 10));
        Assert.Equal(new Vector2(800, 130), Popup.AnchorPosition(new Rect(950, 100, 10, 20), size, bounds, above: false, 10));
        Assert.Equal(new Vector2(740, 100), Popup.AnchorPosition(new Rect(950, 100, 10, 20), size, bounds, Popup.PositionMode.RightTop, 10));
    }

    [Fact]
    public void AnchorRectPlacesThePopupAtTheRectangle()
    {
        var root = Root();
        var source = Placed(root, 100, 100, 300, 30);
        Update(root);
        var popup = new Popup { AnchorRect = new Rect(150, 105, 2, 20) };
        popup.SetPositioning(source, Popup.PositionMode.BelowLeft, 4);
        popup.Style.Width = 100;
        popup.Style.Height = 50;
        Update(root);
        Update(root);

        Assert.Equal(new Vector2(150, 129), popup.Box.Rect.Position);
    }

    [Fact]
    public void PopupsDrawAboveAndCatchTheMouseBeforeLaterPanels()
    {
        var root = Root();
        var source = Placed(root, 100, 100, 80, 20);
        var popup = new Popup(source, Popup.PositionMode.BelowLeft, 0);
        popup.Style.Width = 80;
        popup.Style.Height = 60;
        var cover = Placed(root, 0, 0, 500, 500);
        Update(root);
        Update(root);

        Assert.Same(popup, root.FindPanelAt(new Vector2(120, 150)));
        Assert.Same(cover, root.FindPanelAt(new Vector2(300, 300)));
    }

    [Fact]
    public void UnhandledKeysGoToTheSourceNotTheParent()
    {
        var root = Root();
        var rootKeys = new KeyRecorder { Parent = root };
        var source = new KeyRecorder { Parent = rootKeys };
        var popup = new Popup(source, Popup.PositionMode.BelowLeft, 0);

        Assert.Same(root, popup.Parent);
        popup.OnButtonTyped(new ButtonEvent("f5", true));

        Assert.Equal("f5", source.LastKey);
        Assert.Null(rootKeys.LastKey);
    }

    [Fact]
    public void WithoutASourceKeysGoToTheParent()
    {
        var root = Root();
        var holder = new KeyRecorder { Parent = root };
        var popup = new Popup { Parent = holder };

        popup.OnButtonTyped(new ButtonEvent("f5", true));

        Assert.Equal("f5", holder.LastKey);
    }

    [Fact]
    public void DeletingTheSourceClosesAPopupThatAsksForIt()
    {
        var root = Root();
        var source = Placed(root, 0, 0, 10, 10);
        var popup = new Popup(source, Popup.PositionMode.BelowLeft, 0) { CloseWhenParentIsHidden = true };
        Update(root);

        source.Delete(true);
        Update(root);

        Assert.True(popup.IsDeleting || popup.IsDeleted);
    }

    [Fact]
    public void OptionsCloseEveryPopupAndRunTheirAction()
    {
        var root = Root();
        var source = Placed(root, 0, 0, 10, 10);
        var popup = new Popup(source, Popup.PositionMode.BelowLeft, 0);
        var picked = 0;
        var option = (Button)popup.AddOption("Rename", "edit", () => picked++);
        Update(root);

        Assert.Equal("Rename", option.Text);
        Assert.Equal("edit", option.Icon);
        option.Click();
        Update(root);

        Assert.Equal(1, picked);
        Assert.True(popup.IsDeleting || popup.IsDeleted);
    }

    [Fact]
    public void MoveSelectionWalksTheChildrenAndWraps()
    {
        var root = Root();
        var source = Placed(root, 0, 0, 10, 10);
        var popup = new Popup(source, Popup.PositionMode.BelowLeft, 0);
        var a = popup.AddOption("A");
        var b = popup.AddOption("B");

        popup.MoveSelection(1);
        Assert.Same(a, popup.SelectedChild);
        popup.MoveSelection(1);
        Assert.Same(b, popup.SelectedChild);
        Assert.False(Assert.IsType<Button>(a).Active);
        Assert.True(Assert.IsType<Button>(b).Active);
        popup.MoveSelection(1);
        Assert.Same(a, popup.SelectedChild);
        popup.MoveSelection(-1);
        Assert.Same(b, popup.SelectedChild);
    }

    [Fact]
    public void MoveSelectionSkipsTheHeader()
    {
        var popup = new Popup { Title = "Layers" };
        var a = popup.AddOption("A");

        popup.MoveSelection(-1);
        Assert.Same(a, popup.SelectedChild);
        popup.MoveSelection(1);
        Assert.Same(a, popup.SelectedChild);
    }

    [Fact]
    public void TitleAndIconMakeOneHeader()
    {
        var popup = new Popup { Title = "Layers", Icon = "layers" };

        Assert.Equal("Layers", popup.Title);
        Assert.Equal("layers", popup.Icon);
        Assert.Single(popup.Children, child => child.HasClass("header"));
    }
}
