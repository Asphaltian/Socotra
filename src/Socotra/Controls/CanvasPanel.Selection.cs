namespace Socotra;

public partial class CanvasPanel
{
    private Vector2 _boxStart;
    private Panel? _selectionBox;

    /// <summary>Called when the user starts dragging a selection box, with true if Shift was held to add to the selection.</summary>
    public event Action<bool>? BoxSelectionStarted;

    /// <summary>Called with the selection box, in canvas coordinates, as the user drags it. Select whatever it covers.</summary>
    public event Action<Rect>? BoxSelectionChanged;

    /// <summary>Called when the selection box is let go, with true if it was canceled by Escape, losing focus or <see cref="BoxSelectionEnabled"/> being turned off.</summary>
    public event Action<bool>? BoxSelectionFinished;

    /// <summary>Lets the user drag a selection box over empty canvas with the left button. Handle <see cref="BoxSelectionChanged"/> to select things. Off by default.</summary>
    public bool BoxSelectionEnabled { get; set; }

    /// <summary>The selection box in canvas coordinates while the user drags one, otherwise null.</summary>
    public Rect? SelectionBox { get; private set; }

    /// <summary>Whether the user is dragging a selection box right now.</summary>
    public bool IsBoxSelecting => SelectionBox.HasValue;

    private Panel SelectionBoxPanel => _selectionBox ??= AddChild<Panel>("canvas-selection-box");

    /// <summary>Cancels a selection box on Escape.</summary>
    protected override void OnEscape(PanelEvent e)
    {
        if (!IsBoxSelecting)
        {
            base.OnEscape(e);
            return;
        }

        FinishBoxSelection(true);
        e.StopPropagation();
    }

    /// <summary>Starts a selection box at the mouse. Call it from a subclass that checks for its own things under the mouse first. Pass <paramref name="additive"/> when Shift is held.</summary>
    protected void BeginBoxSelection(bool additive)
    {
        if (!BoxSelectionEnabled || !CanNavigate || IsPanning || IsBoxSelecting)
        {
            return;
        }

        Focus();
        _boxStart = ScreenToCanvas(MousePosition);
        SelectionBox = new Rect(_boxStart, Vector2.Zero);
        SelectionBoxPanel.Style.Display = DisplayMode.Flex;
        BoxSelectionStarted?.Invoke(additive);
        UpdateBoxSelection();
    }

    /// <summary>Ends the selection box, or cancels it when <paramref name="canceled"/> is true.</summary>
    protected void FinishBoxSelection(bool canceled = false)
    {
        if (!IsBoxSelecting)
        {
            return;
        }

        SelectionBox = null;
        SelectionBoxPanel.Style.Display = DisplayMode.None;
        BoxSelectionFinished?.Invoke(canceled);
    }

    private void UpdateBoxSelection()
    {
        var mouse = Vector2.Clamp(MousePosition, Viewport.TopLeft, Viewport.BottomRight);
        var point = ScreenToCanvas(mouse);
        var rect = new Rect(Vector2.Min(_boxStart, point), Vector2.Max(_boxStart, point) - Vector2.Min(_boxStart, point));
        SelectionBox = rect;

        var a = CanvasToScreen(rect.Position);
        var b = CanvasToScreen(rect.Position + rect.Size);
        var min = Vector2.Min(a, b) / ScaleToScreen;
        var size = (Vector2.Max(a, b) - Vector2.Min(a, b)) / ScaleToScreen;
        var box = SelectionBoxPanel;
        box.Style.Left = min.X;
        box.Style.Top = min.Y;
        box.Style.Width = size.X;
        box.Style.Height = size.Y;
        BoxSelectionChanged?.Invoke(rect);
    }
}
