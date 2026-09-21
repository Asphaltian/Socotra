using static Socotra.Tests.ControlTesting;

namespace Socotra.Tests;

public class AutoCompleteTests
{
    private sealed class Entry : TextEntry
    {
        public Label Content => Label;
    }

    private static (RootPanel Root, Entry Entry) Create(string text = "h")
    {
        var root = Root();
        var entry = root.AddChild<Entry>();
        entry.Style.Width = 300;
        entry.Text = text;
        Update(root);
        entry.CaretPosition = entry.TextLength;
        return (root, entry);
    }

    private static void Focus(RootPanel root, TextEntry entry)
    {
        entry.Focus();
        Update(root);
        Update(root);
        Assert.Same(entry, root.Focused);
    }

    private static void OpenCompletion(RootPanel root, Entry entry)
    {
        entry.AutoComplete = _ => ["hello"];
        Focus(root, entry);
        Assert.NotNull(entry.AutoCompletePanel);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AcceptingASuggestionNotifiesOnceAndCanBeUndone(bool mouse)
    {
        var (root, entry) = Create("hi");
        entry.Content.SetSelection(0, 1);
        entry.CaretPosition = 1;
        var notified = new List<string>();
        entry.OnTextEdited = notified.Add;
        OpenCompletion(root, entry);

        if (mouse)
        {
            var option = entry.AutoCompletePanel!.GetChild(0)!;
            option.DispatchEventImmediate(new MousePanelEvent("onclick", option, "mouseleft"));
            Update(root);
        }
        else
        {
            Key(root, "down");
            Assert.Equal("hello", entry.Text);
            Assert.Empty(notified);
            Assert.False(entry.CanUndo);
            Key(root, "enter");
        }

        Assert.Equal("hello", entry.Text);
        Assert.Equal(["hello"], notified);
        Assert.Null(entry.AutoCompletePanel);

        entry.Undo();
        Assert.Equal("hi", entry.Text);
        Assert.Equal(1, entry.CaretPosition);
        Assert.Equal("h", entry.Content.GetSelectedText());
        Assert.False(entry.CanUndo);
        entry.Redo();
        Assert.Equal("hello", entry.Text);
    }

    [Fact]
    public void EscapePutsBackTheTextCaretAndSelection()
    {
        var (root, entry) = Create("hi");
        entry.Content.SetSelection(0, 1);
        entry.CaretPosition = 1;
        var notifications = 0;
        entry.OnTextEdited = _ => notifications++;
        OpenCompletion(root, entry);

        Key(root, "down");
        Assert.Equal("hello", entry.Text);
        Key(root, "escape");

        Assert.Equal("hi", entry.Text);
        Assert.Equal(1, entry.CaretPosition);
        Assert.Equal("h", entry.Content.GetSelectedText());
        Assert.Equal(0, notifications);
        Assert.False(entry.CanUndo);
        Assert.Same(entry, root.Focused);
    }

    [Fact]
    public void SuggestionsFollowTheTypingAndShowAboveTheEntry()
    {
        var (root, entry) = Create("");
        entry.Style.MarginTop = 300;
        string[] words = ["apple", "apricot", "banana"];
        entry.AutoComplete = text => [.. words.Where(word => word.StartsWith(text, StringComparison.Ordinal))];
        Focus(root, entry);
        Assert.Equal(3, entry.AutoCompletePanel!.ChildrenCount);

        root.TypeText("ap");
        Update(root);
        Update(root);
        var popup = entry.AutoCompletePanel!;
        Assert.Equal(["apple", "apricot"], popup.Children.OfType<Button>().Select(button => button.Text));
        Assert.True(popup.HasClass("autocomplete"));
        Assert.True(popup.Box.Rect.Bottom <= entry.Box.Rect.Top);

        Key(root, "tab");
        Assert.Equal("apple", entry.Text);
        Key(root, "tab");
        Assert.Equal("apricot", entry.Text);
        Key(root, "tab", KeyboardModifiers.Shift);
        Assert.Equal("apple", entry.Text);

        root.TypeText("z");
        Update(root);
        Assert.Null(entry.AutoCompletePanel);
    }

    [Fact]
    public void EntriesShowTheirTitleAndPutInTheirValue()
    {
        var (root, entry) = Create("");
        entry.AutoComplete = _ => [new TextEntry.AutocompleteEntry { Title = "Home (h)", Icon = "home", Value = "home" }];
        Focus(root, entry);

        var option = (Button)entry.AutoCompletePanel!.GetChild(0)!;
        Assert.Equal("Home (h)", option.Text);
        Assert.Equal("home", option.Icon);

        Key(root, "down");
        Key(root, "enter");
        Assert.Equal("home", entry.Text);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EntriesThatCannotBeEditedShowNoSuggestions(bool disabled)
    {
        var (_, entry) = Create();
        entry.ReadOnly = !disabled;
        entry.Disabled = disabled;
        entry.AutoComplete = _ => ["hello"];

        entry.UpdateAutoComplete();
        Assert.Null(entry.AutoCompletePanel);
        entry.UpdateAutoComplete(["hello"]);
        Assert.Null(entry.AutoCompletePanel);
        Assert.Equal("h", entry.Text);
    }

    [Fact]
    public void AReadOnlyEntryCannotAcceptAnOpenSuggestion()
    {
        var (root, entry) = Create();
        OpenCompletion(root, entry);
        Key(root, "down");

        entry.ReadOnly = true;
        Key(root, "enter");

        Assert.Equal("h", entry.Text);
        Assert.False(entry.CanUndo);
        Assert.Null(entry.AutoCompletePanel);
    }

    [Fact]
    public void EnterInAMultilineEntryAcceptsWithoutANewLine()
    {
        var (root, entry) = Create();
        entry.Multiline = true;
        OpenCompletion(root, entry);

        Key(root, "down");
        Key(root, "enter");

        Assert.Equal("hello", entry.Text);
        Assert.True(entry.CanUndo);
    }
}
