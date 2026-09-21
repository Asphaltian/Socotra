using static Socotra.Tests.ControlTesting;

namespace Socotra.Tests;

public class CanvasPanelTests
{
    private readonly TestCanvas _canvas = new();

    private static void Near(Vector2 expected, Vector2 actual) => Assert.True((expected - actual).Length() < 0.001f, $"Expected {expected}, got {actual}");

    private static RootPanel CreateRoot()
    {
        var root = new UnscaledRoot();
        root.StyleSheet.Parse("rootpanel { pointer-events: all; }");
        return root;
    }

    private static void Update(RootPanel root)
    {
        root.Update(new Rect(0, 0, 1000, 800), 0.016f);
        root.Update(new Rect(0, 0, 1000, 800), 0.016f);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void NavigationBoundsHoldThroughPanZoomAndResize(bool uniform, bool up)
    {
        _canvas.PreserveAspectRatio = uniform;
        _canvas.YAxisUp = up;
        _canvas.NavigationBounds = new Rect(0, 0, 1000, 600);

        _canvas.SetView(new Vector2(-500, -500), new Vector2(1500, 1500));
        Check();
        _canvas.ZoomAt(new Vector2(340, 170), 0.25f);
        Check();
        _canvas.Pan(new Vector2(10000, 10000));
        Check();
        _canvas.Pan(new Vector2(-10000, -10000));
        Check();
        _canvas.ZoomAt(new Vector2(140, 70), 100);
        Check();
        _canvas.TestViewport = new Rect(40, 20, 1200, 200);
        _canvas.Tick();
        Check();
        _canvas.TestViewport = new Rect(40, 20, 200, 600);
        _canvas.Tick();
        Check();

        if (uniform)
        {
            var origin = _canvas.CanvasToScreen(Vector2.Zero);
            Assert.Equal((_canvas.CanvasToScreen(new Vector2(1, 0)) - origin).Length(), (_canvas.CanvasToScreen(new Vector2(0, 1)) - origin).Length(), 0.001f);
        }

        _canvas.NavigationBounds = null;
        _canvas.Pan(new Vector2(10000, 10000));
        Assert.True(_canvas.ViewMin.X < 0);
        Assert.Throws<ArgumentException>(() => _canvas.NavigationBounds = new Rect(0, 0, 0, 1));

        void Check()
        {
            var a = _canvas.ScreenToCanvas(_canvas.TestViewport.Position);
            var b = _canvas.ScreenToCanvas(_canvas.TestViewport.Position + _canvas.TestViewport.Size);
            var min = Vector2.Min(a, b);
            var max = Vector2.Max(a, b);
            Assert.True(min.X >= -0.001f && min.Y >= -0.001f && max.X <= 1000.001f && max.Y <= 600.001f, $"Visible {min} to {max}");
        }
    }

    [Fact]
    public void CoordinatesRoundTripEitherWayUp()
    {
        _canvas.PreserveAspectRatio = false;
        _canvas.SetView(new Vector2(-2, -5), new Vector2(8, 15));
        foreach (bool up in (bool[])[false, true])
        {
            _canvas.YAxisUp = up;
            var point = new Vector2(3, 2);
            Near(point, _canvas.ScreenToCanvas(_canvas.CanvasToScreen(point)));
            Near(new Vector2(40, up ? 320 : 20), _canvas.CanvasToScreen(_canvas.ViewMin));
        }
    }

    [Fact]
    public void ZoomKeepsThePointUnderTheCursorAndPanFollowsTheMouse()
    {
        _canvas.YAxisUp = true;
        var cursor = new Vector2(173, 219);
        var point = _canvas.ScreenToCanvas(cursor);

        _canvas.ZoomAt(cursor, 0.5f);
        Near(cursor, _canvas.CanvasToScreen(point));

        _canvas.Pan(new Vector2(30, -17));
        Near(cursor + new Vector2(30, -17), _canvas.CanvasToScreen(point));
    }

    [Fact]
    public void ResizingKeepsAnEvenScale()
    {
        _canvas.SetView(new Vector2(-100, -50), new Vector2(900, 450));
        foreach (var size in (Vector2[])[new(1200, 300), new(300, 600)])
        {
            _canvas.TestViewport = new Rect(new Vector2(40, 20), size);
            var origin = _canvas.CanvasToScreen(Vector2.Zero);
            var x = _canvas.CanvasToScreen(new Vector2(100, 0)) - origin;
            var y = _canvas.CanvasToScreen(new Vector2(0, 100)) - origin;
            Assert.Equal(x.Length(), y.Length(), 0.001f);
            Near(new Vector2(40, 20) + (size * 0.5f), _canvas.CanvasToScreen((_canvas.ViewMin + _canvas.ViewMax) * 0.5f));
        }
    }

    [Fact]
    public void BadZoomsLeaveTheViewAlone()
    {
        _canvas.MaximumViewSpan = 1000;
        foreach (float factor in (float[])[0, -1, float.NaN, float.PositiveInfinity, 10000])
        {
            _canvas.ZoomAt(new Vector2(100, 100), factor);
        }

        Near(Vector2.Zero, _canvas.ViewMin);
        Near(Vector2.One, _canvas.ViewMax);
        Assert.Throws<ArgumentException>(() => _canvas.SetView(Vector2.One, Vector2.Zero));
    }

    [Fact]
    public void FitBoundsPadsFlatContentAndKeepsToTheLimits()
    {
        _canvas.PreserveAspectRatio = false;
        _canvas.FitBounds(new Rect(2, 3, 10, 0), new Vector2(0.1f, 0.2f), new Vector2(0.5f, 1));
        Near(new Vector2(1, 2), _canvas.ViewMin);
        Near(new Vector2(13, 4), _canvas.ViewMax);

        _canvas.MinimumViewSpan = 0.1f;
        _canvas.FitBounds(new Rect(5, 5, 0, 0));
        Near(new Vector2(4.95f, 4.95f), _canvas.ViewMin);
        Near(new Vector2(5.05f, 5.05f), _canvas.ViewMax);

        _canvas.NavigationBounds = new Rect(0, 0, 10, 10);
        _canvas.FitBounds(new Rect(-20, -20, 50, 50));
        Near(Vector2.Zero, _canvas.ViewMin);
        Near(new Vector2(10, 10), _canvas.ViewMax);
        Assert.Throws<ArgumentException>(() => _canvas.FitBounds(new Rect(0, 0, float.NaN, 1)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ZoomComesBackAcrossASingleAxisLimit(bool horizontal)
    {
        _canvas.PreserveAspectRatio = false;
        _canvas.ConstrainHorizontalNavigation = horizontal;
        _canvas.ConstrainVerticalNavigation = !horizontal;
        _canvas.NavigationBounds = new Rect(0, 0, 1, 1);
        _canvas.SetView(new Vector2(0.1f, 0.2f), new Vector2(0.9f, 0.8f));
        var min = _canvas.ViewMin;
        var max = _canvas.ViewMax;
        var anchor = _canvas.CanvasToScreen(new Vector2(0.5f));

        _canvas.ZoomAt(anchor, 4);
        _canvas.ZoomAt(anchor, 0.5f);
        Assert.Equal(1, horizontal ? _canvas.ViewMax.X - _canvas.ViewMin.X : _canvas.ViewMax.Y - _canvas.ViewMin.Y, 0.0001f);
        _canvas.ZoomAt(anchor, 0.5f);
        Near(min, _canvas.ViewMin);
        Near(max, _canvas.ViewMax);
    }

    [Fact]
    public void WheelZoomsAndMiddleDragPans()
    {
        var root = CreateRoot();
        var canvas = root.AddChild(new CanvasPanel());
        canvas.Style.Set("width: 600px; height: 300px;");
        canvas.SetView(Vector2.Zero, new Vector2(600, 300));
        Update(root);

        root.SetMousePosition(new Vector2(300, 150));
        Update(root);
        var under = canvas.ScreenToCanvas(canvas.MousePosition);
        root.AddMouseWheel(new Vector2(0, 1));
        Update(root);
        Near(new Vector2(690, 345), canvas.ViewMax - canvas.ViewMin);
        Near(under, canvas.ScreenToCanvas(canvas.MousePosition));

        var before = canvas.ViewMin;
        root.SetMouseButton(MouseButtons.Middle, true);
        Update(root);
        Assert.True(canvas.IsPanning);
        root.SetMousePosition(new Vector2(330, 150));
        Update(root);
        root.SetMouseButton(MouseButtons.Middle, false);
        Update(root);

        Assert.False(canvas.IsPanning);
        Assert.True(canvas.ViewMin.X < before.X);
        Near(under, canvas.ScreenToCanvas(canvas.MousePosition));
    }

    [Fact]
    public void BoxSelectionReportsCanvasRectsAndCancelsOnEscape()
    {
        var root = CreateRoot();
        var canvas = root.AddChild(new CanvasPanel { BoxSelectionEnabled = true });
        canvas.Style.Set("width: 600px; height: 300px;");
        canvas.SetView(Vector2.Zero, new Vector2(600, 300));
        int starts = 0;
        var finishes = new List<bool>();
        Rect box = default;
        bool additive = false;
        canvas.BoxSelectionStarted += shift =>
        {
            starts++;
            additive = shift;
        };
        canvas.BoxSelectionChanged += rect => box = rect;
        canvas.BoxSelectionFinished += finishes.Add;
        Update(root);

        root.SetMousePosition(new Vector2(400, 250));
        Update(root);
        root.SetMouseButton(MouseButtons.Left, true, KeyboardModifiers.Shift);
        Update(root);
        root.SetMousePosition(new Vector2(100, 50));
        Update(root);

        Assert.True(canvas.IsBoxSelecting);
        Assert.True(additive);
        Near(new Vector2(100, 50), box.Position);
        Near(new Vector2(300, 200), box.Size);
        var outline = canvas.Children.Single(p => p.HasClass("canvas-selection-box"));
        Assert.Equal(Length.Pixels(100), outline.Style.Left);
        Assert.Equal(Length.Pixels(300), outline.Style.Width);

        root.SetMouseButton(MouseButtons.Left, false);
        Update(root);
        Assert.False(canvas.IsBoxSelecting);
        Assert.Equal([false], finishes);

        root.SetMouseButton(MouseButtons.Left, true);
        Update(root);
        root.SetMousePosition(new Vector2(200, 100));
        Update(root);
        Escape(canvas);
        root.SetMouseButton(MouseButtons.Left, false);
        Update(root);
        Assert.Equal(2, starts);
        Assert.Equal([false, true], finishes);
    }

    [Fact]
    public void ContentPanelsMoveAndScaleWithTheView()
    {
        var root = CreateRoot();
        var canvas = root.AddChild(new CanvasPanel());
        canvas.Style.Set("width: 600px; height: 300px;");
        canvas.SetView(Vector2.Zero, new Vector2(600, 300));
        var node = canvas.Content.Add.Panel();
        node.Style.Set("position: absolute; left: 100px; top: 50px; width: 40px; height: 20px;");
        Update(root);
        Near(canvas.CanvasToScreen(new Vector2(100, 50)), node.PanelPositionToScreenPosition(Vector2.Zero) - canvas.Box.Rect.Position);

        canvas.ZoomAt(new Vector2(0, 0), 0.5f);
        Update(root);
        Near(canvas.CanvasToScreen(new Vector2(100, 50)), node.PanelPositionToScreenPosition(Vector2.Zero) - canvas.Box.Rect.Position);
        Near(canvas.CanvasToScreen(new Vector2(140, 70)), node.PanelPositionToScreenPosition(node.Box.Rect.Size) - canvas.Box.Rect.Position);
        Assert.Equal(new Vector2(40, 20), node.Box.Rect.Size);
    }

    [Fact]
    public void HandlesDragSnapClampLockAndCancel()
    {
        var root = CreateRoot();
        var canvas = root.AddChild(new CanvasPanel());
        canvas.Style.Set("width: 600px; height: 300px;");
        canvas.SetView(Vector2.Zero, new Vector2(600, 300));
        var handle = canvas.AddChild(new CanvasPanel.Handle { Position = new Vector2(200, 100), SnapIncrement = new Vector2(10), MovementBounds = new Rect(100, 50, 200, 150) });
        handle.Style.Set("width: 20px; height: 20px;");
        var moves = new List<Vector2>();
        var finishes = new List<bool>();
        handle.DragMoved += moves.Add;
        handle.DragFinished += finishes.Add;
        Update(root);
        Near(new Vector2(190, 90), handle.Box.Rect.Position);

        root.SetMousePosition(new Vector2(200, 100));
        Update(root);
        root.SetMouseButton(MouseButtons.Left, true);
        Update(root);
        Assert.True(handle.IsDragging);

        root.SetMousePosition(new Vector2(237, 123));
        Update(root);
        Near(new Vector2(240, 120), handle.Position);
        root.SetMousePosition(new Vector2(500, 250));
        Update(root);
        Near(new Vector2(300, 200), handle.Position);

        canvas.OnMouseWheel(new Vector2(0, 1));
        Near(new Vector2(600, 300), canvas.ViewMax);

        root.SetMouseButton(MouseButtons.Left, false);
        Update(root);
        Assert.Equal([false], finishes);
        Near(new Vector2(100, 100), moves.Last());

        handle.MovementBounds = null;
        root.SetMousePosition(new Vector2(300, 200));
        Update(root);
        root.SetMouseButton(MouseButtons.Left, true, KeyboardModifiers.Shift);
        Update(root);
        root.SetMousePosition(new Vector2(320, 260));
        Update(root);
        Assert.Equal(CanvasPanel.Handle.Axis.Vertical, handle.DragAxis);
        Near(new Vector2(300, 260), handle.Position);

        Escape(handle);
        root.SetMouseButton(MouseButtons.Left, false);
        Update(root);
        Assert.Equal([false, true], finishes);
        Near(new Vector2(300, 200), handle.Position);
    }

    private sealed class TestCanvas : CanvasPanel
    {
        public Rect TestViewport { get; set; } = new(40, 20, 600, 300);

        protected override Rect Viewport => TestViewport;
    }
}
