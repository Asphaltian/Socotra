using System.Globalization;

namespace Socotra;

public partial class NumberEntry
{
    private const float DefaultStep = 0.01f;
    private const float FineScale = 0.1f;

    private bool _scrubbing;
    private float _scrubValue;
    private Vector2 _scrubLast;
    private CursorOverride _scrubCursor;

    internal event Action? ScrubEnded;

    private float ScrubStep => MinValue.HasValue && MaxValue.HasValue ? (MaxValue.Value - MinValue.Value) / 1000 : DefaultStep;

    /// <inheritdoc/>
    protected override void OnMouseDown(MousePanelEvent e)
    {
        if (CanEdit && e.Button == "mouseleft" && IsOnScrubHandle(ScreenMousePosition))
        {
            _scrubbing = true;
            _scrubLast = ScreenMousePosition;
            _scrubValue = Translation.TryParseTypedNumber(Text, out var value) ? value : 0;
            Blur();
            e.StopPropagation();
            return;
        }

        base.OnMouseDown(e);
    }

    /// <inheritdoc/>
    protected override void OnMouseMove(MousePanelEvent e)
    {
        _scrubCursor.Set(this, IsOnScrubHandle(ScreenMousePosition) ? "ew-resize" : null);
        if (!_scrubbing)
        {
            base.OnMouseMove(e);
            return;
        }

        var position = ScreenMousePosition;
        var moved = (position.X - _scrubLast.X) / ScaleToScreen;
        _scrubLast = position;
        var step = e.MouseButton == MouseButtons.Right ? ScrubStep * FineScale : ScrubStep;
        _scrubValue = Math.Clamp(_scrubValue + (moved * step), MinValue ?? float.MinValue, MaxValue ?? float.MaxValue);
        SetValue(_scrubValue);
        e.StopPropagation();
    }

    /// <inheritdoc/>
    protected override void OnMouseUp(MousePanelEvent e)
    {
        if (_scrubbing)
        {
            _scrubbing = false;
            ScrubEnded?.Invoke();
            e.StopPropagation();
            return;
        }

        base.OnMouseUp(e);
    }

    /// <inheritdoc/>
    protected override void OnDragSelect(SelectionEvent e)
    {
        if (!_scrubbing)
        {
            base.OnDragSelect(e);
        }
    }

    private bool IsOnScrubHandle(Vector2 screenPosition) => PrefixLabel is not null && PrefixLabel.Box.Rect.IsInside(screenPosition);

    private void SetValue(float value)
    {
        if (WholeNumbers)
        {
            value = MathF.Round(value);
        }

        Text = value.ToString(WholeNumbers ? "0" : NumberFormat, CultureInfo.InvariantCulture);
        OnValueChanged();
    }
}
