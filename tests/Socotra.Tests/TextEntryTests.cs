using static Socotra.Tests.ControlTesting;

namespace Socotra.Tests;

public class TextEntryTests
{
    public TextEntryTests() => Fonts.Load("Data/Fonts/Lato-Regular.ttf");

    private const string Styles = "rootpanel { flex-direction: column; align-items: flex-start; font-family: Lato; font-size: 20px; pointer-events: all; } .textentry { width: 300px; }";

    private static (RootPanel Root, TestEntry Entry) Focused(string text = "")
    {
        var root = Root(Styles);
        var entry = root.AddChild<TestEntry>();
        entry.Text = text;
        Update(root);
        entry.Focus();
        Update(root);
        Update(root);
        return (root, entry);
    }

    private static void DragSelection(RootPanel root, Vector2 grab, Vector2 landing)
    {
        root.SetMousePosition(grab);
        Update(root);
        root.SetMouseButton(MouseButtons.Left, true);
        Update(root);
        Update(root);
        root.SetMousePosition(grab + new Vector2(8, 0));
        Update(root);
        Update(root);
        root.SetMousePosition(landing);
        Update(root);
        Update(root);
        root.SetMouseButton(MouseButtons.Left, false);
        Update(root);
        Update(root);
    }

    private static void Type(TextEntry entry, string text)
    {
        foreach (var c in text)
        {
            entry.OnKeyTyped(c);
        }
    }

    private static void Press(TextEntry entry, string button, KeyboardModifiers modifiers = KeyboardModifiers.None) =>
        entry.OnButtonTyped(new ButtonEvent(button, true, modifiers));

    [Fact]
    public void TypedTextGoesThroughTheRootToTheFocusedEntry()
    {
        var (root, entry) = Focused();
        var edits = new List<string>();
        entry.OnTextEdited = edits.Add;
        Assert.Same(entry, root.Focused);

        root.TypeText("héllo");
        Update(root);
        Assert.Equal("héllo", entry.Text);
        Assert.Equal(5, entry.CaretPosition);
        Assert.Equal("héllo", edits[^1]);

        root.AddButtonEvent(new ButtonEvent("backspace", true));
        root.AddButtonEvent(new ButtonEvent("backspace", false));
        Update(root);
        Assert.Equal("héll", entry.Text);
    }

    [Fact]
    public void ArrowsHomeAndEndMoveTheCaret()
    {
        var (_, entry) = Focused("one two three");
        entry.CaretPosition = 13;

        Press(entry, "left");
        Assert.Equal(12, entry.CaretPosition);
        Press(entry, "left", KeyboardModifiers.Ctrl);
        Assert.Equal(8, entry.CaretPosition);
        Press(entry, "home");
        Assert.Equal(0, entry.CaretPosition);
        Press(entry, "right", KeyboardModifiers.Ctrl);
        Assert.Equal(3, entry.CaretPosition);
        Press(entry, "end");
        Assert.Equal(13, entry.CaretPosition);
    }

    [Fact]
    public void ShiftSelectsAndTypingReplacesTheSelection()
    {
        var (_, entry) = Focused("hello world");
        entry.CaretPosition = 11;
        Press(entry, "left", KeyboardModifiers.Shift | KeyboardModifiers.Ctrl);
        Assert.Equal("world", entry.Selected);

        Press(entry, "left");
        Assert.Equal(6, entry.CaretPosition);
        Assert.False(entry.ContentLabel.HasSelection());

        Press(entry, "end", KeyboardModifiers.Shift);
        Type(entry, "there");
        Assert.Equal("hello there", entry.Text);

        Press(entry, "a", KeyboardModifiers.Ctrl);
        Assert.Equal("hello there", entry.Selected);
        Press(entry, "delete");
        Assert.Equal("", entry.Text);
    }

    [Fact]
    public void CtrlBackspaceAndDeleteRemoveWords()
    {
        var (_, entry) = Focused("one two three");
        entry.CaretPosition = 7;
        Press(entry, "backspace", KeyboardModifiers.Ctrl);
        Assert.Equal("one  three", entry.Text);
        Press(entry, "delete", KeyboardModifiers.Ctrl);
        Assert.Equal("one three", entry.Text);
    }

    [Fact]
    public void ClipboardCopiesCutsAndPastes()
    {
        var (_, entry) = Focused("copy me");
        entry.ContentLabel.SetSelection(0, 4);
        Assert.Equal("copy", entry.GetClipboardValue(false));
        Assert.Equal("copy me", entry.Text);

        Assert.Equal("copy", entry.GetClipboardValue(true));
        Assert.Equal(" me", entry.Text);

        entry.CaretPosition = 0;
        entry.OnPaste("paste");
        Assert.Equal("paste me", entry.Text);
        Assert.Equal(5, entry.CaretPosition);
    }

    [Fact]
    public void UndoTakesBackTypingRunsAndRedoPutsThemBack()
    {
        var (root, entry) = Focused();
        Type(entry, "hello");
        Update(root, 2);
        Type(entry, "world");
        Assert.True(entry.CanUndo);

        Press(entry, "z", KeyboardModifiers.Ctrl);
        Assert.Equal("hello", entry.Text);
        Press(entry, "z", KeyboardModifiers.Ctrl);
        Assert.Equal("", entry.Text);
        Assert.False(entry.CanUndo);

        Press(entry, "y", KeyboardModifiers.Ctrl);
        Assert.Equal("hello", entry.Text);
        Press(entry, "z", KeyboardModifiers.Ctrl | KeyboardModifiers.Shift);
        Assert.Equal("helloworld", entry.Text);
        Assert.False(entry.CanRedo);

        entry.ClearUndoHistory();
        Assert.False(entry.CanUndo);
    }

    [Fact]
    public void ValidationLimitsWhatCanBeEntered()
    {
        var (_, entry) = Focused();
        entry.MaxLength = 3;
        Type(entry, "abcdef");
        Assert.Equal("abc", entry.Text);

        entry.Text = "";
        entry.MaxLength = null;
        entry.CharacterRegex = "[a-z]";
        Type(entry, "a1b");
        Assert.Equal("ab", entry.Text);

        entry.CharacterRegex = null;
        entry.MinLength = 5;
        entry.UpdateValidation();
        Assert.True(entry.HasValidationErrors);
        Assert.True(entry.HasClass("invalid"));
        Type(entry, "cde");
        Assert.False(entry.HasValidationErrors);

        entry.StringRegex = "^x";
        entry.UpdateValidation();
        Assert.True(entry.HasValidationErrors);
    }

    [Fact]
    public void NumericEntriesOnlyTakeNumbers()
    {
        var (root, entry) = Focused();
        entry.Numeric = true;
        entry.MinValue = 0;
        entry.MaxValue = 10;
        Type(entry, "-1a2.5.3");
        Assert.Equal("12.53", entry.Text);

        entry.Blur();
        Update(root);
        Update(root);
        Assert.Equal("10", entry.Text);

        entry.WholeNumbers = true;
        entry.Text = "7.6";
        Assert.Equal("8", entry.FixNumeric());
    }

    [Theory]
    [InlineData("5")]
    [InlineData("5,0")]
    public void NumberEntryScrubsItsValueByDraggingThePrefix(string start)
    {
        var root = Root(Styles);
        var entry = root.AddChild<NumberEntry>();
        entry.Prefix = "X";
        entry.MinValue = 0;
        entry.MaxValue = 1000;
        entry.WholeNumbers = true;
        entry.Text = start;
        Update(root);

        var handle = entry.PrefixLabel!.Box.Rect.Center;
        root.SetMousePosition(handle);
        Update(root);
        root.SetMouseButton(MouseButtons.Left, true);
        Update(root);
        Update(root);
        root.SetMousePosition(handle + new Vector2(20, 0));
        Update(root);
        Update(root);
        root.SetMouseButton(MouseButtons.Left, false);
        Update(root);

        Assert.Equal("25", entry.Text);
        Assert.Null(root.Focused);
    }

    [Fact]
    public void SurrogatePairsArriveAsOneCharacter()
    {
        var (root, entry) = Focused();

        root.TypeText("a\U0001F600b");
        Update(root);

        Assert.Equal("a\U0001F600b", entry.Text);
        Assert.Equal(3, entry.TextLength);
    }

    [Fact]
    public void KeysTypedInOneFrameKeepTheirOrder()
    {
        var (root, entry) = Focused();

        root.TypeText("a");
        root.AddButtonEvent(new ButtonEvent("backspace", true));
        root.AddButtonEvent(new ButtonEvent("backspace", false));
        root.TypeText("b");
        Update(root);

        Assert.Equal("b", entry.Text);
    }

    [Fact]
    public void TheScrubCursorGivesBackYourOwn()
    {
        var root = Root(Styles);
        var entry = root.AddChild<NumberEntry>();
        entry.Prefix = "X";
        entry.Style.Cursor = "crosshair";
        Update(root);

        root.SetMousePosition(entry.PrefixLabel!.Box.Rect.Center);
        Update(root);
        Update(root);
        Assert.Equal("ew-resize", entry.Style.Cursor);

        root.SetMousePosition(new Vector2(entry.Box.Rect.Right - 5, entry.Box.Rect.Center.Y));
        Update(root);
        Update(root);
        Assert.Equal("crosshair", entry.Style.Cursor);
    }

    [Fact]
    public void MultilineEntriesTakeEnterAndMoveBetweenLines()
    {
        var (root, entry) = Focused();
        entry.Multiline = true;
        Type(entry, "one");
        Press(entry, "enter");
        Type(entry, "two");
        Update(root);
        Assert.Equal("one\ntwo", entry.Text);

        Press(entry, "up");
        Assert.Equal(3, entry.CaretPosition);
        Press(entry, "down");
        Assert.Equal(7, entry.CaretPosition);
        Press(entry, "home");
        Assert.Equal(4, entry.CaretPosition);
    }

    [Fact]
    public void EnterSubmitsAndEscapeCancelsSingleLineEntries()
    {
        var (root, entry) = Focused("done");
        var events = new List<string>();
        entry.AddEventListener("onsubmit", e => events.Add(e.Name));
        entry.AddEventListener("oncancel", e => events.Add(e.Name));

        Press(entry, "enter");
        Update(root);
        Assert.Equal(["onsubmit"], events);
        Assert.Null(root.Focused);

        entry.Focus();
        Update(root);
        Escape(entry);
        Update(root);
        Assert.Equal(["onsubmit", "oncancel"], events);
        Assert.Equal("done", entry.Text);
    }

    [Fact]
    public void ReadOnlyEntriesCanBeSelectedButNotChanged()
    {
        var (_, entry) = Focused("fixed");
        entry.ReadOnly = true;
        Type(entry, "x");
        Press(entry, "backspace");
        entry.OnPaste("y");
        Assert.Equal("fixed", entry.Text);

        entry.ContentLabel.SetSelection(0, 5);
        Assert.Equal("fixed", entry.GetClipboardValue(true));
        Assert.Equal("fixed", entry.Text);
    }

    [Fact]
    public void ValueDoesNotOverwriteTheUsersTypingAndClearsUndo()
    {
        var (root, entry) = Focused();
        Type(entry, "typed");
        entry.Value = "bound";
        Assert.Equal("typed", entry.Text);

        entry.Blur();
        Update(root);
        entry.Value = "bound";
        Assert.Equal("bound", entry.Text);
        Assert.False(entry.CanUndo);
    }

    [Fact]
    public void PlaceholderShowsWhileEmpty()
    {
        var root = Root(Styles);
        var entry = root.AddChild<TestEntry>();
        entry.Placeholder = "Search";
        Update(root);
        Update(root);
        Assert.True(entry.ContentLabel.HasClass("placeholder"));
        Assert.Equal("Search", entry.ContentLabel.TextBlock!.Text);
        Assert.True(entry.HasEmpty);

        entry.Text = "x";
        Update(root);
        Assert.False(entry.ContentLabel.HasClass("placeholder"));
        Assert.Equal("x", entry.ContentLabel.TextBlock.Text);
    }

    [Fact]
    public void DraggingSelectsFromWhereThePressStarted()
    {
        var (root, entry) = Focused("hello world");
        var label = entry.ContentLabel;
        Update(root);
        var start = label.GetCaretRect(2).Center;
        var end = label.GetCaretRect(8).Center;

        root.SetMousePosition(start);
        Update(root);
        root.SetMouseButton(MouseButtons.Left, true);
        Update(root);
        root.SetMousePosition(end);
        Update(root);
        root.SetMouseButton(MouseButtons.Left, false);
        Update(root);

        Assert.Equal("llo wo", entry.Selected);
        Assert.Equal(8, entry.CaretPosition);
    }

    [Fact]
    public void DroppedTextIsInsertedWhereItLands()
    {
        var (_, entry) = Focused("ab");
        var position = entry.ContentLabel.GetCaretRect(1).Center;
        var drop = new DropEvent(entry) { Text = "X", Position = position, IsDrop = true };
        entry.DispatchEventImmediate(drop);

        Assert.Equal("aXb", entry.Text);
        Assert.Equal(DropAction.Copy, drop.Action);
    }

    [Theory]
    [InlineData(true, "o world", "ahellb")]
    [InlineData(false, "hello world", "ahellb")]
    public void DraggingTheSelectionOutDropsItThroughThePlatform(bool move, string source, string target)
    {
        var root = Root(Styles);
        var from = root.AddChild<TestEntry>();
        var to = root.AddChild<TestEntry>();
        from.Text = "hello world";
        to.Text = "ab";
        Update(root);
        from.Focus();
        Update(root);
        Update(root);
        from.ContentLabel.SetSelection(0, 4);
        var grab = from.ContentLabel.GetCaretRect(2).Center;
        var landing = to.ContentLabel.GetCaretRect(1).Center;
        Drag.StartHandler = drag =>
        {
            root.DragEnter(drag.Files, drag.Text);
            root.DragOver(landing);
            var action = root.Drop(landing);
            return move ? action : DropAction.Copy;
        };

        try
        {
            DragSelection(root, grab, landing);
        }
        finally
        {
            Drag.StartHandler = null;
        }

        Assert.Equal(source, from.Text);
        Assert.Equal(target, to.Text);
    }

    [Fact]
    public void DraggingTheSelectionOutWithoutAPlatformChangesNothing()
    {
        var root = Root(Styles);
        var from = root.AddChild<TestEntry>();
        var to = root.AddChild<TestEntry>();
        from.Text = "hello world";
        to.Text = "ab";
        Update(root);
        from.Focus();
        Update(root);
        Update(root);
        from.ContentLabel.SetSelection(0, 4);

        DragSelection(root, from.ContentLabel.GetCaretRect(2).Center, to.ContentLabel.GetCaretRect(1).Center);

        Assert.Equal("hello world", from.Text);
        Assert.Equal("ab", to.Text);
    }

    [Fact]
    public void EmojiCodesAreReplacedWhenAllowed()
    {
        var (_, entry) = Focused();
        entry.AllowEmojiReplace = true;
        Type(entry, "hi :smile:");
        Assert.Equal("hi \U0001F604", entry.Text);
        Assert.Equal(4, entry.CaretPosition);

        entry.OnPaste(" :joy: :nothing:");
        Assert.Equal("hi \U0001F604 \U0001F602 :nothing:", entry.Text);
        Assert.Null(Emoji.FindEmoji(":nothing:"));
    }

    [Fact]
    public void ImeCompositionPreviewsWithoutCommitting()
    {
        var (root, entry) = Focused("a");
        entry.CaretPosition = 1;
        root.SetImeComposition("か");
        Update(root);
        Assert.Equal("aか", entry.Text);

        root.SetImeComposition(null);
        Update(root);
        Assert.Equal("a", entry.Text);
        Assert.False(entry.CanUndo);
    }

    [Fact]
    public void AnUnfinishedCompositionGoesAwayOnBlur()
    {
        var (root, entry) = Focused("a");
        entry.CaretPosition = 1;
        root.SetImeComposition("か");
        Update(root);

        entry.Blur();
        Update(root);
        Update(root);
        Assert.Equal("a", entry.Text);

        entry.Value = "b";
        entry.Focus();
        Update(root);
        entry.CaretPosition = 1;
        Type(entry, "c");
        Assert.Equal("bc", entry.Text);
    }

    [Fact]
    public void IconsAndClearButtons()
    {
        var (root, entry) = Focused("text");
        entry.Icon = "search";
        Assert.True(entry.HasClass("has-icon"));
        Assert.Equal("search", entry.IconPanel!.Text);
        Assert.True(entry.IconPanel.HasClass("iconpanel"));

        entry.HasClearButton = true;
        Assert.Equal("cancel", entry.Icon);
        entry.IconPanel.DispatchEventImmediate(new MousePanelEvent("onclick", entry.IconPanel, "mouseleft"));
        Assert.Equal("", entry.Text);

        entry.HasClearButton = false;
        Assert.Null(entry.IconPanel);
        Assert.False(entry.HasClass("has-icon"));
        Update(root);
    }

    [Fact]
    public void TheClearButtonCanBeUndoneAndRespectsReadOnly()
    {
        var (_, entry) = Focused("text");
        entry.HasClearButton = true;
        void ClickIcon() => entry.IconPanel!.DispatchEventImmediate(new MousePanelEvent("onclick", entry.IconPanel, "mouseleft"));

        ClickIcon();
        entry.Undo();
        Assert.Equal("text", entry.Text);

        entry.ReadOnly = true;
        ClickIcon();
        Assert.Equal("text", entry.Text);

        entry.ReadOnly = false;
        entry.Icon = "search";
        ClickIcon();
        Assert.Equal("text", entry.Text);
        Assert.False(entry.HasClearButton);
        Assert.False(entry.IconPanel!.HasClass("clearbutton"));
    }

    [Fact]
    public void IconPanelsUseTheIconFontFamily()
    {
        var root = Root(Styles);
        var icon = root.Add.Icon("home", "big");
        Update(root);

        Assert.Equal("Material Icons", icon.ComputedStyle!.FontFamily);
        Assert.Equal("home", icon.Text);
        Assert.True(icon.HasClass("big"));
    }

    private sealed class TestEntry : TextEntry
    {
        public Label ContentLabel => Label;

        public string Selected => Label.GetSelectedText();

        public bool HasEmpty => (PseudoClass & PseudoClass.Empty) != 0;
    }
}
