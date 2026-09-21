namespace Socotra;

/// <summary>One entry of <c>transition</c>: which property animates, how long it takes and how it eases.</summary>
public struct TransitionDesc
{
    /// <summary>The CSS property to animate, or <c>all</c>.</summary>
    public string Property { get; set; }

    /// <summary>How long the change takes, in milliseconds.</summary>
    public float? Duration { get; set; }

    /// <summary>How long it waits before starting, in milliseconds.</summary>
    public float? Delay { get; set; }

    /// <summary>The name of the <see cref="Easing"/> function to use, like <c>ease-out</c>.</summary>
    public string? TimingFunction { get; set; }

    internal static TransitionList? ParseProperty(string property, string value, TransitionList? list)
    {
        list ??= new TransitionList();
        if (property == "transition")
        {
            var p = new Parse(value);
            while (!(p = p.SkipWhitespaceAndNewlines()).IsEnd)
            {
                var entry = p.ReadUntilOrEnd(",", respectParens: true);
                p.Pointer++;
                if (string.IsNullOrWhiteSpace(entry))
                {
                    continue;
                }

                if (ParseEntry(entry) is not { } transition)
                {
                    return null;
                }

                list.Add(transition);
            }

            return list;
        }

        var items = new List<string>();
        var values = new Parse(value);
        while (!values.IsEnd)
        {
            items.Add(values.ReadUntilOrEnd(",", respectParens: true).Trim());
            values.Pointer++;
        }

        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].Length == 0)
            {
                continue;
            }

            while (list.List.Count <= i)
            {
                list.List.Add(new TransitionDesc { Property = "all", TimingFunction = "ease", Delay = 0, Duration = 0 });
            }

            var entry = list.List[i];
            var item = new Parse(items[i]);
            switch (property)
            {
                case "transition-property":
                    entry.Property = StyleParser.GetPropertyFromAlias(items[i].ToLowerInvariant());
                    break;
                case "transition-duration" when item.TryReadTime(out var duration):
                    entry.Duration = duration;
                    break;
                case "transition-delay" when item.TryReadTime(out var delay):
                    entry.Delay = delay;
                    break;
                case "transition-timing-function":
                    entry.TimingFunction = items[i];
                    break;
            }

            list.List[i] = entry;
        }

        return list;
    }

    private static TransitionDesc? ParseEntry(string value)
    {
        var transition = new TransitionDesc { Property = "all", Delay = 0, TimingFunction = "ease" };
        var p = new Parse(value).SkipWhitespaceAndNewlines();
        var probe = p;
        if (!probe.TryReadTime(out _))
        {
            transition.Property = StyleParser.GetPropertyFromAlias(p.ReadWord(null, readUntilEnd: true)!.ToLowerInvariant());
            if ((p = p.SkipWhitespaceAndNewlines()).IsEnd)
            {
                return transition;
            }
        }

        if (!p.TryReadTime(out var duration))
        {
            return null;
        }

        transition.Duration = duration;
        if ((p = p.SkipWhitespaceAndNewlines()).IsEnd)
        {
            return transition;
        }

        if (p.TryReadTime(out var delay))
        {
            transition.Delay = delay;
        }

        if ((p = p.SkipWhitespaceAndNewlines()).IsEnd)
        {
            return transition;
        }

        transition.TimingFunction = p.ReadWord(null, readUntilEnd: true, respectParens: true);
        if ((p = p.SkipWhitespaceAndNewlines()).IsEnd)
        {
            return transition;
        }

        if (p.TryReadTime(out delay))
        {
            transition.Delay = delay;
        }

        return transition;
    }
}
