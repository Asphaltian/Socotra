namespace Socotra;

public partial class FloatingWindow
{
    private const float ThrowSpeed = 900;
    private const float MaximumThrowSpeed = 3000;
    private const float StopSpeed = 25;
    private const float Friction = 3;
    private const float BounceRetention = 0.65f;
    private const double VelocityHistory = 0.1;
    private const double ReleaseHistory = 0.035;

    private readonly DragSample[] _dragSamples = new DragSample[64];
    private int _dragSampleCount;
    private int _nextDragSample;
    private Vector2 _throwVelocity;

    /// <summary>Returns <c>true</c> while the window is still sliding after the user flicked it and let go.</summary>
    public bool IsThrowing => _throwVelocity.LengthSquared() > 0;

    private void BeginDragMotion(Vector2 delta)
    {
        StopThrow();
        _dragSampleCount = 0;
        _nextDragSample = 0;
        SampleDragMotion(delta);
    }

    private void SampleDragMotion(Vector2 delta)
    {
        var sample = new DragSample(delta * ScaleFromScreen, TimeNow);
        var lastIndex = (_nextDragSample + _dragSamples.Length - 1) % _dragSamples.Length;
        if (_dragSampleCount > 0 && _dragSamples[lastIndex].Time == sample.Time)
        {
            _dragSamples[lastIndex] = sample;
            return;
        }

        _dragSamples[_nextDragSample] = sample;
        _nextDragSample = (_nextDragSample + 1) % _dragSamples.Length;
        _dragSampleCount = Math.Min(_dragSampleCount + 1, _dragSamples.Length);
    }

    private void StartThrow()
    {
        if (_dragSampleCount < 2)
        {
            return;
        }

        var last = _dragSamples[(_nextDragSample + _dragSamples.Length - 1) % _dragSamples.Length];
        var start = last;
        var recent = last;
        for (var i = 1; i < _dragSampleCount; i++)
        {
            var sample = _dragSamples[(_nextDragSample + _dragSamples.Length - 1 - i) % _dragSamples.Length];
            var age = last.Time - sample.Time;
            if (age > VelocityHistory)
            {
                break;
            }

            start = sample;
            if (age <= ReleaseHistory)
            {
                recent = sample;
            }
        }

        var duration = (float)(last.Time - start.Time);
        var recentDuration = (float)(last.Time - recent.Time);
        if (duration < 0.025f || recentDuration < 0.01f)
        {
            return;
        }

        var distance = last.Position - start.Position;
        var velocity = distance / duration;
        var releaseVelocity = (last.Position - recent.Position) / recentDuration;
        if (distance.Length() < 35 || velocity.Length() < ThrowSpeed || releaseVelocity.Length() < ThrowSpeed)
        {
            return;
        }

        if (Vector2.Dot(Vector2.Normalize(velocity), Vector2.Normalize(releaseVelocity)) < 0.8f)
        {
            return;
        }

        _throwVelocity = Vector2.Lerp(velocity, releaseVelocity, 0.7f);
        if (_throwVelocity.Length() > MaximumThrowSpeed)
        {
            _throwVelocity = Vector2.Normalize(_throwVelocity) * MaximumThrowSpeed;
        }
    }

    private void StopThrow() => _throwVelocity = Vector2.Zero;

    private void TickThrow()
    {
        if (!IsThrowing)
        {
            return;
        }

        if (!IsVisible || IsDragging || IsMinimized || IsAnimating || FindRootPanel() is not { } root)
        {
            StopThrow();
            return;
        }

        var location = _position ?? Box.Rect.Position;
        var bounds = root.Box.Rect;
        var right = Math.Max(bounds.Left, bounds.Right - Box.Rect.Width);
        var bottom = Math.Max(bounds.Top, bounds.Bottom - Box.Rect.Height);
        var remaining = Math.Min(TimeDelta, 0.05f);
        while (remaining > 0)
        {
            var step = Math.Min(remaining, 1f / 120);
            remaining -= step;
            var damping = MathF.Exp(-Friction * step);
            location += _throwVelocity * ((1 - damping) / Friction) * ScaleToScreen;
            _throwVelocity *= damping;
            if (KeepOnScreen)
            {
                (location.X, _throwVelocity.X) = BounceAxis(location.X, _throwVelocity.X, bounds.Left, right);
                (location.Y, _throwVelocity.Y) = BounceAxis(location.Y, _throwVelocity.Y, bounds.Top, bottom);
            }
        }

        _position = location;
        SetNeedsFinalLayout();
        if (_throwVelocity.Length() < StopSpeed)
        {
            StopThrow();
        }
    }

    private static (float Position, float Velocity) BounceAxis(float location, float velocity, float minimum, float maximum)
    {
        if (maximum <= minimum)
        {
            return (minimum, 0);
        }

        if (location < minimum)
        {
            return (minimum, velocity < 0 ? -velocity * BounceRetention : velocity);
        }

        if (location > maximum)
        {
            return (maximum, velocity > 0 ? -velocity * BounceRetention : velocity);
        }

        return (location, velocity);
    }

    private readonly record struct DragSample(Vector2 Position, double Time);
}
