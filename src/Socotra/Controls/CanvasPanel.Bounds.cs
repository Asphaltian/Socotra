namespace Socotra;

public partial class CanvasPanel
{
    private Rect? _navigationBounds;

    /// <summary>Keeps the view inside the left and right edges of <see cref="NavigationBounds"/>. On by default.</summary>
    public bool ConstrainHorizontalNavigation { get; set; } = true;

    /// <summary>Keeps the view inside the top and bottom edges of <see cref="NavigationBounds"/>. Turn it off to let the user pan and zoom freely up and down. On by default.</summary>
    public bool ConstrainVerticalNavigation { get; set; } = true;

    /// <summary>A rectangle in canvas coordinates the view can't pan or zoom out of, or null for no limit.</summary>
    /// <exception cref="ArgumentException">The rectangle isn't finite or doesn't have a positive size.</exception>
    public Rect? NavigationBounds
    {
        get => _navigationBounds;
        set
        {
            if (value is { } rect && (!float.IsFinite(rect.Left) || !float.IsFinite(rect.Top) || !float.IsFinite(rect.Right) || !float.IsFinite(rect.Bottom)
                || !float.IsFinite(rect.Width) || !float.IsFinite(rect.Height) || rect.Width <= 0 || rect.Height <= 0))
            {
                throw new ArgumentException("Navigation bounds must be finite and have positive size.", nameof(value));
            }

            if (_navigationBounds != value)
            {
                _zoomSpan = null;
            }

            _navigationBounds = value;
            ConstrainView();
            UpdateContentTransform();
        }
    }

    /// <summary>
    /// Fits <paramref name="bounds"/>, in canvas coordinates, into view. <paramref name="padding"/> adds space on each
    /// side as a fraction of the size, and <paramref name="minimumPadding"/> is the least space to add, in canvas units.
    /// <see cref="NavigationBounds"/> still applies.
    /// </summary>
    /// <exception cref="ArgumentException">A value isn't finite, or a size or padding is negative.</exception>
    public void FitBounds(Rect bounds, Vector2 padding = default, Vector2 minimumPadding = default)
    {
        if (!float.IsFinite(bounds.Left) || !float.IsFinite(bounds.Top) || !float.IsFinite(bounds.Right) || !float.IsFinite(bounds.Bottom)
            || bounds.Width < 0 || bounds.Height < 0)
        {
            throw new ArgumentException("Fit bounds must be finite and have non-negative size.", nameof(bounds));
        }

        if (!Painter.IsFinite(padding) || padding.X < 0 || padding.Y < 0)
        {
            throw new ArgumentException("Padding must be finite and non-negative.", nameof(padding));
        }

        if (!Painter.IsFinite(minimumPadding) || minimumPadding.X < 0 || minimumPadding.Y < 0)
        {
            throw new ArgumentException("Minimum padding must be finite and non-negative.", nameof(minimumPadding));
        }

        var margin = Vector2.Max(bounds.Size * padding, minimumPadding);
        var center = bounds.Position + (bounds.Size * 0.5f);
        var minimumSpan = Vector2.Max(new Vector2(MinimumViewSpan), Vector2.Abs(center) * 0.000001f);
        var size = Vector2.Max(bounds.Size + (margin * 2), minimumSpan);
        size = Vector2.Min(size, new Vector2(MaximumViewSpan));
        SetView(center - (size * 0.5f), center + (size * 0.5f));
    }

    private void ConstrainView()
    {
        if (_navigationBounds is not { } bounds)
        {
            return;
        }

        var span = ViewMax - ViewMin;
        var center = ViewMin + (span * 0.5f);
        if (PreserveAspectRatio && Viewport.Width > 0 && Viewport.Height > 0)
        {
            var scale = Viewport.Size / span;
            span = Viewport.Size / Math.Min(scale.X, scale.Y);
            float horizontalLimit = ConstrainHorizontalNavigation ? bounds.Width / span.X : 1;
            float verticalLimit = ConstrainVerticalNavigation ? bounds.Height / span.Y : 1;
            span *= Math.Min(1, Math.Min(horizontalLimit, verticalLimit));
        }

        if (ConstrainHorizontalNavigation)
        {
            span.X = Math.Min(span.X, bounds.Width);
        }

        if (ConstrainVerticalNavigation)
        {
            span.Y = Math.Min(span.Y, bounds.Height);
        }

        var min = center - (span * 0.5f);
        if (ConstrainHorizontalNavigation)
        {
            min.X = (float)Math.Clamp((double)min.X, bounds.Left, Math.Max(bounds.Left, (double)bounds.Right - span.X));
        }

        if (ConstrainVerticalNavigation)
        {
            min.Y = (float)Math.Clamp((double)min.Y, bounds.Top, Math.Max(bounds.Top, (double)bounds.Bottom - span.Y));
        }

        ViewMin = min;
        ViewMax = min + span;
        if (ConstrainHorizontalNavigation)
        {
            ViewMax = ViewMax with { X = Math.Min(ViewMax.X, bounds.Right) };
        }

        if (ConstrainVerticalNavigation)
        {
            ViewMax = ViewMax with { Y = Math.Min(ViewMax.Y, bounds.Bottom) };
        }
    }
}
