using static Socotra.Tests.ControlTesting;

namespace Socotra.Tests;

public class ButtonGroupTests
{
    [Fact]
    public void ClickingAButtonPicksItsValue()
    {
        var root = new RootPanel();
        var group = root.AddChild(new ButtonGroup { ButtonClass = "mode" });
        var picked = new List<string>();
        var values = new List<object?>();
        group.ValueChanged = values.Add;
        var easy = group.AddButton("Easy", () => picked.Add("easy"));
        easy.Value = "easy";
        var hard = group.AddButton("Hard", () => picked.Add("hard"));
        hard.Value = "hard";
        Assert.True(easy.HasClass("mode"));

        hard.Click();
        Update(root);
        Update(root);

        Assert.Equal(["hard"], picked);
        Assert.Equal("hard", group.Value);
        Assert.Same(hard, group.SelectedButton);
        Assert.True(hard.HasClass("active"));
        Assert.False(easy.HasClass("active"));
        Assert.Equal(["hard"], values);
    }

    [Fact]
    public void SettingTheValueMarksTheMatchingButton()
    {
        var root = new RootPanel();
        var group = root.AddChild(new ButtonGroup());
        group.AddButton("A", () => { }).Value = "a";
        var b = group.AddButton("B", () => { });
        b.Value = "b";

        group.Value = "b";
        Update(root);
        Update(root);

        Assert.True(b.HasClass("active"));
        Assert.IsType<ButtonGroup>(Panel.CreateElement("buttongroup"));
    }

    [Fact]
    public void ActiveButtonsHearWhenTheyArePickedAndLeft()
    {
        var root = new RootPanel();
        var group = root.AddChild(new ButtonGroup());
        var states = new List<bool>();
        var first = group.AddButtonActive("First", states.Add);
        var second = group.AddButtonActive("Second", _ => { });

        group.SelectedButton = first;
        group.SelectedButton = second;
        Update(root);
        Update(root);

        Assert.Equal([true, false], states);
    }

    [Fact]
    public void OptionsKeepTheirOwnValues()
    {
        var root = new RootPanel();
        var group = root.AddChild(new ButtonGroup { Options = [new Option("One", 1), new Option("Two", 2)] });
        var values = new List<object?>();
        group.ValueChanged = values.Add;
        Update(root);

        ((Button)group.Children[1]).Click();
        Update(root);
        Update(root);

        Assert.Equal([2], values);
        Assert.Equal(2, group.Value);
    }

    [Fact]
    public void ButtonsWithoutAValueAreNotPicked()
    {
        var root = new RootPanel();
        var group = root.AddChild(new ButtonGroup());
        var first = group.AddButton("First", () => { });
        var second = group.AddButton("Second", () => { });
        Update(root);

        Assert.False(first.Active || second.Active);
    }
}
