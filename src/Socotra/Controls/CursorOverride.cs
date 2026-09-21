namespace Socotra;

internal struct CursorOverride
{
    private bool _active;
    private string? _previous;

    public void Set(Panel panel, string? cursor)
    {
        if (cursor is not null)
        {
            if (!_active)
            {
                _previous = panel.Style.Cursor;
                _active = true;
            }

            panel.Style.Cursor = cursor;
        }
        else if (_active)
        {
            panel.Style.Cursor = _previous;
            _active = false;
        }
    }
}
