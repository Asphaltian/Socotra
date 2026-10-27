using static Socotra.Tests.ControlTesting;

namespace Socotra.Tests;

public class InputTests
{
    private const string Styles = """
        rootpanel { flex-direction: column; align-items: flex-start; pointer-events: all; }
        .item { width: 100px; height: 50px; flex-shrink: 0; }
        .row { flex-direction: row; }
        .list { width: 100px; height: 100px; flex-direction: column; overflow: scroll; }
        .overlay { position: absolute; left: 0; top: 0; width: 500px; height: 500px; pointer-events: none; }
        """;

    private static List<Button> Buttons(Panel parent, int count) =>
        [.. Enumerable.Range(0, count).Select(_ => parent.AddChild<Button>("item"))];

    private static void Press(RootPanel root, params string[] buttons)
    {
        foreach (var button in buttons)
        {
            root.AddButtonEvent(new ButtonEvent(button, true));
            root.AddButtonEvent(new ButtonEvent(button, false));
        }

        Update(root);
    }

    private static void Click(RootPanel root, Vector2 position)
    {
        root.SetMousePosition(position);
        Update(root);
        root.SetMouseButton(MouseButtons.Left, true);
        root.SetMouseButton(MouseButtons.Left, false);
        Update(root);
        Update(root);
    }

    private static RootPanel FocusRoot(string styles = "")
    {
        Fonts.Load("Data/Fonts/Lato-Regular.ttf");
        return Root(ControlTesting.Styles + ".clickable { width: 100px; height: 40px; flex-shrink: 0; } .container { width: 300px; height: 200px; }" + styles);
    }

    private static T Clickable<T>(Panel parent)
        where T : Panel, new()
    {
        var panel = parent.AddChild<T>();
        panel.AddClass("clickable");
        return panel;
    }

    private static void ClickOn(RootPanel root, Panel panel)
    {
        Update(root);
        Click(root, panel.Box.Rect.Center);
    }

    [Fact]
    public void ClickingATextEntryFocusesIt()
    {
        var root = FocusRoot();
        var entry = Clickable<TextEntry>(root);

        ClickOn(root, entry);

        Assert.Same(entry, root.Focused);
    }

    [Fact]
    public void ClickingAButtonDoesNotFocusIt()
    {
        var root = FocusRoot();
        var button = Clickable<Button>(root);

        ClickOn(root, button);

        Assert.Null(root.Focused);
    }

    [Fact]
    public void EnterAfterClickingAButtonDoesNotClickItAgain()
    {
        var root = FocusRoot();
        var button = Clickable<Button>(root);
        var clicks = 0;
        button.AddEventListener("onclick", () => clicks++);

        ClickOn(root, button);
        Press(root, "enter");

        Assert.Equal(1, clicks);
    }

    [Fact]
    public void ClickingAButtonFinishesEditing()
    {
        var root = FocusRoot();
        var entry = Clickable<TextEntry>(root);
        var button = Clickable<Button>(root);
        var blurs = 0;
        entry.AddEventListener("onblur", () => blurs++);

        ClickOn(root, entry);
        Assert.Same(entry, root.Focused);
        ClickOn(root, button);

        Assert.Null(root.Focused);
        Assert.Equal(1, blurs);
    }

    [Fact]
    public void ClickingEmptySpaceClearsFocus()
    {
        var root = FocusRoot();
        var entry = Clickable<TextEntry>(root);
        var empty = Clickable<Panel>(root);

        ClickOn(root, entry);
        Assert.Same(entry, root.Focused);
        ClickOn(root, empty);

        Assert.Null(root.Focused);
    }

    [Fact]
    public void ClickingAButtonFocusesTheFocusablePanelItsIn()
    {
        var root = FocusRoot();
        var container = root.AddChild<Panel>("container");
        container.AcceptsFocus = true;
        var button = Clickable<Button>(container);

        ClickOn(root, button);

        Assert.Same(container, root.Focused);
    }

    [Fact]
    public void ClickingASuggestionKeepsTheEntryFocused()
    {
        var root = FocusRoot(".popup { pointer-events: all; } .popup > * { width: 100px; height: 20px; pointer-events: all; }");
        var entry = Clickable<TextEntry>(root);
        entry.AutoComplete = _ => ["hello"];
        var blurs = 0;
        entry.AddEventListener("onblur", () => blurs++);

        ClickOn(root, entry);
        Update(root);
        Assert.Same(entry, root.Focused);
        var popup = Assert.IsType<Popup>(entry.AutoCompletePanel);
        Update(root);
        Update(root);

        ClickOn(root, popup.GetChild(0)!);

        Assert.Equal("hello", entry.Text);
        Assert.Same(entry, root.Focused);
        Assert.Equal(0, blurs);
        Assert.Null(entry.AutoCompletePanel);
    }

    [Fact]
    public void ClickingHoversPressesFocusesAndClicks()
    {
        var root = Root(Styles);
        var (first, second) = (root.AddChild<Panel>("item"), root.AddChild<Panel>("item"));
        first.AcceptsFocus = second.AcceptsFocus = true;
        int clicks = 0;
        first.AddEventListener("onclick", () => clicks++);
        Update(root);

        root.SetMousePosition(new Vector2(50, 25));
        Update(root);

        Assert.Same(first, root.Hovered);
        Assert.True(first.HasHovered && root.HasHovered);
        Assert.False(second.HasHovered);

        root.SetMouseButton(MouseButtons.Left, true);
        Update(root);

        Assert.True(first.HasActive);
        Assert.Same(first, root.Focused);
        Assert.True(first.HasFocus);
        Assert.True(root.HasFocus);
        Assert.False(second.HasFocus);

        root.SetMouseButton(MouseButtons.Left, false);
        Update(root);
        Update(root);

        Assert.Equal(1, clicks);
        Assert.False(first.HasActive);
    }

    [Fact]
    public void ReleasingSomewhereElseIsNotAClick()
    {
        var root = Root(Styles);
        var (first, second) = (root.AddChild<Button>("item"), root.AddChild<Button>("item"));
        int clicks = 0;
        first.AddEventListener("onclick", () => clicks++);
        second.AddEventListener("onclick", () => clicks++);
        Update(root);

        root.SetMousePosition(new Vector2(50, 25));
        Update(root);
        root.SetMouseButton(MouseButtons.Left, true);
        Update(root);
        root.SetMousePosition(new Vector2(50, 75));
        Update(root);

        Assert.Same(second, root.Hovered);
        Assert.False(second.HasHovered);

        root.SetMouseButton(MouseButtons.Left, false);
        Update(root);

        Assert.Equal(0, clicks);
        Assert.False(first.HasActive || first.HasHovered);
        Assert.True(second.HasHovered);
    }

    [Fact]
    public void PointerEventsNoneAndDisabledPanelsLetTheMouseThrough()
    {
        var root = Root(Styles);
        var button = root.AddChild<Button>("item");
        root.AddChild<Panel>("overlay");
        int clicks = 0;
        button.AddEventListener("onclick", () => clicks++);
        Update(root);

        Click(root, new Vector2(50, 25));
        Assert.Same(button, root.Hovered);
        Assert.Equal(1, clicks);

        button.Disabled = true;
        Update(root);
        Click(root, new Vector2(50, 25));
        Assert.Same(root, root.Hovered);
        Assert.Equal(1, clicks);
    }

    [Fact]
    public void ArrowKeysMoveFocusToTheNearestPanelThatWay()
    {
        var root = Root(Styles);
        var column = Buttons(root, 2);
        var row = Buttons(root.AddChild<Panel>("row"), 3);
        Update(root);

        Press(root, "down");
        Assert.Same(column[0], root.Focused);

        Press(root, "down", "down");
        Assert.Same(row[0], root.Focused);

        Press(root, "right", "right", "right");
        Assert.Same(row[2], root.Focused);

        Press(root, "up");
        Assert.Same(column[1], root.Focused);

        int clicks = 0;
        column[1].AddEventListener("onclick", () => clicks++);
        Press(root, "enter", "space");
        Update(root);
        Assert.Equal(2, clicks);
    }

    [Fact]
    public void AutofocusFocusesAPanelAndNavigationComesBackToIt()
    {
        var root = Root(Styles);
        var buttons = Buttons(root, 3);
        buttons[1].SetProperty("autofocus", "");
        Update(root);

        Assert.Same(buttons[1], root.Focused);

        root.ClearFocus();
        Update(root);
        Assert.Null(root.Focused);

        Press(root, "up");
        Assert.Same(buttons[1], root.Focused);
    }

    [Fact]
    public void FocusIsVisibleOnlyWhileKeysAreInUse()
    {
        var root = Root(Styles);
        var buttons = Buttons(root, 2);
        buttons[0].SetProperty("autofocus", "");
        Update(root);

        Assert.True(buttons[0].HasFocus);
        Assert.True((buttons[0].PseudoClass & PseudoClass.FocusVisible) != 0);

        root.SetMousePosition(new Vector2(50, 75));
        Update(root);

        Assert.True(buttons[0].HasFocus);
        Assert.Equal(PseudoClass.None, buttons[0].PseudoClass & PseudoClass.FocusVisible);

        Press(root, "down");

        Assert.True(buttons[1].HasFocus);
        Assert.True((buttons[1].PseudoClass & PseudoClass.FocusVisible) != 0);
        Assert.Equal(PseudoClass.None, buttons[0].PseudoClass & PseudoClass.FocusVisible);

        root.SetMousePosition(new Vector2(50, 75));
        Update(root);
        Assert.True((buttons[1].PseudoClass & PseudoClass.FocusVisible) != 0);
    }

    [Fact]
    public void TabFollowsTabIndexThenTreeOrder()
    {
        var root = Root(Styles);
        var buttons = Buttons(root, 4);
        buttons[1].TabIndex = 2;
        buttons[2].TabIndex = 1;
        buttons[3].TabIndex = -1;
        Update(root);

        var visited = new List<Panel?>();
        for (int i = 0; i < 4; i++)
        {
            Press(root, "tab");
            visited.Add(root.Focused);
        }

        Assert.Equal([buttons[2], buttons[1], buttons[0], buttons[2]], visited);

        root.AddButtonEvent(new ButtonEvent("tab", true, KeyboardModifiers.Shift));
        Update(root);
        Assert.Same(buttons[0], root.Focused);
    }

    [Fact]
    public void AFocusedPanelCanKeepKeysFromTheRoot()
    {
        var root = Root(Styles);
        var keys = root.AddChild<KeyCatcher>("item");
        root.AddChild<Button>("item");
        keys.Focus();
        Update(root);

        Press(root, "down", "tab");

        Assert.Equal(["down", "down", "tab", "tab"], keys.Buttons);
        Assert.Same(keys, root.Focused);
    }

    [Fact]
    public void TheWheelScrollsTheListUnderTheMouse()
    {
        var root = Root(Styles);
        var list = root.AddChild<Panel>("list");
        Buttons(list, 6);
        Update(root);
        Assert.Equal(new Vector2(0, 200), list.ScrollSize);

        root.SetMousePosition(new Vector2(50, 50));
        root.AddMouseWheel(new Vector2(0, 1));
        for (int i = 0; i < 200; i++)
        {
            Update(root);
        }

        Assert.InRange(list.ScrollOffset.Y, 1, 200);
        Assert.Equal(-MathF.Round(list.ScrollOffset.Y), list.Children[0].Box.Rect.Top);

        root.AddMouseWheel(new Vector2(0, -100));
        for (int i = 0; i < 200; i++)
        {
            Update(root);
        }

        Assert.Equal(Vector2.Zero, list.ScrollOffset);
    }

    [Fact]
    public void FocusScrollsIntoView()
    {
        var root = Root(Styles);
        var list = root.AddChild<Panel>("list");
        var buttons = Buttons(list, 6);
        Update(root);

        Press(root, "down", "down", "down", "down");

        Assert.Same(buttons[3], root.Focused);
        Assert.Equal(new Vector2(0, 100), list.ScrollOffset);
        Assert.Equal(new Rect(0, 50, 100, 50), buttons[3].Box.Rect);
    }

    [Fact]
    public void EachRootKeepsItsOwnFocus()
    {
        var first = Root(Styles);
        var second = Root(Styles);
        var a = first.AddChild<Button>("item");
        var b = second.AddChild<Button>("item");
        Update(first);
        Update(second);

        Assert.True(a.Focus());
        Assert.True(b.Focus());
        Update(first);
        Update(second);

        Assert.Same(a, first.Focused);
        Assert.Same(b, second.Focused);
        Assert.True(a.HasFocus && first.HasFocus);
        Assert.True(b.HasFocus && second.HasFocus);

        a.Parent = second;

        Assert.Null(first.Focused);
        Assert.False(first.HasFocus);
        Assert.False(a.HasFocus);
        Assert.Same(b, second.Focused);
    }

    private sealed class KeyCatcher : Panel
    {
        public KeyCatcher() => AcceptsFocus = true;

        public List<string> Buttons { get; } = [];

        public override void OnButtonEvent(ButtonEvent e) => Buttons.Add(e.Button);

        public override void OnButtonTyped(ButtonEvent e)
        {
        }
    }
}
