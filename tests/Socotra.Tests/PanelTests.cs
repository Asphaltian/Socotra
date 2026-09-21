using static Socotra.Tests.ControlTesting;

namespace Socotra.Tests;

public class PanelTests
{
    [Fact]
    public void ChildrenCanBeAddedFoundAndCounted()
    {
        var root = new RootPanel();
        var a = root.AddChild<Panel>("a");
        Assert.True(root.AddChild<Button>(out var b, "b"));
        var c = root.AddChild(new Label());

        Assert.Equal(3, root.ChildrenCount);
        Assert.Equal(1, root.GetChildIndex(b));
        Assert.Equal(-1, root.GetChildIndex(new Panel()));
        Assert.Same(c, root.GetChild(-1, loop: true));
        Assert.Same(a, root.GetChild(3, loop: true));
        Assert.Null(root.GetChild(3));
        Assert.Equal([b], root.ChildrenOfType<Button>());
        Assert.Equal([c, b, a], root.ChildrenOfType<Panel>());
    }

    [Fact]
    public void APanelSwappedForAnotherIsGoneBeforeTheFrameIsLaidOut()
    {
        var root = Root(".slot { flex-direction: row; justify-content: center; width: 200px; } .item { width: 50px; height: 20px; }");
        var slot = root.AddChild<Panel>("slot");
        var old = slot.AddChild<Panel>("item");
        Update(root);

        old.Delete();
        var replacement = slot.AddChild<Panel>("item");
        Update(root);

        Assert.True(old.IsDeleted);
        Assert.Equal(75, replacement.Box.Rect.Left - slot.Box.Rect.Left);
    }

    [Fact]
    public void AncestorsAndSelfStartsWithThePanel()
    {
        var root = new RootPanel();
        var middle = root.AddChild<Panel>();
        var leaf = middle.AddChild<Panel>();

        Assert.Equal([leaf, middle, root], leaf.AncestorsAndSelf);
        Assert.Equal([middle, root], leaf.Ancestors);
        Assert.Same(root, leaf.FindPopupPanel());
        Assert.True(leaf.IsAncestor(root));
    }

    [Fact]
    public void LabelsAndImagesPassNewChildrenToTheirParent()
    {
        var root = new RootPanel();
        var label = root.AddChild<Label>();
        root.AddChild<Panel>();

        var panel = new Panel { Parent = label };

        Assert.Same(root, panel.Parent);
        Assert.Equal(1, root.GetChildIndex(panel));
    }

    [Fact]
    public void SortingReordersChildrenAndTheirPseudoClasses()
    {
        var root = new RootPanel();
        var panels = new[] { 3, 1, 2 }.Select(n => root.AddChild<Panel>($"n{n}")).ToArray();

        root.SortChildren(p => int.Parse(p.Classes[1..]));
        Update(root);

        Assert.Equal(["n1", "n2", "n3"], root.Children.Select(c => c.Classes));
        Assert.True(root.Children[0].PseudoClass.HasFlag(PseudoClass.FirstChild));
        Assert.True(panels[0].PseudoClass.HasFlag(PseudoClass.LastChild));

        root.SortChildren((x, y) => string.CompareOrdinal(y.Classes, x.Classes));
        Update(root);
        Assert.Equal(["n3", "n2", "n1"], root.Children.Select(c => c.Classes));
    }

    [Fact]
    public void DeletingChildrenLeavesThePanelEmpty()
    {
        var root = new RootPanel();
        var panel = root.AddChild<Panel>();
        panel.AddChild<Panel>();
        panel.AddChild<Panel>();

        panel.DeleteChildren(true);
        Update(root);

        Assert.False(panel.HasChildren);
        Assert.True(panel.PseudoClass.HasFlag(PseudoClass.Empty));
    }

    [Fact]
    public void ClassesCanBeSetToggledBoundAndFlashed()
    {
        var root = new RootPanel();
        var panel = root.AddChild<Panel>("One two");
        bool bound = false;
        panel.BindClass("bound", () => bound);

        Assert.Equal("one two", panel.Classes);
        panel.ToggleClass("two");
        panel.SetClass("three four", true);
        Assert.True(panel.HasClass("three") && panel.HasClass("four") && !panel.HasClass("two"));
        Assert.False(panel.HasClass(" "));

        bound = true;
        panel.FlashClass("flash", 0.5f);
        Update(root);
        Assert.True(panel.HasClass("bound") && panel.HasClass("flash"));

        Update(root, 0.3f);
        panel.FlashClass("flash", 0.5f);
        Update(root, 0.3f);
        Assert.True(panel.HasClass("flash"));

        Update(root, 0.3f);
        Assert.False(panel.HasClass("flash"));
    }

    [Fact]
    public void InvokesRunAfterTheirDelayUnlessCanceledOrDeleted()
    {
        var root = new RootPanel();
        var panel = root.AddChild<Panel>();
        var calls = new List<string>();
        panel.Invoke(0.1f, () => calls.Add("invoke"));
        panel.InvokeOnce("once", 0.1f, () => calls.Add("first"));
        panel.InvokeOnce("once", 0.1f, () => calls.Add("second"));
        panel.InvokeOnce("canceled", 0.1f, () => calls.Add("canceled"));
        panel.CancelInvoke("canceled");

        Update(root, 0.05f);
        Assert.Empty(calls);

        Update(root, 0.1f);
        Assert.Equal(["invoke", "second"], calls);

        panel.Invoke(0.1f, () => calls.Add("deleted"));
        panel.Delete(true);
        Update(root, 1);
        Assert.Equal(["invoke", "second"], calls);
    }

    [Fact]
    public void DeletionCancelsTheTokenAndUserDataStays()
    {
        var root = new RootPanel();
        var panel = root.AddChild<Panel>();
        panel.UserData = 42;
        var token = panel.DeletionToken;

        panel.Delete(true);

        Assert.True(token.IsCancellationRequested);
        Assert.Equal(CancellationToken.None, panel.DeletionToken);
        Assert.Equal(42, panel.UserData);
    }

    [Fact]
    public void AFailingDeletionDoesNotStopTheOthers()
    {
        var root = new RootPanel();
        var other = root.AddChild<Panel>();
        var failing = root.AddChild<FailingDelete>();
        Update(root);

        other.Delete();
        failing.Delete();
        Update(root);

        Assert.True(other.IsDeleted);
    }

    private sealed class FailingDelete : Panel
    {
        public override void Delete(bool immediate = false)
        {
            if (immediate)
            {
                throw new InvalidOperationException("Can't delete.");
            }

            base.Delete(immediate);
        }
    }

    [Fact]
    public void AChildOfADeletedPanelCanBeAddedElsewhere()
    {
        var root = new RootPanel();
        var parent = root.AddChild<Panel>();
        var child = parent.AddChild<Panel>();

        parent.Delete(true);
        child.Parent = root;
        Update(root);

        Assert.Same(root, child.Parent);
    }

    [Fact]
    public void DeletingAPanelCancelsItsChildrensTokens()
    {
        var root = new RootPanel();
        var panel = root.AddChild<Panel>();
        var token = panel.AddChild<Panel>().AddChild<Panel>().DeletionToken;

        panel.Delete(true);

        Assert.True(token.IsCancellationRequested);
    }

    [Fact]
    public void AttributesSetMatchingPropertiesFromText()
    {
        var panel = new Settings();

        panel.SetProperty("volume", "0.25");
        panel.SetProperty("count", "7");
        panel.SetProperty("mode", "wide");
        panel.SetProperty("muted", "muted");
        panel.SetProperty("title", "Options");
        panel.SetProperty("unknown", "x");

        Assert.Equal(0.25f, panel.Volume);
        Assert.Equal(7, panel.Count);
        Assert.Equal(Mode.Wide, panel.Mode);
        Assert.True(panel.Muted);
        Assert.Equal("Options", panel.Title);
        Assert.Equal("x", panel.GetAttribute("unknown"));
        Assert.Equal("fallback", panel.GetAttribute("missing", "fallback"));

        panel.SetProperty("muted", null);
        Assert.False(panel.Muted);
        Assert.Null(panel.GetAttribute("muted"));
    }

    [Theory]
    [InlineData("text", typeof(Label))]
    [InlineData("icon", typeof(IconPanel))]
    [InlineData("i", typeof(IconPanel))]
    [InlineData("img", typeof(Image))]
    public void MarkupAliasesMakeTheirPanels(string element, Type type)
    {
        Assert.IsType(type, Panel.CreateElement(element));
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("disabled", true)]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData(null, false)]
    public void TheDisabledAttributeReadsItsValue(string? value, bool disabled)
    {
        var panel = new Panel();

        panel.SetProperty("disabled", value);

        Assert.Equal(disabled, panel.Disabled);
    }

    [Fact]
    public void ClassAttributesReplaceTheLastOnes()
    {
        var panel = new Panel();
        panel.AddClass("kept");

        panel.SetProperty("class", "a b");
        panel.SetProperty("class", "c");

        Assert.Equal(["kept", "c"], panel.Class);
    }

    [Fact]
    public void PositionsConvertBetweenScreenAndPanel()
    {
        var root = new RootPanel();
        root.StyleSheet.Parse(".box { position: absolute; left: 100px; top: 50px; width: 200px; height: 100px; }");
        var box = root.AddChild<Panel>("box");
        Update(root);

        Assert.Equal(new Vector2(50, 25), box.ScreenPositionToPanelPosition(new Vector2(150, 75)));
        Assert.Equal(new Vector2(0.25f, 0.25f), box.ScreenPositionToPanelDelta(new Vector2(150, 75)));
        Assert.Equal(new Vector2(150, 75), box.PanelPositionToScreenPosition(new Vector2(50, 25)));
        Assert.Equal([root, box], root.FindInRect(new Rect(0, 0, 400, 400), fullyInside: false));
        Assert.Equal([box], box.FindInRect(new Rect(90, 40, 300, 300), fullyInside: true));
        Assert.Empty(root.FindInRect(new Rect(90, 40, 300, 300), fullyInside: true));
    }

    [Fact]
    public void AddMakesCommonPanels()
    {
        var root = new RootPanel();

        var panel = root.Add.Panel("header");
        var label = root.Add.Label("Hello", "title");

        Assert.True(panel.HasClass("header"));
        Assert.Equal("Hello", label.Text);
        Assert.True(label.HasClass("title"));
        Assert.Equal([panel, label], root.Children);
    }

    private enum Mode
    {
        Narrow,
        Wide,
    }

    private sealed class Settings : Panel
    {
        public float Volume { get; set; }

        public int Count { get; set; }

        public Mode Mode { get; set; }

        public bool Muted { get; set; }

        public string? Title { get; set; }
    }
}
