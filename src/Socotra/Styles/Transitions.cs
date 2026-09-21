namespace Socotra;

/// <summary>The transitions playing on a panel right now, which you get from <see cref="Panel.Transitions"/>.</summary>
public sealed class Transitions
{
    private readonly Panel _panel;
    private readonly List<Entry> _entries = [];

    internal Transitions(Panel panel)
    {
        _panel = panel;
    }

    /// <summary>Whether any transitions are playing.</summary>
    public bool HasAny => _entries.Count > 0;

    internal void Add(Styles from, Styles to, double startTime)
    {
        if (to.Transitions is not { List.Count: > 0 } transitions)
        {
            return;
        }

        var fromCopy = new Styles();
        fromCopy.From(from);
        var toCopy = new Styles();
        toCopy.From(to);
        foreach (var transition in transitions.List)
        {
            var target = HashCode.Combine(to.GetHashCode(), transition.Property);
            var existing = _entries.FindLastIndex(e => e.Target == target);
            if (existing >= 0)
            {
                _entries[existing] = _entries[existing] with { IsKilled = false };
                continue;
            }

            var length = (transition.Duration ?? 1000) / 1000.0;
            var delay = (transition.Delay ?? 0) / 1000.0;
            if (length <= 0 && delay <= 0)
            {
                continue;
            }

            _entries.Add(new Entry(transition.Property, startTime + delay, length, target, Easing.GetFunction(transition.TimingFunction), fromCopy, toCopy));
        }
    }

    internal void Kill(Styles from)
    {
        if (from.Transitions is not { List.Count: > 0 } transitions)
        {
            return;
        }

        for (int i = 0; i < _entries.Count; i++)
        {
            if (transitions.List.Any(t => t.Property == _entries[i].Property))
            {
                _entries[i] = _entries[i] with { IsKilled = true };
            }
        }
    }

    internal void Kill()
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            _entries[i] = _entries[i] with { IsKilled = true };
        }
    }

    internal void Clear() => _entries.Clear();

    internal bool Run(Styles style, double now)
    {
        if (_entries.Count == 0)
        {
            return false;
        }

        if (_entries.RemoveAll(e => e.StartTime + e.Length < now) > 0)
        {
            _panel.SetNeedsPreLayout();
        }

        foreach (var entry in _entries)
        {
            var progress = entry.IsKilled ? 1 : now < entry.StartTime ? 0 : entry.Length <= 0 ? 1 : (float)((now - entry.StartTime) / entry.Length);
            var delta = entry.EasingFunction(progress);
            if (entry.Property == "all")
            {
                style.FromLerp(entry.From, entry.To, delta);
            }
            else
            {
                style.LerpProperty(entry.Property, entry.From, entry.To, delta);
            }
        }

        if (_entries.RemoveAll(e => e.IsKilled) > 0)
        {
            _panel.SetNeedsPreLayout();
        }

        return true;
    }

    private readonly record struct Entry(string Property, double StartTime, double Length, int Target, Easing.Function EasingFunction, Styles From, Styles To, bool IsKilled = false);
}
