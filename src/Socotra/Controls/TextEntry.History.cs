namespace Socotra;

public partial class TextEntry
{
    private List<string> _history = [];

    /// <summary>The most entries <see cref="AddToHistory"/> keeps. The oldest go first. 0 or less keeps them all.</summary>
    public int HistoryMaxItems { get; set; } = 30;

    /// <summary>
    /// What <see cref="AddToHistory"/> has kept, oldest first. Save it when the app closes and set it again when it
    /// starts to keep the history between runs.
    /// </summary>
    /// <example>
    /// <code>
    /// // Keep a console's history between runs
    /// console.History = File.Exists(path) ? File.ReadAllLines(path) : [];
    /// // ...and when closing
    /// File.WriteAllLines(path, console.History);
    /// </code>
    /// </example>
    public IReadOnlyList<string> History
    {
        get => _history.AsReadOnly();
        set
        {
            _history = [.. value];
            TrimHistory();
        }
    }

    /// <summary>
    /// Adds <paramref name="str"/> to the entry's history, like a command that was just run. With the entry empty, Up and Down
    /// show the history in the suggestion list, newest last.
    /// </summary>
    public void AddToHistory(string str)
    {
        _history.RemoveAll(entry => entry == str);
        _history.Add(str);
        TrimHistory();
    }

    /// <summary>Forgets everything <see cref="AddToHistory"/> added.</summary>
    public void ClearHistory() => _history.Clear();

    private void TrimHistory()
    {
        if (HistoryMaxItems > 0 && _history.Count > HistoryMaxItems)
        {
            _history.RemoveRange(0, _history.Count - HistoryMaxItems);
        }
    }
}
