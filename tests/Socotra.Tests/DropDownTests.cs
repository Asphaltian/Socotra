using Socotra.Tests.Razor;
using static Socotra.Tests.ControlTesting;

namespace Socotra.Tests;

public class DropDownTests
{
    private static (RootPanel Root, DropDown DropDown) Build()
    {
        var root = Root();
        var dropDown = new DropDown(root);
        dropDown.Style.Width = 200;
        dropDown.Style.Height = 30;
        dropDown.Style.FlexGrow = 0;
        dropDown.Options.Add(new Option("Easy", 0));
        dropDown.Options.Add(new Option("Normal", 1));
        dropDown.Options.Add(new Option("Hard", "whatshot", 2));
        Update(root);
        return (root, dropDown);
    }

    private static List<Button> OptionButtons(RootPanel root) => [.. root.Children.OfType<Popup>().Single(popup => !popup.IsDeleting).Children.OfType<Button>()];

    [Fact]
    public void SettingTheValueShowsItsOptionWithoutAChange()
    {
        var (root, dropDown) = Build();
        var changes = 0;
        dropDown.ValueChanged = _ => changes++;

        dropDown.Value = 1;
        Update(root);

        Assert.Equal("Normal", dropDown.Text);
        Assert.Equal(1, dropDown.Value);
        Assert.Same(dropDown.Options[1], dropDown.Selected);
        Assert.Equal(0, changes);

        dropDown.Value = "2";
        Assert.Equal("Hard", dropDown.Text);
        Assert.Equal("whatshot", dropDown.Icon);
    }

    [Fact]
    public void SettingOrPickingBuildsTheOptionsOnceAndKeepsTheirValues()
    {
        var dropDown = new DropDown();
        var builds = 0;
        dropDown.BuildOptions = () =>
        {
            builds++;
            return [new Option("Easy", 0), new Option("Hard", 2)];
        };

        dropDown.Value = 0;
        Assert.Equal(1, builds);

        dropDown.Selected = dropDown.Options[1];
        Assert.Equal(1, builds);
        Assert.Equal(2, dropDown.Value);
        Assert.Equal("Hard", dropDown.Text);
    }

    [Fact]
    public void PickingFromTheListChangesTheValueAndCloses()
    {
        var (root, dropDown) = Build();
        var picked = new List<string>();
        var events = new List<string>();
        dropDown.ValueChanged = picked.Add;
        dropDown.AddEventListener("onchange", e => events.Add(e.Name));
        dropDown.AddEventListener("value.changed", e => events.Add($"{e.Name} {e.Value}"));

        Click(root, dropDown);
        Assert.True(dropDown.HasClass("open"));
        var buttons = OptionButtons(root);
        Assert.Equal(["Easy", "Normal", "Hard"], buttons.Select(button => button.Text));

        buttons[2].Click();
        Update(root);

        Assert.Equal(["2"], picked);
        Assert.Equal(["onchange", "value.changed 2"], events);
        Assert.Equal("Hard", dropDown.Text);
        Assert.Empty(root.Children.OfType<Popup>());
        Assert.False(dropDown.HasClass("open"));
    }

    [Fact]
    public void TheListMarksThePickedOptionAndIsAsWideAsTheDropDown()
    {
        var (root, dropDown) = Build();
        dropDown.Value = 1;

        dropDown.Open();
        var buttons = OptionButtons(root);
        Assert.True(buttons[1].Active);
        Assert.False(buttons[0].Active);
        Update(root);
        Update(root);

        var popup = root.Children.OfType<Popup>().Single();
        Assert.Equal(dropDown.Box.Rect.Left, popup.Box.Rect.Left);
        Assert.Equal(dropDown.Box.Rect.Bottom, popup.Box.Rect.Top);
        Assert.Equal(dropDown.Box.Rect.Width, popup.Box.Rect.Width);
    }

    [Fact]
    public void TheKeyboardOpensWalksAndPicks()
    {
        var (root, dropDown) = Build();
        dropDown.Focus();
        Update(root);

        Key(root, "enter");
        Update(root);
        var buttons = OptionButtons(root);

        Key(root, "down");
        Assert.Same(buttons[0], root.Focused);
        Key(root, "down");
        Assert.Same(buttons[1], root.Focused);
        Key(root, "enter");

        Assert.Equal(1, dropDown.Selected?.Value);
        Assert.Equal("Normal", dropDown.Text);
        Assert.Empty(root.Children.OfType<Popup>());
    }

    [Fact]
    public void EnterClosesAnOpenList()
    {
        var (root, dropDown) = Build();
        dropDown.Focus();
        Update(root);

        Key(root, "enter");
        Key(root, "enter");
        Update(root);

        Assert.False(dropDown.HasClass("open"));
        Assert.DoesNotContain(root.Children.OfType<Popup>(), popup => !popup.IsDeleting);
    }

    [Fact]
    public void EscapeClosesTheList()
    {
        var (root, dropDown) = Build();
        dropDown.Focus();
        Update(root);
        Key(root, "enter");
        Assert.True(dropDown.HasClass("open"));

        Key(root, "escape");
        Assert.False(dropDown.HasClass("open"));
        Assert.Same(dropDown, root.Focused);

        Key(root, "enter");
        Update(root);
        Key(root, "down");
        Assert.IsType<Button>(root.Focused);
        Key(root, "escape");
        Assert.False(dropDown.HasClass("open"));
    }

    [Fact]
    public void EnumAndBoolValuesFillInTheirOptions()
    {
        var root = Root();
        var days = new DropDown(root) { Value = DayOfWeek.Tuesday };
        var toggle = new DropDown(root) { Value = false };

        Assert.Equal(7, days.Options.Count);
        Assert.Equal("Tuesday", days.Text);
        Assert.Equal(["True", "False"], toggle.Options.Select(option => option.Title));
        Assert.Equal("False", toggle.Text);
    }

    [Fact]
    public void BuildOptionsMakesTheOptionsEachTimeItOpens()
    {
        var (root, dropDown) = Build();
        var builds = 0;
        dropDown.BuildOptions = () =>
        {
            builds++;
            return [new Option($"Build {builds}", builds)];
        };

        dropDown.Open();
        Update(root);

        Assert.Equal("Build 1", Assert.Single(OptionButtons(root)).Text);
    }

    [Fact]
    public void SelectMarkupReadsItsOptions()
    {
        var root = Root();
        root.AddChild<SelectPage>();
        Update(root);
        Update(root);

        var select = root.Descendants.OfType<DropDown>().Single();
        Assert.Equal("select", select.ElementName);
        Assert.Equal(["Low", "High"], select.Options.Select(option => option.Title));
        Assert.Equal(["low", "high"], select.Options.Select(option => option.Value as string));
        Assert.Equal("bolt", select.Options[1].Icon);

        select.Value = "high";
        Assert.Equal("High", select.Text);
    }

    [Fact]
    public void MarkupNamesMakeTheControls()
    {
        Assert.IsType<DropDown>(Panel.CreateElement("select"));
        Assert.IsType<DropDown>(Panel.CreateElement("dropdown"));
        Assert.IsType<ButtonGroup>(Panel.CreateElement("ButtonGroup"));
        Assert.IsType<Menu>(Panel.CreateElement("menu"));
        Assert.IsType<MenuBar>(Panel.CreateElement("menubar"));
        Assert.IsType<TabPanel>(Panel.CreateElement("tabpanel"));
        Assert.IsType<Toolbar>(Panel.CreateElement("toolbar"));
        Assert.IsType<StatusBar>(Panel.CreateElement("statusbar"));
        Assert.IsType<Form>(Panel.CreateElement("form"));
        Assert.IsType<FieldControl>(Panel.CreateElement("control"));
    }
}
