namespace Socotra;

public partial class CanvasPanel
{
    /// <summary>
    /// A panel on a canvas the user can drag with the left mouse button. By default it sits at <see cref="Position"/> in
    /// canvas coordinates and keeps its CSS size as the canvas zooms. Override <see cref="OnDragMove"/> to change your own
    /// data instead.
    /// </summary>
    /// <example>
    /// <code>
    /// // A point the user can drag around the canvas, snapped to whole units
    /// var point = canvas.AddChild(new CanvasPanel.Handle { Position = new Vector2(3, 4), SnapIncrement = Vector2.One });
    /// point.DragFinished += canceled => Save(point.Position);
    /// </code>
    /// </example>
    public class Handle : Panel
    {
        private CanvasPanel? _dragCanvas;
        private Vector2 _mouseStart;
        private Vector2 _screenStart;
        private Vector2 _lastMouse;
        private bool _lockAxis;

        /// <summary>Makes a handle at canvas point 0, 0.</summary>
        public Handle()
        {
            AddClass("canvas-handle");
            AcceptsFocus = true;
            CanDragScroll = false;
            Style.PointerEvents = PointerEvents.All;
        }

        /// <summary>Called when a drag starts.</summary>
        public event Action? DragStarted;

        /// <summary>Called as the handle is dragged, with how far it has moved from <see cref="DragOrigin"/> in canvas units.</summary>
        public event Action<Vector2>? DragMoved;

        /// <summary>Called when a drag ends, with true if it was canceled. A canceled drag puts the handle back where it started.</summary>
        public event Action<bool>? DragFinished;

        /// <summary>The closest canvas this handle is inside.</summary>
        public CanvasPanel? Canvas => Ancestors.OfType<CanvasPanel>().FirstOrDefault();

        /// <summary>Where the handle is, in canvas coordinates.</summary>
        public Vector2 Position { get; set; }

        /// <summary>Which point of the handle sits on <see cref="Position"/>, as a fraction of its size. 0.5, 0.5, the middle, by default.</summary>
        public Vector2 Anchor { get; set; } = new(0.5f);

        /// <summary>Places the handle at <see cref="Position"/> for you. Turn it off for a handle laid out inside another panel, like the title of a card.</summary>
        public bool Positioned { get; set; } = true;

        /// <summary>Whether the user can drag the handle. On by default.</summary>
        public bool DragEnabled { get; set; } = true;

        /// <summary>Which ways the handle can move.</summary>
        public Axis MovementAxis { get; set; }

        /// <summary>Holding Shift when a drag starts locks it to whichever way the mouse moves first. On by default.</summary>
        public bool LockAxisWithShift { get; set; } = true;

        /// <summary>Snaps the position to multiples of these, in canvas units. 0 on an axis doesn't snap it.</summary>
        public Vector2 SnapIncrement { get; set; }

        /// <summary>A rectangle in canvas coordinates the handle can't be dragged out of, or null for no limit.</summary>
        public Rect? MovementBounds { get; set; }

        /// <summary>Whether the handle is being dragged right now.</summary>
        public bool IsDragging { get; private set; }

        /// <summary>Where the handle was when the drag started.</summary>
        public Vector2 DragOrigin { get; private set; }

        /// <summary>Which ways the current drag can move, including a Shift lock.</summary>
        public Axis DragAxis { get; private set; }

        /// <summary>Ends the drag. Pass <paramref name="canceled"/> to put the handle back where it started. Escape, losing focus or the handle being deleted cancel it for you.</summary>
        public void FinishDrag(bool canceled = false)
        {
            if (!IsDragging)
            {
                return;
            }

            IsDragging = false;
            _dragCanvas = null;
            OnDragFinish(canceled);
            DragFinished?.Invoke(canceled);
        }

        /// <summary>Cancels the drag on Escape.</summary>
        protected override void OnEscape(PanelEvent e)
        {
            if (!IsDragging)
            {
                base.OnEscape(e);
                return;
            }

            FinishDrag(true);
            e.StopPropagation();
        }

        /// <inheritdoc/>
        public override void Tick()
        {
            base.Tick();
            if (IsDragging && (!HasActive || !DragEnabled || !IsVisible || Canvas != _dragCanvas))
            {
                FinishDrag(true);
            }

            if (Positioned && Canvas is { } canvas && Parent is not null)
            {
                var point = canvas.CanvasToScreen(Position) + canvas.Box.Rect.Position - Parent.Box.Rect.Position;
                Style.Position = PositionMode.Absolute;
                Style.Left = (point.X - (Box.Rect.Width * Anchor.X)) / ScaleToScreen;
                Style.Top = (point.Y - (Box.Rect.Height * Anchor.Y)) / ScaleToScreen;
            }
        }

        /// <summary>Cancels a drag that's still going.</summary>
        public override void OnDeleted()
        {
            FinishDrag(true);
            base.OnDeleted();
        }

        /// <summary>Decides whether a press starts a drag. Override it to select things or check what was hit, and return false to refuse.</summary>
        protected virtual bool OnDragStart(MousePanelEvent e) => true;

        /// <summary>Moves the handle <paramref name="delta"/> canvas units from <see cref="DragOrigin"/>. Override it to move other panels or change your own data.</summary>
        protected virtual void OnDragMove(Vector2 delta) => Position = DragOrigin + delta;

        /// <summary>Called when a drag ends. By default a canceled drag puts the handle back at <see cref="DragOrigin"/>.</summary>
        protected virtual void OnDragFinish(bool canceled)
        {
            if (canceled)
            {
                Position = DragOrigin;
            }
        }

        /// <summary>Starts a drag on a left press.</summary>
        protected override void OnMouseDown(MousePanelEvent e)
        {
            if (e.Button != "mouseleft")
            {
                base.OnMouseDown(e);
                return;
            }

            e.StopPropagation();
            var canvas = Canvas;
            if (!DragEnabled || IsDragging || canvas is null || canvas.IsPanning || canvas.IsBoxSelecting || !canvas.CanNavigate)
            {
                return;
            }

            Focus();
            if (!OnDragStart(e))
            {
                return;
            }

            _dragCanvas = canvas;
            DragOrigin = Position;
            _mouseStart = canvas.ScreenToCanvas(canvas.MousePosition);
            _screenStart = _lastMouse = canvas.MousePosition;
            DragAxis = MovementAxis;
            _lockAxis = LockAxisWithShift && e.HasShift;
            IsDragging = true;
            DragStarted?.Invoke();
        }

        /// <summary>Moves the drag with the mouse.</summary>
        protected override void OnMouseMove(MousePanelEvent e)
        {
            if (!IsDragging)
            {
                base.OnMouseMove(e);
                return;
            }

            e.StopPropagation();
            MoveDrag();
        }

        /// <summary>Ends the drag when the left button comes up.</summary>
        protected override void OnMouseUp(MousePanelEvent e)
        {
            if (!IsDragging || e.Button != "mouseleft")
            {
                base.OnMouseUp(e);
                return;
            }

            e.StopPropagation();
            MoveDrag();
            FinishDrag();
        }

        /// <summary>Cancels the drag when the handle loses focus.</summary>
        protected override void OnBlur(PanelEvent e)
        {
            if (e.Target == this)
            {
                FinishDrag(true);
            }

            base.OnBlur(e);
        }

        private void MoveDrag()
        {
            if (_dragCanvas is not { } canvas || canvas.MousePosition == _lastMouse)
            {
                return;
            }

            _lastMouse = canvas.MousePosition;
            var delta = canvas.ScreenToCanvas(canvas.MousePosition) - _mouseStart;
            var pixels = canvas.MousePosition - _screenStart;
            if (_lockAxis && DragAxis == Axis.Both && pixels.Length() > 4 * ScaleToScreen)
            {
                DragAxis = MathF.Abs(pixels.X) > MathF.Abs(pixels.Y) ? Axis.Horizontal : Axis.Vertical;
            }

            var point = DragOrigin + delta;
            if (SnapIncrement.X > 0)
            {
                point.X = MathF.Round(point.X / SnapIncrement.X) * SnapIncrement.X;
            }

            if (SnapIncrement.Y > 0)
            {
                point.Y = MathF.Round(point.Y / SnapIncrement.Y) * SnapIncrement.Y;
            }

            if (MovementBounds is { } bounds)
            {
                point.X = Math.Clamp(point.X, bounds.Left, bounds.Right);
                point.Y = Math.Clamp(point.Y, bounds.Top, bounds.Bottom);
            }

            delta = point - DragOrigin;
            if (DragAxis == Axis.Horizontal)
            {
                delta.Y = 0;
            }
            else if (DragAxis == Axis.Vertical)
            {
                delta.X = 0;
            }

            OnDragMove(delta);
            DragMoved?.Invoke(delta);
        }

        /// <summary>Which ways a handle can move.</summary>
        public enum Axis
        {
            /// <summary>Any direction.</summary>
            Both,

            /// <summary>Only left and right.</summary>
            Horizontal,

            /// <summary>Only up and down.</summary>
            Vertical,
        }
    }
}
