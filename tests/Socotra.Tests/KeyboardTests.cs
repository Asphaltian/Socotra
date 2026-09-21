using static Socotra.Tests.ControlTesting;

namespace Socotra.Tests;

public class KeyboardTests
{
    private static (RootPanel Root, Field Field) Focused()
    {
        var root = new RootPanel();
        var field = root.AddChild<Field>();
        field.Focus();
        Update(root);
        return (root, field);
    }

    [Fact]
    public void ButtonNamesAreLowerCaseWithoutAKeyPrefix()
    {
        var e = new ButtonEvent("KEY_A", true, KeyboardModifiers.Ctrl | KeyboardModifiers.Shift, 65);

        Assert.Equal("a", e.Button);
        Assert.Equal(65, e.VirtualKey);
        Assert.True(e.HasCtrl && e.HasShift && !e.HasAlt);
        Assert.Equal("a pressed", e.ToString());
    }

    [Fact]
    public void TypedTextReachesTheFocusedPanelWithModifiers()
    {
        var (root, field) = Focused();

        root.AddButtonEvent(new ButtonEvent("shift", true, KeyboardModifiers.Shift));
        root.TypeText("Hi");
        Update(root);

        Assert.Equal("Hi", field.Typed);
        Assert.Equal([KeyboardModifiers.Shift, KeyboardModifiers.Shift], field.TypedModifiers);
    }

    [Fact]
    public void PressesAreTypedButtonsButReleasesAreNot()
    {
        var (root, field) = Focused();

        root.AddButtonEvent(new ButtonEvent("a", true));
        root.AddButtonEvent(new ButtonEvent("a", true));
        root.AddButtonEvent(new ButtonEvent("a", false));
        Update(root);

        Assert.Equal(["a pressed", "a pressed", "a released"], field.Buttons);
        Assert.Equal(["a", "a"], field.TypedButtons);
    }

    [Fact]
    public void TabIsHandledAsATypedButton()
    {
        var root = new RootPanel();
        var first = root.AddChild<Button>();
        var second = root.AddChild<Button>();
        Update(root);

        root.AddButtonEvent(new ButtonEvent("tab", true));
        Update(root);
        Assert.Same(first, root.Focused);

        first.FocusNext();
        Update(root);
        Assert.Same(second, root.Focused);

        second.FocusPrevious();
        Update(root);
        Assert.Same(first, root.Focused);
    }

    [Fact]
    public void ImeCompositionSendsStartPreviewAndEnd()
    {
        var (root, field) = Focused();

        root.SetImeComposition("に");
        Update(root);
        root.SetImeComposition("にほ");
        Update(root);
        root.SetImeComposition(null);
        root.TypeText("日本");
        Update(root);
        Update(root);

        Assert.Equal(["onimestart", "onime に", "onime にほ", "onime ", "onimeend"], field.Ime);
        Assert.Equal("日本", field.Typed);
        Assert.Equal(field.Box.Rect, root.Focused!.ImeCaretRect);
    }

    [Fact]
    public void CtrlCAndCtrlXCopyTheFocusedPanelsValue()
    {
        var copied = new List<string>();
        Clipboard.SetTextHandler = copied.Add;
        try
        {
            var (root, field) = Focused();
            field.Value = "selected";

            root.AddButtonEvent(new ButtonEvent("c", true, KeyboardModifiers.Ctrl));
            root.AddButtonEvent(new ButtonEvent("x", true, KeyboardModifiers.Ctrl));
            root.AddButtonEvent(new ButtonEvent("c", true, KeyboardModifiers.Ctrl | KeyboardModifiers.Shift));
            Update(root);
            Update(root);

            Assert.Equal(["selected", "selected"], copied);
            Assert.Equal([false, true], field.Cuts);
            Assert.Equal(["c"], field.TypedButtons);
        }
        finally
        {
            Clipboard.SetTextHandler = null;
        }
    }

    [Fact]
    public void CtrlVPastesTheClipboardIntoTheFocusedPanel()
    {
        Clipboard.GetTextHandler = () => "pasted";
        try
        {
            var (root, field) = Focused();

            root.AddButtonEvent(new ButtonEvent("v", true, KeyboardModifiers.Ctrl));
            Update(root);
            Update(root);

            Assert.Equal(["pasted"], field.Pasted);
            Assert.Empty(field.TypedButtons);
        }
        finally
        {
            Clipboard.GetTextHandler = null;
        }
    }

    [Fact]
    public void WithoutAHostTheClipboardIsEmpty()
    {
        Clipboard.SetText("ignored");

        Assert.Null(Clipboard.GetText());
    }

    [Fact]
    public void EscapeEventsBlurTheFocusedPanel()
    {
        var (root, field) = Focused();

        field.CreateEvent("onescape");
        Update(root);
        Update(root);

        Assert.Null(root.Focused);
    }

    private sealed class Field : Panel
    {
        public Field() => AcceptsFocus = true;

        public string Value { get; set; } = "";

        public string Typed { get; private set; } = "";

        public List<KeyboardModifiers> TypedModifiers { get; } = [];

        public List<string> Buttons { get; } = [];

        public List<string> TypedButtons { get; } = [];

        public List<string> Ime { get; } = [];

        public List<string> Pasted { get; } = [];

        public List<bool> Cuts { get; } = [];

        public override bool AcceptsImeInput => true;

        public override void OnKeyTyped(char k, KeyboardModifiers modifiers)
        {
            Typed += k;
            TypedModifiers.Add(modifiers);
        }

        public override void OnButtonEvent(ButtonEvent e) => Buttons.Add(e.ToString());

        public override void OnButtonTyped(ButtonEvent e) => TypedButtons.Add(e.Button);

        public override void OnPaste(string text) => Pasted.Add(text);

        public override string? GetClipboardValue(bool cut)
        {
            Cuts.Add(cut);
            return Value;
        }

        protected override void OnEvent(PanelEvent e)
        {
            if (e.Name.StartsWith("onime", StringComparison.Ordinal))
            {
                Ime.Add(e.Value is null ? e.Name : $"{e.Name} {e.Value}");
            }

            base.OnEvent(e);
        }
    }

    [Fact]
    public void AControlThatHandlesArrowsKeepsFocus()
    {
        var root = new RootPanel();
        root.StyleSheet.Parse(".box { width: 100px; height: 50px; flex-shrink: 0; }");
        var arrows = root.AddChild<ArrowCatcher>("box");
        var below = root.AddChild<Button>("box");
        Update(root);
        arrows.Focus();
        Update(root);

        root.AddButtonEvent(new ButtonEvent("down", true));
        root.AddButtonEvent(new ButtonEvent("down", false));
        Update(root);

        Assert.Same(arrows, root.Focused);
        Assert.Equal(["down"], arrows.Typed);
        Assert.False(below.HasFocus);
    }

    [Fact]
    public void PanelsInsideAHiddenPanelCantTakeFocus()
    {
        var root = new RootPanel();
        var hidden = root.AddChild<Panel>();
        hidden.Style.Display = DisplayMode.None;
        var button = hidden.AddChild<Button>();
        Update(root);

        button.Focus();
        Update(root);

        Assert.NotNull(button.ComputedStyle);
        Assert.Null(root.Focused);
    }

    [Fact]
    public void AFocusedPanelMovedToAnotherUILosesFocus()
    {
        var (root, field) = Focused();
        var blurs = 0;
        field.AddEventListener("onblur", () => blurs++);

        var other = new RootPanel();
        field.Parent = other;
        root.TypeText("x");
        Update(root);
        Update(other);

        Assert.Null(root.Focused);
        Assert.False(field.HasFocus);
        Assert.Equal(1, blurs);
        Assert.Equal("", field.Typed);
    }

    [Fact]
    public void EscapeSendsOnescapeOnceWhileHeld()
    {
        var (root, field) = Focused();
        var escapes = 0;
        field.AddEventListener("onescape", () => escapes++);

        root.AddButtonEvent(new ButtonEvent("escape", true));
        root.AddButtonEvent(new ButtonEvent("escape", true));
        root.AddButtonEvent(new ButtonEvent("escape", false));
        Update(root);
        Update(root);

        Assert.Equal(1, escapes);
        Assert.Empty(field.Buttons);
        Assert.Empty(field.TypedButtons);
    }

    [Fact]
    public void StoppedButtonsDontReachTheParent()
    {
        var root = new RootPanel();
        var field = root.AddChild<Field>();
        var stopper = field.AddChild<Stopper>();
        stopper.Focus();
        Update(root);

        root.AddButtonEvent(new ButtonEvent("a", true));
        Update(root);

        Assert.Empty(field.Buttons);
        Assert.Empty(field.TypedButtons);
    }

    private sealed class Stopper : Panel
    {
        public Stopper() => AcceptsFocus = true;

        public override void OnButtonEvent(ButtonEvent e)
        {
            e.StopPropagation = true;
            base.OnButtonEvent(e);
        }

        public override void OnButtonTyped(ButtonEvent e)
        {
            e.StopPropagation = true;
            base.OnButtonTyped(e);
        }
    }

    private sealed class ArrowCatcher : Panel
    {
        public ArrowCatcher() => AcceptsFocus = true;

        public List<string> Typed { get; } = [];

        public override void OnButtonTyped(ButtonEvent e)
        {
            if (e.Pressed && e.Button is "up" or "down")
            {
                Typed.Add(e.Button);
                return;
            }

            base.OnButtonTyped(e);
        }
    }
}
