namespace Socotra;

/// <summary>
/// The tooltips of one <see cref="RootPanel"/>, found through <see cref="RootPanel.Tooltips"/>. While the mouse rests on a
/// panel with a <see cref="Panel.Tooltip"/>, or inside one, its tooltip shows next to the mouse, kept on the screen.
/// </summary>
/// <example>
/// <code>
/// // Desktop-style tooltips that wait half a second before showing
/// root.Tooltips.Delay = 0.5f;
/// </code>
/// </example>
public sealed class TooltipSystem
{
    private const float CursorOffset = 20;

    private readonly RootPanel _root;
    private Panel? _hovered;
    private Panel? _tooltip;
    private Panel? _suppressed;
    private double _hoverStart;
    private double _lastHidden = double.NegativeInfinity;
    private Vector2 _lastCursor;
    private bool _waitForMove;

    internal TooltipSystem(RootPanel root)
    {
        _root = root;
    }

    /// <summary>How long the mouse has to rest on a panel before its tooltip shows, in seconds. It's 0 by default, so tooltips show straight away.</summary>
    public float Delay { get; set; }

    /// <summary>For this long after a tooltip goes away, in seconds, the next one shows without waiting for <see cref="Delay"/>, so the user can read along a row of buttons.</summary>
    public float GraceTime { get; set; } = 0.5f;

    /// <summary>The tooltip panel showing right now, or null.</summary>
    public Panel? Current => _tooltip is { IsDeleted: false } ? _tooltip : null;

    /// <summary>Whether a tooltip is showing.</summary>
    public bool IsShowing => _tooltip is { IsDeleted: false };

    internal void SetHovered(Panel? current, Vector2 cursor)
    {
        var moved = cursor != _lastCursor;
        _lastCursor = cursor;
        while (current is not null && !current.HasTooltip)
        {
            current = current.Parent;
        }

        if (moved)
        {
            _waitForMove = false;
        }

        if (current == _hovered)
        {
            return;
        }

        Hide();
        _hovered = current;
        _hoverStart = _root.Time;
        if (!moved)
        {
            _waitForMove = true;
        }

        if (_suppressed != current)
        {
            _suppressed = null;
        }
    }

    internal void Frame(Vector2 cursor, bool cursorVisible)
    {
        if (!cursorVisible)
        {
            Hide();
            _hovered = null;
            return;
        }

        if (_tooltip is { IsDeleted: false } tooltip)
        {
            if (_hovered is not { IsDeleted: false } owner)
            {
                Hide();
                return;
            }

            Place(tooltip, cursor);
            owner.UpdateTooltip(tooltip);
            return;
        }

        if (_hovered is null || _hovered == _suppressed || _waitForMove)
        {
            return;
        }

        if (_hovered.IsDeleted)
        {
            _hovered = null;
            return;
        }

        var delay = _root.Time - _lastHidden < GraceTime ? 0 : Delay;
        if (_root.Time - _hoverStart >= delay)
        {
            Show(_hovered, cursor);
        }
    }

    private void Show(Panel owner, Vector2 cursor)
    {
        _tooltip = owner.BuildTooltip();
        if (_tooltip is null)
        {
            _suppressed = owner;
            return;
        }

        _tooltip.Parent ??= owner.FindRootPanel();
        Place(_tooltip, cursor);
    }

    private void Hide()
    {
        if (_tooltip is null)
        {
            return;
        }

        var tooltip = _tooltip;
        _tooltip = null;
        _lastHidden = _root.Time;
        tooltip.Delete();
    }

    private void Place(Panel tooltip, Vector2 cursor)
    {
        var size = _root.Bounds.Size;
        var position = cursor - _root.Bounds.Position;
        var style = tooltip.Style;
        style.Left = null;
        style.Right = null;
        style.Top = null;
        style.Bottom = null;
        if (position.X < size.X * 0.7f)
        {
            style.Left = (position.X + CursorOffset) / _root.Scale;
        }
        else
        {
            style.Right = (size.X - position.X + CursorOffset) / _root.Scale;
        }

        if (position.Y > size.Y * 0.1f)
        {
            style.Bottom = (size.Y - position.Y + CursorOffset) / _root.Scale;
        }
        else
        {
            style.Top = (position.Y + CursorOffset) / _root.Scale;
        }
    }
}
