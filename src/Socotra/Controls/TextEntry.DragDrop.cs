using System.Globalization;

namespace Socotra;

public partial class TextEntry
{
    private SelectionDrag? _localSelectionDrag;
    private bool _suppressDragSelection;

    internal int DropCaretPosition { get; private set; } = -1;

    /// <inheritdoc/>
    protected override void OnDragLeave(PanelEvent e)
    {
        if (e is DropEvent)
        {
            DropCaretPosition = -1;
        }

        base.OnDragLeave(e);
    }

    /// <summary>Takes dragged text and inserts it where it's dropped. Text dragged from another text entry moves.</summary>
    protected override void OnDrop(PanelEvent e)
    {
        if (e is not DropEvent { Text: { Length: > 0 } text } drop || !CanEdit)
        {
            return;
        }

        var letter = Label.GetLetterAtScreenPosition(drop.Position);
        if (letter < 0)
        {
            return;
        }

        var selection = SelectionDrag.Current;
        if (selection?.Text != text)
        {
            selection = null;
        }

        drop.Action = selection?.CanMove == true ? DropAction.Move : DropAction.Copy;
        drop.StopPropagation();
        DropCaretPosition = drop.IsDrop ? -1 : letter;
        if (!drop.IsDrop)
        {
            return;
        }

        Focus();
        if (drop.Action == DropAction.Move && selection!.Source == this)
        {
            selection.MoveWithin(letter);
            return;
        }

        var before = Text;
        var elements = StringInfo.ParseCombiningCharacters(before);
        var offset = letter < elements.Length ? elements[letter] : before.Length;
        Label.SetCaretPosition(letter);
        OnPaste(text);
        if (Text != before.Insert(offset, text))
        {
            drop.Action = DropAction.Copy;
        }
    }

    private void UpdateTextDrag()
    {
        if (!Box.Rect.IsInside(ScreenMousePosition) || !CanEdit)
        {
            CancelTextDrag();
            DragSelectionOut();
            return;
        }

        _localSelectionDrag ??= new SelectionDrag(this, publish: false);
        _suppressDragSelection = true;
        DropCaretPosition = Label.GetLetterAtScreenPosition(ScreenMousePosition);
    }

    private void CancelTextDrag()
    {
        _localSelectionDrag?.Dispose();
        _localSelectionDrag = null;
        DropCaretPosition = -1;
    }

    private bool FinishTextDrag(bool copy)
    {
        if (_localSelectionDrag is not { } drag)
        {
            return false;
        }

        try
        {
            var position = Label.GetLetterAtScreenPosition(ScreenMousePosition);
            if (Box.Rect.IsInside(ScreenMousePosition) && position >= 0)
            {
                if (copy)
                {
                    drag.CopyWithin(position);
                }
                else
                {
                    drag.MoveWithin(position);
                }
            }
        }
        finally
        {
            _pressedOnSelection = false;
            CancelTextDrag();
        }

        return true;
    }

    private void DragSelectionOut()
    {
        _suppressDragSelection = true;
        _pressedOnSelection = false;
        if (string.IsNullOrEmpty(Label.GetSelectedText()))
        {
            return;
        }

        using var selection = new SelectionDrag(this);
        var drag = new Drag(this);
        drag.SetText(selection.Text);
        selection.Complete(drag.Start());
    }

    private sealed class SelectionDrag : IDisposable
    {
        [ThreadStatic]
        private static SelectionDrag? _current;

        private readonly SelectionDrag? _previous;
        private readonly bool _published;
        private readonly string _original;
        private readonly int _start;
        private readonly int _length;
        private bool _handled;

        public SelectionDrag(TextEntry source, bool publish = true)
        {
            Source = source;
            _original = source.Text;
            _start = Math.Min(source.Label.SelectionStart, source.Label.SelectionEnd);
            _length = Math.Abs(source.Label.SelectionEnd - source.Label.SelectionStart);
            Text = source.Label.GetSelectedText();
            _published = publish;
            _previous = _current;
            if (publish)
            {
                _current = this;
            }
        }

        public static SelectionDrag? Current => _current;

        public TextEntry Source { get; }

        public string Text { get; }

        public bool CanMove => !_handled && !Source.IsDeleted && Source.CanEdit && Source.Text == _original;

        public void CopyWithin(int destination)
        {
            if (!CanMove)
            {
                return;
            }

            _handled = true;
            Source.PasteText(Text, destination, destination, select: true);
        }

        public void MoveWithin(int destination)
        {
            if (!CanMove)
            {
                return;
            }

            _handled = true;
            if (destination >= _start && destination <= _start + _length)
            {
                return;
            }

            Source.RecordEdit(EditKind.Single);
            Source.Label.RemoveText(_start, _length);
            if (destination > _start)
            {
                destination -= _length;
            }

            Source.Label.SetCaretPosition(destination);
            Source.Label.InsertText(Text, destination);
            Source.Label.SetCaretPosition(destination + _length);
            Source.Label.SetSelection(destination, destination + _length);
            Source.OnValueChanged();
        }

        public void Complete(DropAction action)
        {
            if (action != DropAction.Move || !CanMove)
            {
                return;
            }

            _handled = true;
            Source.RecordEdit(EditKind.Single);
            Source.Label.RemoveText(_start, _length);
            Source.Label.SetCaretPosition(_start);
            Source.OnValueChanged();
        }

        public void Dispose()
        {
            if (_published)
            {
                _current = _previous;
            }
        }
    }
}
