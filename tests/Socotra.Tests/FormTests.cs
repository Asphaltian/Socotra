using static Socotra.Tests.ControlTesting;

namespace Socotra.Tests;

public class FormTests
{
    [Fact]
    public void RowsHoldALabelAndTheControl()
    {
        var form = new Form();
        var entry = new TextEntry();

        form.AddHeader("Audio", "volume_up");
        form.AddRow("Name", entry);

        var header = form.Children[0];
        Assert.True(header.HasClass("field-header"));
        Assert.Equal("volume_up", header.Children.OfType<IconPanel>().Single().Text);
        var field = Assert.IsType<Field>(form.Children[1]);
        Assert.Equal("Name", field.Children[0].Children.OfType<Label>().Single().Text);
        var control = Assert.IsType<FieldControl>(field.Children[1]);
        Assert.True(control.HasClass("field-control") && control.HasClass("control"));
        Assert.Same(control, entry.Parent);

        form.Clear();
        Assert.Equal(0, form.ChildrenCount);
    }

    [Fact]
    public void ChangesInsideTheFormBecomeFormChanged()
    {
        var root = Root();
        var outer = new Panel { Parent = root };
        var form = new Form { Parent = outer };
        var entry = new TextEntry();
        form.AddRow("Name", entry);
        var formChanges = 0;
        var outerChanges = 0;
        form.AddEventListener("form.changed", () => formChanges++);
        outer.AddEventListener("onchange", () => outerChanges++);
        Update(root);
        entry.Focus();
        Update(root);

        root.TypeText("a");
        Update(root);
        Update(root);

        Assert.Equal(1, formChanges);
        Assert.Equal(0, outerChanges);
    }

    [Fact]
    public void SlidersAndSwitchesChangeTheForm()
    {
        var root = Root();
        var form = new Form { Parent = root };
        var slider = new SliderControl(0, 100, 1);
        var toggle = new SwitchControl();
        form.AddRow("Volume", slider);
        form.AddRow("Mute", toggle);
        var formChanges = 0;
        form.AddEventListener("form.changed", () => formChanges++);
        Update(root);

        Press(root, slider.Box.Rect.Center);
        Press(root, toggle.Box.Rect.Center);

        Assert.Equal(2, formChanges);
    }

    [Fact]
    public void ButtonGroupOptionsMakeButtonsAndPickTheValue()
    {
        var root = Root();
        var changes = new List<object?>();
        var group = new ButtonGroup
        {
            Parent = root,
            ButtonClass = "choice",
            Options = [new Option("Low", "low"), new Option("Medium", "medium"), new Option("High", "high")],
            Value = "medium",
        };
        group.ValueChanged = changes.Add;
        Update(root);
        Update(root);

        var buttons = group.Children.OfType<Button>().ToList();
        Assert.Equal(["Low", "Medium", "High"], buttons.Select(button => button.Text));
        Assert.All(buttons, button => Assert.True(button.HasClass("choice")));
        Assert.Same(buttons[1], group.SelectedButton);
        Assert.True(buttons[1].HasClass("active"));

        buttons[2].Click();
        Update(root);
        Update(root);

        Assert.Equal("high", group.Value);
        Assert.Same(buttons[2], group.SelectedButton);
        Assert.False(buttons[1].HasClass("active"));
        Assert.Equal(["high"], changes);
    }

    [Fact]
    public void ButtonGroupButtonsAddedByHandTellWhenTheyArePicked()
    {
        var root = Root();
        var group = new ButtonGroup { Parent = root };
        var states = new List<string>();
        var first = group.AddButtonActive("First", on => states.Add($"first {on}"));
        var second = group.AddButtonActive("Second", on => states.Add($"second {on}"));
        second.Value = "two";

        first.Click();
        Update(root);
        second.Click();
        Update(root);
        Update(root);

        Assert.Equal(["first False", "first True", "second True"], states.Order());
        Assert.Equal("two", group.Value);
        Assert.True(second.Active);
        Assert.False(first.Active);
    }

    [Fact]
    public void ToolbarButtonsTogglesAndMenus()
    {
        var root = Root();
        var toolbar = new Toolbar { Parent = root };
        var saves = 0;
        var toggles = new List<bool>();
        var save = toolbar.AddButton("Save", "save", () => saves++);
        var grid = toolbar.AddToggle("Grid", "grid_on", true, toggles.Add);
        toolbar.AddSeparator();
        var spacer = toolbar.AddSpacer();
        var viewMenu = new Menu();
        viewMenu.AddOption("Zoom In");
        var view = toolbar.AddMenu("View", "visibility", viewMenu);
        Update(root);

        Assert.Equal("save", save.Icon);
        Assert.True(spacer.HasClass("toolbar-spacer"));
        Assert.True(grid.Active);

        save.Click();
        grid.Click();
        Update(root);
        Assert.Equal(1, saves);
        Assert.Equal([false], toggles);
        Assert.False(grid.Active);

        save.Disabled = true;
        save.Click();
        Update(root);
        Assert.Equal(1, saves);

        Click(root, view);
        Assert.True(viewMenu.IsOpen);
        Update(root);
        Assert.True(view.Active);

        toolbar.Delete(true);
        Assert.True(viewMenu.IsDeleted);
    }

    [Fact]
    public void ToolbarArrowKeysMoveBetweenButtons()
    {
        var root = Root();
        var toolbar = new Toolbar { Parent = root };
        var a = toolbar.AddButton("A", null, null);
        var b = toolbar.AddButton("B", null, null);
        var c = toolbar.AddButton("C", null, null);
        Update(root);
        a.Focus();
        Update(root);

        Key(root, "right");
        Assert.Same(b, root.Focused);
        Key(root, "end");
        Assert.Same(c, root.Focused);
        Key(root, "right");
        Assert.Same(a, root.Focused);
        Key(root, "left");
        Assert.Same(c, root.Focused);

        toolbar.Vertical = true;
        Update(root);
        Key(root, "down");
        Assert.Same(a, root.Focused);
    }

    [Fact]
    public void StatusBarMessagesHideTheLeftWidgetsUntilTheyExpire()
    {
        var root = Root();
        var bar = new StatusBar { Parent = root };
        bar.Style.Width = 600;
        var messages = new List<string>();
        bar.MessageChanged += messages.Add;
        var left = bar.AddLeft(new TextEntry { Text = "kept" });
        var right = bar.AddRight(new Label("Online"));
        Update(root);

        bar.ShowMessage("Saved", 2);
        bar.ShowMessage("Saved", 2);
        Update(root);
        Assert.Equal("Saved", bar.Message);
        Assert.False(left.IsVisible);
        Assert.True(right.IsVisible);

        Update(root, 1);
        Assert.Equal("Saved", bar.Message);
        Update(root, 1.5f);
        Update(root);
        Assert.Equal("", bar.Message);
        Assert.True(left.IsVisible);
        Assert.Equal("kept", left.Text);
        Assert.Equal(["Saved", ""], messages);

        bar.ShowMessage("Waiting", 0);
        Update(root, 100);
        Assert.Equal("Waiting", bar.Message);
        bar.ClearMessage();
        Assert.Equal("", bar.Message);
    }

    [Fact]
    public void AMessageShownBeforeTheBarIsAddedGetsItsFullTime()
    {
        var root = Root();
        Update(root, 50);
        var bar = new StatusBar();

        bar.ShowMessage("Saved", 2);
        bar.Parent = root;
        Update(root);
        Update(root, 1);

        Assert.Equal("Saved", bar.Message);
    }

    [Fact]
    public void StatusBarWidgetsCanBeTakenBack()
    {
        var bar = new StatusBar();
        var widget = bar.AddRight(new Panel(), 2);

        Assert.Equal(2, widget.Style.FlexGrow);
        Assert.True(bar.RemoveWidget(widget));
        Assert.Null(widget.Parent);
        Assert.False(widget.IsDeleted);
        Assert.False(bar.RemoveWidget(widget));
        Assert.Throws<ArgumentException>(() => bar.AddLeft(bar.Left));
        Assert.Throws<ArgumentOutOfRangeException>(() => bar.AddLeft(new Panel(), -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => bar.ShowMessage("x", float.NaN));
    }
}
