namespace Socotra;

public partial class Panel
{
    /// <summary>Whether the panel can take input focus, which gets it key presses. Tab and the arrow keys move between panels that accept focus.</summary>
    public bool AcceptsFocus { get; set; }

    /// <summary>
    /// Where the panel sits in the Tab order. 0, the default, is tree order. Positive values come
    /// first, lowest first. Negative values are skipped by Tab and the arrow keys, but the panel can
    /// still be focused by clicking it or calling <see cref="Focus"/>.
    /// </summary>
    public int TabIndex { get; set; }

    /// <summary>Whether the panel takes text from an input method editor, for typing languages like Japanese. Text entry controls return true.</summary>
    public virtual bool AcceptsImeInput => false;

    /// <summary>Where the mouse is, relative to the panel's top left corner.</summary>
    public Vector2 MousePosition => ScreenPositionToPanelPosition(ScreenMousePosition);

    /// <summary>Whether this panel has the mouse captured. See <see cref="SetMouseCapture"/>.</summary>
    public bool HasMouseCapture => FindRootPanel()?.Input.MouseCapture == this;

    internal Vector2 ScreenMousePosition => FindRootPanel()?.Input.MousePosition ?? Vector2.Zero;

    /// <summary>Where typing happens in this panel, on screen. Override it in text controls so an input method editor's window shows next to the caret instead of over the text.</summary>
    public virtual Rect ImeCaretRect => Box.Rect;

    /// <summary>Gives input focus to this panel, or to its nearest ancestor that accepts it. Returns false if none of them do.</summary>
    public bool Focus() => FindRootPanel()?.Input.SetFocus(this) ?? false;

    /// <summary>Takes input focus away from this panel, giving it to its nearest ancestor that accepts it.</summary>
    public bool Blur() => FindRootPanel()?.Input.ClearFocus(this) ?? false;

    /// <summary>Moves focus to the panel after this one in Tab order.</summary>
    public bool FocusNext() => FindRootPanel()?.Input.MoveFocus(this, backwards: false) ?? false;

    /// <summary>Moves focus to the panel before this one in Tab order.</summary>
    public bool FocusPrevious() => FindRootPanel()?.Input.MoveFocus(this, backwards: true) ?? false;

    /// <summary>
    /// Captures the mouse while <paramref name="b"/> is true, for things like changing a number by dragging. Read how
    /// far the mouse moved from <see cref="RootPanel.MouseDelta"/> in <see cref="Tick"/>.
    /// </summary>
    public void SetMouseCapture(bool b) => FindRootPanel()?.Input.SetMouseCapture(this, b);

    /// <summary>Whether this panel is entirely inside a rectangle on screen, or overlaps it at all when <paramref name="fullyInside"/> is false.</summary>
    public bool IsInside(Rect rect, bool fullyInside) => Box.Rect.IsInside(rect, fullyInside);

    /// <summary>Whether a point on screen is over this panel. Transforms, rounded corners and <c>border-shape</c> count.</summary>
    public bool IsInside(Vector2 point) => IsInsideUntransformed(GlobalMatrix is { } matrix ? matrix.Transform(point) : point);

    internal bool IsInsideUntransformed(Vector2 point)
    {
        if (LayoutTree.IsInlineParticipant)
        {
            return LayoutTree.ContainsInlineContent(point);
        }

        var rect = Box.Rect;
        if (point.X < rect.Left || point.X > rect.Right || point.Y < rect.Top || point.Y > rect.Bottom || ComputedStyle is not { } style)
        {
            return false;
        }

        var local = point - rect.Position;
        if (style.BorderShape is { IsNone: false } shape)
        {
            if (shape.Kind == BorderShapeKind.Circle)
            {
                var circle = shape.ResolveCircle(new Rect(Vector2.Zero, rect.Size));
                if (Vector2.Distance(local, circle.Center) > circle.Radius)
                {
                    return false;
                }
            }
            else if (!IsInsidePolygon(local, rect.Size, shape.Points))
            {
                return false;
            }
        }

        if (!style.HasBorderRadius)
        {
            return true;
        }

        var radii = _paintCache.OuterRadii;
        var right = rect.Width - local.X;
        var bottom = rect.Height - local.Y;
        return !OutsideCorner(radii.TopLeft.X, radii.TopLeft.Y, local.X, local.Y)
            && !OutsideCorner(radii.TopRight.X, radii.TopRight.Y, right, local.Y)
            && !OutsideCorner(radii.BottomRight.X, radii.BottomRight.Y, right, bottom)
            && !OutsideCorner(radii.BottomLeft.X, radii.BottomLeft.Y, local.X, bottom);
    }

    private static bool IsInsidePolygon(Vector2 point, Vector2 size, IReadOnlyList<BorderShapePoint> points)
    {
        bool inside = false;
        for (int i = 0, j = points.Count - 1; i < points.Count; j = i++)
        {
            var a = new Vector2(points[i].X.GetPixels(size.X), points[i].Y.GetPixels(size.Y));
            var b = new Vector2(points[j].X.GetPixels(size.X), points[j].Y.GetPixels(size.Y));
            if ((a.Y > point.Y) != (b.Y > point.Y) && point.X < ((b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y)) + a.X)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    /// <summary>
    /// Called when a key or mouse button goes down or up while this panel has focus, and for mouse buttons
    /// pressed on it. By default it passes the button to the parent, unless <see cref="ButtonEvent.StopPropagation"/> is
    /// set. Override it to handle buttons, and call the base method for the ones you leave alone.
    /// </summary>
    public virtual void OnButtonEvent(ButtonEvent e)
    {
        if (!e.StopPropagation)
        {
            Parent?.OnButtonEvent(e);
        }
    }

    /// <summary>Called when a character is typed while this panel has focus. By default it passes it to the parent.</summary>
    public virtual void OnKeyTyped(char k) => Parent?.OnKeyTyped(k);

    /// <summary>Called for a typed character with the modifier keys held when it was typed. By default it calls <see cref="OnKeyTyped(char)"/>.</summary>
    public virtual void OnKeyTyped(char k, KeyboardModifiers modifiers) => OnKeyTyped(k);

    /// <summary>
    /// Called when a button is pressed, or repeats, while this panel has focus. By default it passes it to the
    /// parent, unless <see cref="ButtonEvent.StopPropagation"/> is set, and at the root panel Tab and the arrow keys
    /// move focus, so handle arrows here to keep them. Buttons use this to click on Enter and Space.
    /// </summary>
    public virtual void OnButtonTyped(ButtonEvent e)
    {
        if (!e.StopPropagation)
        {
            Parent?.OnButtonTyped(e);
        }
    }

    /// <summary>Called when Ctrl+V pastes <paramref name="text"/> while this panel has focus. By default it passes it to the parent.</summary>
    public virtual void OnPaste(string text) => Parent?.OnPaste(text);

    /// <summary>Returns the text Ctrl+C or Ctrl+X should put on the clipboard, if this panel has any. By default it asks the parent.</summary>
    public virtual string? GetClipboardValue(bool cut)
    {
        if (LayoutTree.IsInlineParticipant)
        {
            return LayoutTree.SelectedInlineText;
        }

        return AllowChildSelection ? CollectSelectedChildrenText(this) : Parent?.GetClipboardValue(cut);
    }

    internal bool TryClickFromKeyboard(ButtonEvent e)
    {
        if (!e.Pressed || e.Button is not ("enter" or "space"))
        {
            return false;
        }

        CreateClick();
        return true;
    }

    internal void CreateClick() => CreateEvent(new MousePanelEvent("onclick", this, "mouseleft"));

    private static bool OutsideCorner(float radiusX, float radiusY, float fromSide, float fromTopOrBottom)
    {
        if (fromSide >= radiusX || fromTopOrBottom >= radiusY)
        {
            return false;
        }

        var x = (radiusX - fromSide) / radiusX;
        var y = (radiusY - fromTopOrBottom) / radiusY;
        return x * x + y * y > 1;
    }
}
