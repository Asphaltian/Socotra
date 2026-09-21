using static Socotra.Tests.ControlTesting;

namespace Socotra.Tests;

public class ButtonTests
{
    [Fact]
    public void TextAndHelpShowOnlyWhenSet()
    {
        var button = new Button("Start");

        Assert.Equal("Start", button.Text);
        Assert.True(button.HasClass("button"));
        Assert.True(button.AcceptsFocus);

        button.Help = "Begins a new game";
        Assert.Equal("Begins a new game", button.Help);

        button.SetProperty("text", "Continue");
        Assert.Equal("Continue", button.Text);

        button.SetContent("  Quit  ");
        Assert.Equal("Quit", button.Text);

        button.Text = "";
        Assert.Equal("", button.Text);
    }

    [Fact]
    public void ClickingCallsTheAction()
    {
        var root = new RootPanel();
        int clicks = 0;
        var button = root.AddChild(new Button("Go", () => clicks++));

        button.Click();
        Update(root);

        Assert.Equal(1, clicks);
    }

    [Fact]
    public void EnterAndSpaceClickTheFocusedButtonUnlessItsDisabled()
    {
        var root = new RootPanel();
        int clicks = 0;
        var button = root.AddChild(new Button("Go", () => clicks++));
        button.Focus();
        Update(root);

        root.AddButtonEvent(new ButtonEvent("enter", true));
        root.AddButtonEvent(new ButtonEvent("space", true));
        root.AddButtonEvent(new ButtonEvent("space", false));
        Update(root);
        Update(root);
        Assert.Equal(2, clicks);

        button.Disabled = true;
        root.AddButtonEvent(new ButtonEvent("enter", true));
        Update(root);
        Update(root);
        Assert.Equal(2, clicks);
    }

    [Fact]
    public void ActiveKeepsTheActiveClass()
    {
        var root = new RootPanel();
        var button = root.AddChild<Button>();
        button.Active = true;
        Update(root);
        Assert.True(button.HasClass("active"));

        button.Active = false;
        Update(root);
        Assert.False(button.HasClass("active"));

        button.SetProperty("active", "true");
        Assert.True(button.HasClass("active"));
    }

    [Fact]
    public void DeletingTheTextRemovesItsLabel()
    {
        var button = new Button("Go");

        button.DeleteText();

        Assert.Null(button.Text);
        button.Text = "ignored";
        Assert.Null(button.Text);
    }
}
