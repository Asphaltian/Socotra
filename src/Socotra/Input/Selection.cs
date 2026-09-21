namespace Socotra;

internal sealed class Selection
{
    private Panel? _selectionStart;
    private Vector2 _selectionStartPos;
    private Vector2 _selectionEndPos;

    public void Release(Panel subtree)
    {
        if (_selectionStart?.IsAncestor(subtree) == true)
        {
            _selectionStart = null;
        }
    }

    public void UpdateSelection(Panel? hovered, bool dragging, bool started, bool ended, Vector2 pos)
    {
        if (started)
        {
            _selectionStart = hovered;
            if (hovered is null)
            {
                return;
            }

            _selectionStartPos = hovered.ScreenPositionToPanelPosition(pos);
            _selectionEndPos = _selectionStartPos;
            return;
        }

        if (_selectionStart is null || !(dragging || ended))
        {
            return;
        }

        var endPos = _selectionStart.ScreenPositionToPanelPosition(pos);
        if (endPos == _selectionEndPos)
        {
            return;
        }

        _selectionEndPos = endPos;
        var start = _selectionStart.PanelPositionToScreenPosition(_selectionStartPos);
        var end = _selectionStart.PanelPositionToScreenPosition(_selectionEndPos);
        _selectionStart.CreateEvent(new SelectionEvent("ondragselect", _selectionStart)
        {
            StartPoint = start,
            EndPoint = end,
            SelectionRect = Rect.FromPoints(start, end),
        });
    }
}
