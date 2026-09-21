namespace Socotra;

public partial class TextEntry
{
    private const float EditRunTimeout = 1;
    private const int MaxUndoSteps = 200;

    private readonly List<TextState> _undoStack = [];
    private readonly List<TextState> _redoStack = [];
    private EditKind _runKind = EditKind.Single;
    private double _lastEditTime;
    private bool _restoringState;

    private enum EditKind
    {
        Single,
        Typing,
        Deleting,
    }

    /// <summary>Whether there's an edit <see cref="Undo"/> can take back.</summary>
    public bool CanUndo => _undoStack.Count > 0;

    /// <summary>Whether there's an undone edit <see cref="Redo"/> can put back.</summary>
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>Takes back the last edit, putting back the text, caret and selection from before it, like Ctrl+Z. Letters typed in one go come back out together.</summary>
    public void Undo()
    {
        if (!CanEdit || _undoStack.Count == 0)
        {
            return;
        }

        _redoStack.Add(CurrentState());
        Restore(_undoStack[^1]);
        _undoStack.RemoveAt(_undoStack.Count - 1);
        BreakEditRun();
    }

    /// <summary>Puts back the last edit <see cref="Undo"/> took away, like Ctrl+Y.</summary>
    public void Redo()
    {
        if (!CanEdit || _redoStack.Count == 0)
        {
            return;
        }

        _undoStack.Add(CurrentState());
        Restore(_redoStack[^1]);
        _redoStack.RemoveAt(_redoStack.Count - 1);
        BreakEditRun();
    }

    /// <summary>Forgets every edit, so there's nothing to undo or redo. Call it after replacing the text with something the user didn't type.</summary>
    public void ClearUndoHistory()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        BreakEditRun();
    }

    private TextState CurrentState() => new(Text, CaretPosition, Label.SelectionStart, Label.SelectionEnd);

    private void RecordEdit(EditKind kind)
    {
        if (_restoringState)
        {
            return;
        }

        _redoStack.Clear();
        if (_undoStack.Count > 0 && kind != EditKind.Single && kind == _runKind && TimeNow - _lastEditTime < EditRunTimeout)
        {
            _lastEditTime = TimeNow;
            return;
        }

        _undoStack.Add(CurrentState());
        if (_undoStack.Count > MaxUndoSteps)
        {
            _undoStack.RemoveAt(0);
        }

        _runKind = kind;
        _lastEditTime = TimeNow;
    }

    private void BreakEditRun() => _runKind = EditKind.Single;

    private void ApplyState(TextState state)
    {
        Label.Text = state.Text;
        Label.SetSelection(state.SelectionStart, state.SelectionEnd);
        CaretPosition = state.Caret;
    }

    private void Restore(TextState state)
    {
        _restoringState = true;
        try
        {
            ApplyState(state);
        }
        finally
        {
            _restoringState = false;
        }

        OnValueChanged();
    }

    private readonly record struct TextState(string Text, int Caret, int SelectionStart, int SelectionEnd);
}
