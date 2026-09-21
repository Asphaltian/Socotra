namespace Socotra;

internal sealed class GridLayout
{
    private float _itemWidth = 100;
    private float _itemHeight = 100;
    private Rect _rect;
    private Rect _outerRect;
    private float _scrollOffset;
    private Vector2 _cellSize;
    private int _columns;
    private int _updateHash;

    public float ItemWidth
    {
        get => _itemWidth;
        set => _itemWidth = MathF.Max(1, value);
    }

    public float ItemHeight
    {
        get => _itemHeight;
        set => _itemHeight = MathF.Max(1, value);
    }

    public Vector2 Spacing { get; set; }

    public bool ScaleUp { get; set; } = true;

    public bool Update(Box box, float scaleFromScreen, float scrollOffset)
    {
        var hash = HashCode.Combine(box.RectInner, box.Rect, scaleFromScreen, scrollOffset, _itemWidth, _itemHeight, Spacing);
        if (hash == _updateHash)
        {
            return false;
        }

        _updateHash = hash;
        _cellSize = new Vector2(_itemWidth, _itemHeight);
        _rect = new Rect(box.RectInner.Position - box.Rect.Position, box.RectInner.Size) * scaleFromScreen;
        _outerRect = box.Rect * scaleFromScreen;
        _scrollOffset = scrollOffset;

        float stepX = _cellSize.X + Spacing.X;
        _columns = Math.Max(1, stepX > 0 ? (int)MathF.Floor((_rect.Width + Spacing.X) / stepX) : 1);
        if (ScaleUp)
        {
            _cellSize.X = (_rect.Width - ((_columns - 1) * Spacing.X)) / _columns;
            _cellSize.Y = MathF.Max(1, _cellSize.X * (_itemHeight / _itemWidth));
        }

        return true;
    }

    public void GetVisibleRange(out int firstIndex, out int lastIndex)
    {
        float rowStep = MathF.Max(1, _cellSize.Y + Spacing.Y);
        int topRow = Math.Max(0, (int)MathF.Floor((_scrollOffset - _rect.Top) / rowStep));
        int rowsFit = (int)MathF.Ceiling(_outerRect.Height / rowStep) + 1;
        firstIndex = topRow * _columns;
        lastIndex = firstIndex + (rowsFit * _columns);
    }

    public Rect GetPosition(int index)
    {
        int column = index % _columns;
        int row = index / _columns;
        return new Rect(_rect.Left + (column * (_cellSize.X + Spacing.X)), _rect.Top + (row * (_cellSize.Y + Spacing.Y)), _cellSize.X, _cellSize.Y);
    }

    public void Position(int index, Panel panel)
    {
        var r = GetPosition(index);
        panel.Style.Left = r.Left;
        panel.Style.Top = r.Top;
        panel.Style.Width = r.Width;
        panel.Style.Height = r.Height;
        panel.Style.Dirty();
    }

    public float GetHeight(int count)
    {
        float rowStep = _cellSize.Y + Spacing.Y;
        float paddingY = MathF.Max(0, _outerRect.Height - _rect.Height);
        return rowStep <= 0 ? paddingY : (MathF.Ceiling(count / (float)_columns) * rowStep) + paddingY;
    }
}
