using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Socotra;

/// <summary>
/// <c>FloatingWindow</c> is a panel that works like a small desktop window inside your UI. The user can drag it
/// around by its title bar, and you can give it minimize and close buttons too. Usually you may want it for things
/// like a chat, an inventory or debug tools that the user should be able to move out of the way.
/// <para>
/// Windows with the same parent stack on top of each other, and whichever one was clicked last comes to the front.
/// In markup it's <c>&lt;floatingwindow&gt;</c>, and everything you put inside it goes into the window.
/// </para>
/// </summary>
/// <example>
/// <code>
/// // A chat window that can be minimized and won't go off the screen
/// &lt;floatingwindow Title="Chat" Icon="💬" ShowMinimizeButton="@true" KeepOnScreen="@true"&gt;
///     &lt;ChatLog /&gt;
/// &lt;/floatingwindow&gt;
/// </code>
/// </example>
[StyleSheet.Inline("floatingwindow", Styles)]
public partial class FloatingWindow : Panel
{
    private const float MinimizedSize = 64;
    private const float MinimizedSpacing = 8;
    private const float MinimizedOverhang = 12;

    private static readonly List<FloatingWindow> MinimizedWindows = [];

    private Length? _restoreWidth;
    private Length? _restoreHeight;
    private int _minimizedIndex;
    private int _minimizedCount;
    private Vector2? _position;
    private Vector2 _dragStart;
    private bool _wasDragged;
    private bool _wasActive;
    private Rect _viewport;
    private float _layoutScale;
    private bool _constrained;

    /// <summary>
    /// Creates an empty window. Keep in mind that clicking the window itself doesn't take keyboard focus away from
    /// whatever is inside it, so a text entry in there keeps its focus when the user grabs the title bar.
    /// </summary>
    public FloatingWindow()
    {
        AddClass("floating-window");
        CanDragScroll = false;
        FocusOnClick = false;
    }

    /// <summary>Text that is shown in the title bar.</summary>
    [Parameter]
    public string? Title { get; set; }

    /// <summary>
    /// Emoji that is shown in the title bar, left of the <see cref="Title"/>. It's also what the window turns into
    /// when it's minimized, so pick something the user will recognize.
    /// </summary>
    [Parameter]
    public string? Icon { get; set; } = "🪟";

    /// <summary>Set this to <c>true</c> if you want a minimize button in the title bar.</summary>
    [Parameter]
    public bool ShowMinimizeButton { get; set; }

    /// <summary>Set this to <c>true</c> if you want a close button in the title bar.</summary>
    [Parameter]
    public bool ShowCloseButton { get; set; }

    /// <summary>
    /// Set this to <c>true</c> to stop the window from leaving the screen. It stays inside even when the screen
    /// gets smaller or the window's content grows.
    /// </summary>
    [Parameter]
    public bool KeepOnScreen { get; set; }

    /// <summary>
    /// Called when the user presses the close button, or when you call <see cref="RequestClose"/>. If you don't set
    /// it, the window simply deletes itself, so set this if you'd rather hide it or ask the user first.
    /// </summary>
    [Parameter]
    public Action? OnCloseRequested { get; set; }

    /// <summary>Returns <c>true</c> while the window is minimized to an icon at the bottom of the screen.</summary>
    public bool IsMinimized { get; private set; }

    /// <summary>Returns <c>true</c> once the user has moved the window away from where it started.</summary>
    public bool HasBeenDragged { get; private set; }

    /// <summary>Returns <c>true</c> while the user is dragging the window by its title bar.</summary>
    public bool IsDragging { get; private set; }

    /// <summary>
    /// Moves the window so its top left corner is at <paramref name="screenPosition"/>, in screen pixels. The window
    /// stays there even if its parent moves or changes size.
    /// </summary>
    public void MoveTo(Vector2 screenPosition)
    {
        if (IsMinimized || IsAnimating)
        {
            return;
        }

        StopThrow();
        if (_position == screenPosition)
        {
            return;
        }

        _position = screenPosition;
        SetNeedsFinalLayout();
    }

    /// <summary>
    /// Brings the window in front of the other windows under the same parent. Please note that this doesn't give it
    /// keyboard focus.
    /// </summary>
    public void BringToFront()
    {
        if (Parent is { } parent)
        {
            parent.SetChildIndex(this, parent.ChildrenCount - 1);
        }
    }

    /// <summary>
    /// Does the same thing as pressing the close button: calls <see cref="OnCloseRequested"/> if you've set it, and
    /// deletes the window if you haven't.
    /// </summary>
    public void RequestClose()
    {
        StopThrow();
        if (OnCloseRequested is { } close)
        {
            close();
        }
        else
        {
            Delete();
        }
    }

    /// <summary>
    /// Minimizes the window to its <see cref="Icon"/> at the bottom of the screen. Everything inside the window stays
    /// the way it was, so it's all still there when you <see cref="Restore"/> it.
    /// </summary>
    public void Minimize()
    {
        if (IsMinimized || IsAnimating)
        {
            return;
        }

        EndMove(false);
        StopThrow();
        _position = Box.Rect.Position;
        _restoreWidth = Style.Width;
        _restoreHeight = Style.Height;
        IsMinimized = true;
        MinimizedWindows.Add(this);
        UpdateMinimizedPlacement();
        BeginWindowAnimation(Box.Rect);
    }

    /// <summary>Opens a minimized window back up at the spot where it was before, and brings it to the front.</summary>
    public void Restore()
    {
        if (!IsMinimized || IsAnimating)
        {
            return;
        }

        _animationDockRect = Box.Rect;
        IsMinimized = false;
        MinimizedWindows.Remove(this);
        Style.Width = _restoreWidth;
        Style.Height = _restoreHeight;
        SetClass("minimized", false);
        BeginWindowAnimation(_expandedRect);
    }

    /// <summary>
    /// Don't forget to call <c>base.Tick()</c> if you override this, otherwise the window won't come to the front
    /// when it's clicked or stay on the screen with <see cref="KeepOnScreen"/>.
    /// </summary>
    public override void Tick()
    {
        base.Tick();

        if (HasActive && !_wasActive)
        {
            StopThrow();
            BringToFront();
        }

        _wasActive = HasActive;
        if (!IsVisible)
        {
            EndMove(true);
        }

        if (IsMinimized)
        {
            UpdateMinimizedPlacement();
        }

        TickWindowAnimation();
        TickThrow();

        if (FindRootPanel() is not { } root)
        {
            return;
        }

        var bounds = root.Box.Rect;
        if (_viewport == bounds && _layoutScale == ScaleFromScreen && _constrained == KeepOnScreen)
        {
            return;
        }

        _viewport = bounds;
        _layoutScale = ScaleFromScreen;
        _constrained = KeepOnScreen;
        Style.MaxWidth = KeepOnScreen ? bounds.Width * ScaleFromScreen : null;
        Style.MaxHeight = KeepOnScreen ? bounds.Height * ScaleFromScreen : null;
        SetNeedsFinalLayout();
    }

    /// <inheritdoc/>
    public override void OnDeleted()
    {
        MinimizedWindows.Remove(this);
        base.OnDeleted();
    }

    internal void BeginMove(Vector2 delta)
    {
        if (IsAnimating || IsMinimized)
        {
            return;
        }

        BeginDragMotion(delta);
        _dragStart = Box.Rect.Position;
        _wasDragged = HasBeenDragged;
        HasBeenDragged = true;
        IsDragging = true;
        BringToFront();
        SetClass("dragging", true);
    }

    internal void Move(Vector2 delta)
    {
        if (!IsDragging)
        {
            return;
        }

        SampleDragMotion(delta);
        MoveTo(_dragStart + delta);
    }

    internal void EndMove(bool cancelled)
    {
        if (!IsDragging)
        {
            return;
        }

        if (cancelled)
        {
            MoveTo(_dragStart);
            HasBeenDragged = _wasDragged;
        }
        else
        {
            StartThrow();
        }

        IsDragging = false;
        SetClass("dragging", false);
    }

    /// <inheritdoc/>
    protected override void OnMouseDown(MousePanelEvent e)
    {
        base.OnMouseDown(e);
        StopThrow();
        BringToFront();
    }

    /// <inheritdoc/>
    protected override void OnLayout(ref Rect rect)
    {
        base.OnLayout(ref rect);
        if (IsMinimized && !IsAnimating)
        {
            rect.Position = GetMinimizedRect().Position;
            return;
        }

        if (_position is { } location)
        {
            rect.Position = location;
        }

        if (KeepOnScreen && FindRootPanel() is { } root)
        {
            var bounds = root.Box.Rect;
            rect.Position = new Vector2(
                Math.Clamp(rect.Left, bounds.Left, Math.Max(bounds.Left, bounds.Right - rect.Width)),
                Math.Clamp(rect.Top, bounds.Top, Math.Max(bounds.Top, bounds.Bottom - rect.Height)));
        }

        if (_position.HasValue)
        {
            _position = rect.Position;
        }

        if (IsAnimating)
        {
            _expandedRect = rect;
            ApplyWindowAnimation();
        }
    }

    /// <inheritdoc/>
    protected override void BuildRenderTree(RenderTreeBuilder tree)
    {
        tree.OpenElement<FloatingWindowTitleBar>(0);
        tree.AddAttribute(1, "class", "floating-window-header");

        tree.OpenElement<Label>(2);
        tree.AddAttribute(3, "class", "floating-window-icon");
        tree.AddAttribute(4, "text", Icon);
        tree.CloseElement();

        tree.OpenElement<Label>(5);
        tree.AddAttribute(6, "class", "floating-window-title");
        tree.AddAttribute(7, "text", Title);
        tree.CloseElement();

        if (ShowMinimizeButton)
        {
            tree.OpenElement<Button>(8);
            tree.AddAttribute(9, "class", "floating-window-nodrag floating-window-control");
            tree.AddAttribute(10, "icon", "remove");
            tree.AddAttribute(11, "onclick", (Action)Minimize);
            tree.CloseElement();
        }

        if (ShowCloseButton)
        {
            tree.OpenElement<Button>(12);
            tree.AddAttribute(13, "class", "floating-window-nodrag floating-window-control");
            tree.AddAttribute(14, "icon", "close");
            tree.AddAttribute(15, "onclick", (Action)RequestClose);
            tree.CloseElement();
        }

        tree.CloseElement();

        tree.OpenElement<Panel>(17);
        tree.AddAttribute(18, "class", "floating-window-content");
        tree.AddContent(19, ChildContent);
        tree.CloseElement();

        tree.OpenElement<Button>(20);
        tree.AddAttribute(21, "class", "floating-window-restore");
        tree.AddAttribute(22, "text", Icon);
        tree.AddAttribute(23, "onclick", (Action)Restore);
        tree.AddAttribute(24, "tooltip", $"Restore {Title}");
        tree.CloseElement();
    }

    /// <inheritdoc/>
    protected override string? GetRenderTreeChecksum() => $"{BuildHash()}";

    /// <inheritdoc/>
    protected override int BuildHash() => HashCode.Combine(Title, Icon, ShowCloseButton, ShowMinimizeButton, IsMinimized);

    private void UpdateMinimizedPlacement()
    {
        var root = FindRootPanel();
        var count = 0;
        var index = 0;
        foreach (var window in MinimizedWindows)
        {
            if (window.IsDeleted || !window.IsVisible || window.FindRootPanel() != root)
            {
                continue;
            }

            if (window == this)
            {
                index = count;
            }

            count++;
        }

        if (_minimizedIndex == index && _minimizedCount == count)
        {
            return;
        }

        _minimizedIndex = index;
        _minimizedCount = count;
        SetNeedsFinalLayout();
    }

    private Rect GetMinimizedRect()
    {
        var bounds = FindRootPanel()?.Box.Rect ?? default;
        var size = MinimizedSize * ScaleToScreen;
        var spacing = (MinimizedSize + MinimizedSpacing) * ScaleToScreen;
        var rowWidth = (Math.Max(0, _minimizedCount - 1) * spacing) + size;
        return new Rect(
            bounds.Left + ((bounds.Width - rowWidth) * 0.5f) + (_minimizedIndex * spacing),
            bounds.Bottom - size + (MinimizedOverhang * ScaleToScreen),
            size,
            size);
    }
}
