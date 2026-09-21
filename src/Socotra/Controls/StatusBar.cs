namespace Socotra;

/// <summary>
/// A strip along the bottom of a window, with widgets on the <see cref="Left"/> and ones that always show on the
/// <see cref="Right"/>. A message from <see cref="ShowMessage"/> takes the left widgets' place for a while, without deleting them.
/// </summary>
/// <example>
/// <code>
/// // A cursor position on the left, a connection state on the right, and a message after saving
/// var status = new StatusBar { Parent = window };
/// status.AddLeft(new Label("Ln 1, Col 1"));
/// status.AddRight(new Label("Online"));
/// status.ShowMessage("Saved", 3);
/// </code>
/// </example>
[StyleSheet.Inline("statusbar", Styles)]
public class StatusBar : Panel
{
    private const string Styles = """
        .statusbar { flex-direction: row; align-items: center; flex-shrink: 0; min-width: 0; overflow: hidden; pointer-events: all; }
        .statusbar > .status-left, .statusbar > .status-right { align-items: center; min-width: 0; overflow: hidden; }
        .statusbar > .status-left { flex-grow: 1; flex-shrink: 1; }
        .statusbar > .status-right { flex-shrink: 0; margin-left: auto; }
        .statusbar > .status-message { flex-grow: 1; flex-shrink: 1; min-width: 0; white-space: nowrap; text-overflow: ellipsis; overflow: hidden; }
        .status-left > *, .status-right > * { flex-shrink: 0; }
        .status-separator { flex-shrink: 0; }
        """;

    private readonly Label _messageLabel;
    private double? _expiresAt;
    private float? _showFor;

    /// <summary>Makes an empty status bar.</summary>
    public StatusBar()
    {
        AddClass("statusbar");
        Left = Add.Panel("status-left");
        _messageLabel = Add.Label("", "status-message");
        _messageLabel.Style.Display = DisplayMode.None;
        Right = Add.Panel("status-right");
    }

    /// <summary>Called with the new message when it changes, and with an empty string when it's cleared.</summary>
    public event Action<string>? MessageChanged;

    /// <summary>The panel holding the usual widgets. They're hidden while a message shows.</summary>
    public Panel Left { get; }

    /// <summary>The panel holding widgets that always show, even with a message up.</summary>
    public Panel Right { get; }

    /// <summary>The message showing, or an empty string.</summary>
    public string Message => _messageLabel.Text ?? "";

    /// <summary>Adds <paramref name="widget"/> to the <see cref="Left"/> side. The status bar owns it from then on.</summary>
    /// <param name="widget">The panel to add.</param>
    /// <param name="stretch">How much of the spare room it takes, compared with the other widgets. 0 keeps it at its natural size.</param>
    /// <exception cref="ArgumentException"><paramref name="widget"/> is deleted, a root panel, or the status bar or one of its ancestors.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="stretch"/> is negative.</exception>
    public T AddLeft<T>(T widget, int stretch = 0)
        where T : Panel => AddWidget(Left, widget, stretch);

    /// <summary>Adds <paramref name="widget"/> to the <see cref="Right"/> side, where it always shows. The status bar owns it from then on.</summary>
    /// <param name="widget">The panel to add.</param>
    /// <param name="stretch">How much of the spare room it takes, compared with the other widgets. 0 keeps it at its natural size.</param>
    /// <exception cref="ArgumentException"><paramref name="widget"/> is deleted, a root panel, or the status bar or one of its ancestors.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="stretch"/> is negative.</exception>
    public T AddRight<T>(T widget, int stretch = 0)
        where T : Panel => AddWidget(Right, widget, stretch);

    /// <summary>Takes <paramref name="widget"/> out of the status bar without deleting it; it's yours again. Returns false if it wasn't in either side.</summary>
    public bool RemoveWidget(Panel? widget)
    {
        if (widget is null || (widget.Parent != Left && widget.Parent != Right))
        {
            return false;
        }

        widget.Parent = null;
        return true;
    }

    /// <summary>Adds a separator to the <see cref="Left"/> side, or to the <see cref="Right"/> side when <paramref name="right"/> is true.</summary>
    public Panel AddSeparator(bool right = false) => (right ? Right : Left).Add.Panel("status-separator");

    /// <summary>
    /// Shows <paramref name="text"/> in place of the left widgets for <paramref name="seconds"/>, replacing any message
    /// already there. Pass 0 or less to keep it until you replace or clear it. Empty text clears it.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seconds"/> is infinite or not a number.</exception>
    public void ShowMessage(string? text, float seconds = 5)
    {
        if (!float.IsFinite(seconds))
        {
            throw new ArgumentOutOfRangeException(nameof(seconds));
        }

        text ??= "";
        _expiresAt = null;
        _showFor = text.Length > 0 && seconds > 0 ? seconds : null;
        if (Message == text)
        {
            return;
        }

        _messageLabel.Text = text;
        _messageLabel.Tooltip = text;
        var hasMessage = text.Length > 0;
        Left.Style.Display = hasMessage ? DisplayMode.None : DisplayMode.Flex;
        _messageLabel.Style.Display = hasMessage ? DisplayMode.Flex : DisplayMode.None;
        MessageChanged?.Invoke(text);
    }

    /// <summary>Clears the message and shows the left widgets again.</summary>
    public void ClearMessage() => ShowMessage("");

    /// <inheritdoc/>
    public override void Tick()
    {
        base.Tick();
        if (_showFor is { } seconds)
        {
            _expiresAt = TimeNow + seconds;
            _showFor = null;
        }

        if (_expiresAt is { } deadline && TimeNow >= deadline)
        {
            ClearMessage();
        }
    }

    private T AddWidget<T>(Panel slot, T widget, int stretch)
        where T : Panel
    {
        ArgumentNullException.ThrowIfNull(widget);
        ArgumentOutOfRangeException.ThrowIfNegative(stretch);
        if (widget.IsDeleted || widget.IsDeleting || widget is RootPanel || AncestorsAndSelf.Contains(widget) || widget == Left || widget == Right)
        {
            throw new ArgumentException("The widget cannot be owned by this status bar.", nameof(widget));
        }

        widget.Parent = slot;
        widget.Style.FlexGrow = stretch;
        widget.Style.FlexShrink = stretch > 0 ? 1 : 0;
        return widget;
    }
}
