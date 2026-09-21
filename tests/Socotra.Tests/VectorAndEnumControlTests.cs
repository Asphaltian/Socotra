using static Socotra.Tests.ControlTesting;

namespace Socotra.Tests;

public class VectorAndEnumControlTests
{
    private enum Quality
    {
        Low,
        Medium,
        High,
    }

    private enum Weekday
    {
        Monday,
        Tuesday,
        Wednesday,
        Thursday,
        Friday,
    }

    private static NumberEntry[] Entries(VectorControl control) => [.. control.Children.OfType<NumberEntry>()];

    [Fact]
    public void TheVectorTypeDecidesWhichEntriesShow()
    {
        var root = Root();
        var control = root.AddChild<VectorControl>();
        control.Value = new Vector2(1, 2);
        Update(root);

        Assert.Equal(["1", "2"], Entries(control).Where(entry => entry.IsVisible).Select(entry => entry.Value));

        control.Value = new Vector4(1, 2, 3, 4.5f);
        Update(root);

        Assert.Equal(["1", "2", "3", "4.5"], Entries(control).Where(entry => entry.IsVisible).Select(entry => entry.Value));
        Assert.Throws<ArgumentException>(() => control.Value = 5f);
    }

    [Fact]
    public void TypingAComponentReportsTheSameVectorType()
    {
        var root = Root();
        var control = root.AddChild<VectorControl>();
        control.Value = new Vector3(1, 2, 3);
        object? reported = null;
        control.ValueChanged = value => reported = value;
        Update(root);
        var y = Entries(control)[1];
        y.Focus();
        Update(root);
        Update(root);

        y.OnKeyTyped('5');

        Assert.Equal(new Vector3(1, 52, 3), reported);
        Assert.Equal(new Vector3(1, 52, 3), control.Value);
    }

    [Fact]
    public void ComponentLettersAreColored()
    {
        var root = Root();
        var control = root.AddChild<VectorControl>();
        Update(root);

        var entries = Entries(control);
        Assert.Equal(["X", "Y", "Z", "W"], entries.Select(entry => entry.Prefix));
        Assert.Equal(Color.Parse("#FB5A5A"), entries[0].PrefixLabel!.ComputedStyle!.FontColor);
        Assert.Equal(Color.Parse("#3273EB"), entries[2].PrefixLabel!.ComputedStyle!.FontColor);
        Assert.Equal(1, entries[0].ComputedStyle!.FlexGrow);
    }

    [Fact]
    public void FewValuesShowAsButtonsAndClickingOnePicksIt()
    {
        var root = Root();
        var control = root.AddChild<EnumControl>();
        control.Value = Quality.Medium;
        Enum? picked = null;
        control.ValueChanged = value => picked = value;
        Update(root);
        Update(root);

        var buttons = control.Descendants.OfType<Button>().ToArray();
        Assert.Equal(["Low", "Medium", "High"], buttons.Select(button => button.Text));
        Assert.True(buttons[1].HasClass("active"));

        Click(root, buttons[2]);
        Update(root);

        Assert.Equal(Quality.High, picked);
        Assert.Equal(Quality.High, control.Value);
    }

    [Fact]
    public void ManyValuesShowAsADropDownAndAnotherEnumRebuilds()
    {
        var root = Root();
        var control = root.AddChild<EnumControl>();
        control.Value = Weekday.Wednesday;
        Update(root);

        var dropDown = Assert.Single(control.Children.OfType<DropDown>());
        Assert.Equal(5, dropDown.Options.Count);
        Assert.Equal(Weekday.Wednesday, dropDown.Value);

        control.Value = Quality.Low;
        Update(root);

        Assert.Empty(control.Children.OfType<DropDown>());
        Assert.Single(control.Children.OfType<ButtonGroup>());
    }
}
