namespace Socotra;

/// <summary>The properties that animate smoothly when they change, as set by <c>transition</c> and its longhands.</summary>
public sealed class TransitionList
{
    /// <summary>How each property transitions, one entry per property.</summary>
    public List<TransitionDesc> List { get; } = [];

    /// <summary>Stops every property from transitioning.</summary>
    public void Clear() => List.Clear();

    internal void AddTransitions(TransitionList transitions)
    {
        foreach (var transition in transitions.List)
        {
            Add(transition);
        }
    }

    internal void Add(TransitionDesc transition)
    {
        var entry = List.FirstOrDefault(t => t.Property == transition.Property);
        entry.Property = transition.Property;
        entry.Duration = transition.Duration ?? entry.Duration;
        entry.Delay = transition.Delay ?? entry.Delay;
        entry.TimingFunction = transition.TimingFunction ?? entry.TimingFunction;
        List.RemoveAll(t => t.Property == transition.Property);
        List.Add(entry);
    }
}
