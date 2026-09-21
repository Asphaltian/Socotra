using static Socotra.Tests.ControlTesting;

namespace Socotra.Tests;

public class TextEntryHistoryTests
{
    [Fact]
    public void AddingMovesRepeatsToTheEndAndDropsTheOldest()
    {
        var entry = new TextEntry { HistoryMaxItems = 3 };

        entry.AddToHistory("one");
        entry.AddToHistory("two");
        entry.AddToHistory("one");
        Assert.Equal(["two", "one"], entry.History);

        entry.AddToHistory("three");
        entry.AddToHistory("four");
        Assert.Equal(["one", "three", "four"], entry.History);

        entry.ClearHistory();
        Assert.Empty(entry.History);
    }

    [Fact]
    public void ASavedHistoryCanBePutBack()
    {
        var saved = new TextEntry();
        saved.AddToHistory("help");
        saved.AddToHistory("quit");

        var restored = new TextEntry { HistoryMaxItems = 1 };
        restored.History = saved.History;
        Assert.Equal(["quit"], restored.History);

        restored.HistoryMaxItems = 0;
        restored.History = ["a", "b", "c"];
        Assert.Equal(["a", "b", "c"], restored.History);
    }

    [Fact]
    public void UpAndDownInAnEmptyEntryWalkTheHistoryNewestFirst()
    {
        var root = Root();
        var entry = root.AddChild<TextEntry>();
        entry.Style.MarginTop = 300;
        entry.Style.Width = 300;
        entry.AddToHistory("first");
        entry.AddToHistory("second");
        entry.AddToHistory("third");
        entry.Focus();
        Update(root);
        Update(root);

        Key(root, "up");
        Assert.Equal("third", entry.Text);
        Assert.Equal(["first", "second", "third"], entry.AutoCompletePanel!.Children.OfType<Button>().Select(button => button.Text));
        Key(root, "up");
        Assert.Equal("second", entry.Text);
        Key(root, "down");
        Assert.Equal("third", entry.Text);
        Key(root, "enter");

        Assert.Equal("third", entry.Text);
        Assert.Null(entry.AutoCompletePanel);
        Assert.Same(entry, root.Focused);
    }
}
