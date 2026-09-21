using Socotra.Tests.Razor;
using static Socotra.Tests.ControlTesting;

namespace Socotra.Tests;

public class RazorTests
{
    private static (RootPanel Root, MainMenu Menu) Build()
    {
        var root = new RootPanel();
        var menu = root.AddChild<MainMenu>();
        Update(root);
        return (root, menu);
    }

    private static IEnumerable<string> Texts(Panel panel) => panel.Descendants.OfType<Label>().Select(l => l.Text);

    [Fact]
    public void MarkupBuildsPanels()
    {
        var (_, menu) = Build();

        Assert.True(menu.HasClass("menu"));
        Assert.Collection(menu.Children,
            title => Assert.True(title.HasClass("title")),
            items => Assert.True(items.HasClass("items")),
            footer => Assert.True(footer.HasClass("footer")));

        var title = menu.Children[0];
        Assert.Equal("div", title.ElementName);
        Assert.Collection(title.Children,
            text => Assert.Equal("Main ", Assert.IsType<Label>(text).Text),
            span => Assert.Equal("span", span.ElementName));
        Assert.Equal("menu", Assert.IsType<Label>(title.Children[1].Children.Single()).Text);
    }

    [Fact]
    public void ComponentsGetParametersAttributesAndChildContent()
    {
        var (_, menu) = Build();

        var buttons = menu.Children[1].Children.Cast<MenuButton>().ToList();
        Assert.Equal(["Start", "Options"], buttons.Select(b => b.Text));
        Assert.All(buttons, b => Assert.True(b.HasClass("button") && b.HasClass("entry")));
        Assert.Equal(["Start", "START"], Texts(buttons[0]));
    }

    [Fact]
    public void ChangesReusePanelsByPosition()
    {
        var (root, menu) = Build();
        var first = menu.Children[1].Children[0];

        menu.Items.Add("Quit");
        menu.Selected = "Quit";
        Update(root);

        var buttons = menu.Children[1].Children.Cast<MenuButton>().ToList();
        Assert.Same(first, buttons[0]);
        Assert.Equal(["Start", "Options", "Quit"], buttons.Select(b => b.Text));
        Assert.True(buttons[2].Selected);
        Assert.True(buttons[2].HasClass("selected"));
        Assert.Equal("Selected Quit", menu.Children.OfType<Label>().Single().Text);

        menu.Items.RemoveAt(0);
        menu.Selected = null;
        Update(root);
        Update(root);

        Assert.Equal(["Options", "Quit"], menu.Children[1].Children.Cast<MenuButton>().Select(b => b.Text));
        Assert.Same(first, menu.Children[1].Children[0]);
        Assert.True(buttons[2].IsDeleted);
        Assert.Empty(menu.Children.OfType<Label>());
    }

    [Fact]
    public void EventsReachHandlersAndReferencesAreCaptured()
    {
        var (root, menu) = Build();

        var options = menu.Children[1].Children.Cast<MenuButton>().Single(b => b.Text == "Options");
        options.CreateEvent(new MousePanelEvent("onclick", options, "mouseleft"));
        Update(root);
        Update(root);

        Assert.Equal("Options", menu.Selected);
        Assert.True(options.Selected);

        Assert.NotNull(menu.Footer);
        menu.Footer.CreateEvent(new MousePanelEvent("onclick", menu.Footer, "mouseleft"));
        Update(root);
        Assert.Equal(1, menu.FooterClicks);
    }

    [Fact]
    public void AnElementMovedAwayComesBackInsteadOfBeingDuplicated()
    {
        var root = new RootPanel();
        var page = root.AddChild<Rebuilt>();
        Update(root);
        var moving = page.Children.Single(c => c.HasClass("moving"));

        moving.Parent = root;
        page.Count++;
        Update(root);

        Assert.Same(moving, page.Children.Single(c => c.HasClass("moving")));
        Assert.DoesNotContain(root.Children, c => c.HasClass("moving"));
    }

    [Fact]
    public void SlotMarkupKeepsItsPanelAcrossRebuilds()
    {
        var root = new RootPanel();
        var page = root.AddChild<Rebuilt>();
        Update(root);
        var split = page.Descendants.OfType<SplitContainer>().Single();
        var pane = Assert.Single(split.Left.Children);

        page.Count++;
        Update(root);

        Assert.Same(pane, Assert.Single(split.Left.Children));
        Assert.False(pane.IsDeleted);
    }

    [Fact]
    public void ControlsTakeTheirValuesFromMarkupAndReportChanges()
    {
        var root = new RootPanel();
        var page = root.AddChild<BoundControls>();
        Update(root);
        var toggle = page.Descendants.OfType<SwitchControl>().Single();
        var slider = page.Descendants.OfType<SliderControl>().Single();

        Assert.True(toggle.Value);
        Assert.Equal(4, slider.Value);
        Assert.Equal(10, slider.Max);

        toggle.DispatchEventImmediate(new MousePanelEvent("onmousedown", toggle, "mouseleft"));
        Assert.False(page.On);
    }

    [Fact]
    public void KeysKeepPanelsWithTheirItems()
    {
        var root = new RootPanel();
        var list = root.AddChild<KeyedList>();
        Update(root);
        var labels = list.Children.OfType<Label>().ToDictionary(l => l.Text);

        list.Items.Reverse();
        list.Items.RemoveAt(1);
        Update(root);

        var after = list.Children.OfType<Label>().ToList();
        Assert.Equal(["C", "A"], after.Select(l => l.Text));
        Assert.Same(labels["C"], after[0]);
        Assert.Same(labels["A"], after[1]);
        Assert.True(labels["B"].IsDeleted);
    }

    [Fact]
    public void BindingSendsValuesBothWays()
    {
        var root = new RootPanel();
        var list = root.AddChild<KeyedList>();
        Update(root);
        var field = list.Children.OfType<NameField>().Single();
        Assert.Equal("Mudkip", field.Value);

        field.Type("Treecko");
        Update(root);
        Assert.Equal("Treecko", list.Name);

        list.Name = "Torchic";
        Update(root);
        Assert.Equal("Torchic", field.Value);
    }

    [Fact]
    public void RefOnAnElementGivesItsPanel()
    {
        var root = new RootPanel();
        var refs = root.AddChild<ElementRefs>();
        Update(root);

        Assert.True(refs.Picked!.HasClass("picked"));
        Assert.IsType<Button>(refs.Pressed);

        refs.Shown = false;
        Update(root);
        Assert.Null(refs.Picked);
    }

    [Fact]
    public void EmptyTextClearsALabel()
    {
        var root = new RootPanel();
        var caption = root.AddChild<Caption>();
        Update(root);
        var label = caption.Children.OfType<Label>().Single();
        Assert.Equal("first", label.Text);

        caption.Text = "";
        Update(root);

        Assert.Equal("", label.Text);
    }

    [Fact]
    public void PanelClassesShowTheMarkupBetweenTheirTags()
    {
        var root = new RootPanel();
        var list = root.AddChild<CardList>();
        Update(root);

        var cards = list.Children.OfType<Card>().ToList();
        Assert.Equal(["card first", "card second"], cards.Select(card => card.Classes));
        Assert.Equal(["first", "second"], cards.Select(card => card.Children.OfType<Label>().Single().Text));
    }
}
