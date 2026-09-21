namespace Socotra;

internal sealed class VerticalListLayout
{
    private float _itemHeight = 100;
    private Rect _rect;
    private Rect _outerRect;
    private float _scrollOffset;
    private Vector2 _cellSize;
    private int _updateHash;

    public float ItemHeight
    {
        get => _itemHeight;
        set => _itemHeight = MathF.Max(1, value);
    }

    public Vector2 Spacing { get; set; }

    public bool Update(Box box, float scaleFromScreen, float scrollOffset)
    {
        var hash = HashCode.Combine(box.RectInner, box.Rect, scaleFromScreen, scrollOffset, _itemHeight, Spacing);
        if (hash == _updateHash)
        {
            return false;
        }

        _updateHash = hash;
        _rect = new Rect(box.RectInner.Position - box.Rect.Position, box.RectInner.Size) * scaleFromScreen;
        _outerRect = box.Rect * scaleFromScreen;
        _scrollOffset = scrollOffset;
        _cellSize = new Vector2(MathF.Max(1, _rect.Width), _itemHeight);
        return true;
    }

    public void GetVisibleRange(out int firstIndex, out int lastIndex)
    {
        float step = MathF.Max(1, _cellSize.Y + Spacing.Y);
        firstIndex = Math.Max(0, (int)MathF.Floor((_scrollOffset - _rect.Top) / step));
        lastIndex = firstIndex + (int)MathF.Ceiling(_outerRect.Height / step) + 1;
    }

    public Rect GetPosition(int index) => new(_rect.Left, _rect.Top + (index * (_cellSize.Y + Spacing.Y)), _cellSize.X, _cellSize.Y);

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
        float step = _cellSize.Y + Spacing.Y;
        float paddingY = MathF.Max(0, _outerRect.Height - _rect.Height);
        return step <= 0 ? paddingY : (count * step) + paddingY;
    }
}
