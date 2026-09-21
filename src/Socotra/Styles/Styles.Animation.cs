namespace Socotra;

public partial class Styles
{
    private KeyFrames? _currentFrames;
    private double _animationStart;
    private Styles? _animationStyle;
    private bool _animationComplete;

    /// <summary>Whether an animation is set with <see cref="AnimationName"/>.</summary>
    public bool HasAnimation => !string.IsNullOrWhiteSpace(_animationName) && _animationName != "none";

    /// <summary>Whether an animation is set and hasn't finished yet. An animation that repeats forever never finishes.</summary>
    public bool IsAnimationActive => HasAnimation && !_animationComplete;

    /// <summary>Stops the animation. If <see cref="AnimationName"/> is still set, it plays again from the beginning.</summary>
    public void ResetAnimation()
    {
        _currentFrames = null;
        _animationStart = 0;
        _animationStyle = null;
        _animationComplete = false;
    }

    /// <summary>
    /// Plays the <c>@keyframes</c> rule called <paramref name="name"/> for <paramref name="duration"/> seconds, replacing any animation
    /// that's playing. The other parameters take the same values as the matching <c>animation-</c> properties.
    /// </summary>
    /// <example><code>
    /// // Shake twice, easing out, over half a second each time
    /// panel.Style.StartAnimation("shake", 0.5f, iterations: 2, timing: "ease-out");
    /// </code></example>
    public void StartAnimation(string name, float duration, int iterations = 1, float delay = 0, string timing = "linear", string direction = "normal", string fillMode = "none")
    {
        ResetAnimation();
        _animationName = name;
        _animationDuration = duration;
        _animationDelay = delay;
        _animationIterationCount = iterations;
        _animationDirection = direction;
        _animationFillMode = fillMode;
        _animationTimingFunction = timing;
        Dirty();
    }

    internal bool ApplyAnimation(Panel panel)
    {
        if (!HasAnimation || !panel.TryFindKeyframe(_animationName!, out var keyframes))
        {
            _currentFrames = null;
            return false;
        }

        if (_currentFrames != keyframes)
        {
            _currentFrames = keyframes;
            _animationStart = panel.TimeNow;
            _animationStyle = new Styles();
            _animationComplete = false;
        }

        if (_animationPlayState == "paused")
        {
            _animationStart += panel.TimeDelta;
        }

        var duration = _animationDuration ?? 1.0f;
        if (duration <= 0)
        {
            return false;
        }

        var iterations = _animationIterationCount ?? float.PositiveInfinity;
        var total = iterations * duration;
        var played = (float)(panel.TimeNow - _animationStart - (_animationDelay ?? 0));
        var fillMode = _animationFillMode ?? "none";
        if (played < 0)
        {
            if (fillMode is "backwards" or "both")
            {
                keyframes.FillStyle(0, _animationStyle!);
                Add(_animationStyle!);
            }

            return false;
        }

        if (!float.IsInfinity(iterations))
        {
            played = Math.Clamp(played, 0, total);
        }

        var delta = (played % duration) / duration;
        switch (_animationDirection ?? "normal")
        {
            case "reverse":
                delta = 1 - delta;
                break;
            case "alternate" or "alternate-reverse":
                delta = (played % (duration * 2)) / duration;
                if (delta > 1)
                {
                    delta = 1 - (delta - 1);
                }

                if (_animationDirection == "alternate-reverse")
                {
                    delta = 1 - delta;
                }

                break;
        }

        delta = Easing.GetFunction(_animationTimingFunction ?? "linear")(delta);
        if (played >= total)
        {
            if (fillMode is "forwards" or "both")
            {
                keyframes.FillStyle(1, _animationStyle!);
                Add(_animationStyle!);
            }

            var firstCompletion = !_animationComplete;
            _animationComplete = true;
            return firstCompletion;
        }

        keyframes.FillStyle(delta, _animationStyle!);
        Add(_animationStyle!);
        return true;
    }
}
