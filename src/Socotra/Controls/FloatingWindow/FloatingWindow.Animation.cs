namespace Socotra;

public partial class FloatingWindow
{
    private const float AnimationDuration = 0.32f;

    private float _animationElapsed;
    private Rect _expandedRect;
    private Rect _animationDockRect;
    private PanelTransform? _previousTransform;
    private Length? _previousOriginX;
    private Length? _previousOriginY;

    /// <summary>Returns <c>true</c> while the window is shrinking down to its icon or growing back from it.</summary>
    public bool IsAnimating { get; private set; }

    private void BeginWindowAnimation(Rect expanded)
    {
        _expandedRect = expanded;
        _animationElapsed = 0;
        _previousTransform = Style.Transform;
        _previousOriginX = Style.TransformOriginX;
        _previousOriginY = Style.TransformOriginY;
        IsAnimating = true;
        SetClass("animating", true);
        BringToFront();
        ApplyWindowAnimation();
        StateHasChanged();
        SetNeedsFinalLayout();
    }

    private void TickWindowAnimation()
    {
        if (!IsAnimating)
        {
            return;
        }

        _animationElapsed += TimeDelta;
        if (!IsVisible || _animationElapsed >= AnimationDuration)
        {
            FinishWindowAnimation();
            return;
        }

        ApplyWindowAnimation();
        SetNeedsFinalLayout();
    }

    private void ApplyWindowAnimation()
    {
        if (_expandedRect.Width <= 0 || _expandedRect.Height <= 0)
        {
            return;
        }

        var progress = Math.Clamp(_animationElapsed / AnimationDuration, 0, 1);
        var amount = IsMinimized ? progress : 1 - progress;
        var travel = amount * amount * (3 - (2 * amount));
        var narrowing = 1 - ((1 - amount) * (1 - amount));
        var dock = IsMinimized ? GetMinimizedRect() : _animationDockRect;

        var width = MathX.Lerp(_expandedRect.Width, dock.Width, narrowing);
        var height = MathX.Lerp(_expandedRect.Height, dock.Height, travel);
        var center = Vector2.Lerp(_expandedRect.Center, dock.Center, travel);
        var topLeft = center - (new Vector2(width, height) * 0.5f);
        var offset = (topLeft - _expandedRect.Position) * ScaleFromScreen;
        var transform = new PanelTransform();
        transform.AddTranslate(offset.X, offset.Y);
        transform.AddScale(new Vector3(width / _expandedRect.Width, height / _expandedRect.Height, 1));

        Style.TransformOriginX = 0;
        Style.TransformOriginY = 0;
        Style.Transform = transform;
    }

    private void FinishWindowAnimation()
    {
        IsAnimating = false;
        Style.Transform = _previousTransform;
        Style.TransformOriginX = _previousOriginX;
        Style.TransformOriginY = _previousOriginY;
        if (IsMinimized)
        {
            Style.Width = MinimizedSize;
            Style.Height = MinimizedSize;
        }

        SetClass("animating", false);
        SetClass("minimized", IsMinimized);
        StateHasChanged();
        SetNeedsFinalLayout();
    }
}
