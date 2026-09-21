using static Socotra.Tests.ControlTesting;

namespace Socotra.Tests;

public class CheckboxTests
{
    private const string Styles = """
        rootpanel { flex-direction: column; align-items: flex-start; pointer-events: all; }
        .checkbox, .switchcontrol { width: 40px; height: 20px; flex-shrink: 0; }
        """;

    private static void ClickAt(RootPanel root, Vector2 position)
    {
        root.SetMousePosition(position);
        Update(root);
        root.SetMouseButton(MouseButtons.Left, true);
        Update(root);
        root.SetMouseButton(MouseButtons.Left, false);
        Update(root);
        Update(root);
    }

    [Fact]
    public void ClickingTogglesTheCheckboxAndReportsIt()
    {
        var root = Root(Styles);
        var checkbox = root.AddChild<Checkbox>();
        var changes = new List<bool>();
        var events = new List<string>();
        checkbox.ValueChanged = changes.Add;
        foreach (var name in (string[])["onchange", "onchecked", "onunchecked"])
        {
            checkbox.AddEventListener(name, e => events.Add(e.Name));
        }

        Update(root);
        ClickAt(root, new Vector2(10, 10));

        Assert.True(checkbox.Checked);
        Assert.True(checkbox.Value);
        Assert.True(checkbox.HasClass("checked"));
        Assert.Equal([true], changes);
        Assert.Equal(["onchange", "onchecked"], events);

        ClickAt(root, new Vector2(10, 10));
        Update(root);

        Assert.False(checkbox.Checked);
        Assert.False(checkbox.HasClass("checked"));
        Assert.Equal([true, false], changes);
        Assert.Equal(["onchange", "onchecked", "onchange", "onunchecked"], events);
    }

    [Fact]
    public void EnterAndSpaceToggleTheFocusedCheckbox()
    {
        var root = Root(Styles);
        var checkbox = root.AddChild<Checkbox>();
        Assert.True(checkbox.AcceptsFocus);
        checkbox.Focus();
        Update(root);

        root.AddButtonEvent(new ButtonEvent("space", true));
        Update(root);
        Update(root);
        Assert.True(checkbox.Checked);

        root.AddButtonEvent(new ButtonEvent("enter", true));
        Update(root);
        Update(root);
        Assert.False(checkbox.Checked);
    }

    [Fact]
    public void SettingCheckedFromCodeUpdatesTheClassWithoutCallingValueChanged()
    {
        var checkbox = new Checkbox();
        int calls = 0;
        checkbox.ValueChanged = _ => calls++;

        checkbox.Checked = true;
        Assert.True(checkbox.HasClass("checked"));
        checkbox.Value = false;
        Assert.False(checkbox.HasClass("checked"));
        Assert.Equal(0, calls);
    }

    [Fact]
    public void MarkupSetsTheStateAndTheLabel()
    {
        var checkbox = Assert.IsType<Checkbox>(Panel.CreateElement("checkbox"));
        Assert.Null(checkbox.Label);

        checkbox.SetProperty("checked", "true");
        checkbox.SetProperty("text", "Music");
        Assert.True(checkbox.Checked);
        Assert.Equal("Music", checkbox.LabelText);

        checkbox.SetContent("  Sound effects  ");
        Assert.Equal("Sound effects", checkbox.Label!.Text);
        Assert.IsType<IconPanel>(checkbox.CheckMark);
    }

    [Fact]
    public void PressingTheSwitchFlipsItAndReportsIt()
    {
        var root = Root(Styles);
        var toggle = root.AddChild<SwitchControl>();
        var changes = new List<bool>();
        toggle.OnValueChanged = changes.Add;
        Update(root);
        Assert.True(toggle.HasClass("inactive"));

        ClickAt(root, new Vector2(10, 10));
        Assert.True(toggle.Value);
        Assert.True(toggle.HasClass("active"));
        Assert.False(toggle.HasClass("inactive"));

        ClickAt(root, new Vector2(10, 10));
        Assert.False(toggle.Value);
        Assert.Equal([true, false], changes);
    }

    [Fact]
    public void SwitchLabelComesAndGoes()
    {
        var toggle = new SwitchControl();
        var frames = toggle.ChildrenCount;

        toggle.Label = "Fullscreen";
        Assert.Equal("Fullscreen", toggle.Label);
        Assert.Equal(frames + 1, toggle.ChildrenCount);
        Assert.True(toggle.Children.Last().HasClass("switch-label"));

        toggle.Label = null;
        Assert.Null(toggle.Label);
        Assert.Equal(frames, toggle.ChildrenCount);

        toggle.Value = true;
        Assert.True(toggle.HasClass("active"));
    }
}
