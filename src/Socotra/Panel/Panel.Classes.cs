namespace Socotra;

public partial class Panel
{
    private HashSet<string>? _classSet;
    private string? _classes;
    private Dictionary<string, Func<bool>>? _classBinds;

    /// <summary>The panel's CSS classes.</summary>
    public IEnumerable<string> Class => _classSet ?? Enumerable.Empty<string>();

    /// <summary>The panel's CSS classes as one string, separated by spaces. Setting it replaces them all.</summary>
    public string Classes
    {
        get => _classes ??= string.Join(' ', Class);
        set
        {
            _classes = null;
            if (_classSet is { Count: > 0 })
            {
                _classSet.Clear();
                StyleSelectorsChanged(true, true);
            }

            AddClass(value);
        }
    }

    internal int ClassHash
    {
        get
        {
            var hash = 0;
            foreach (var name in Class)
            {
                hash += StringComparer.OrdinalIgnoreCase.GetHashCode(name);
            }

            return hash;
        }
    }

    /// <summary>Adds one or more classes, separated by spaces.</summary>
    public void AddClass(string? classname)
    {
        foreach (var name in SplitClasses(classname))
        {
            _classSet ??= new(StringComparer.OrdinalIgnoreCase);
            if (_classSet.Add(name))
            {
                _classes = null;
                StyleSelectorsChanged(true, true);
            }
        }
    }

    /// <summary>Removes one or more classes, separated by spaces.</summary>
    public void RemoveClass(string? classname)
    {
        foreach (var name in SplitClasses(classname))
        {
            if (_classSet?.Remove(name) == true)
            {
                _classes = null;
                StyleSelectorsChanged(true, true);
            }
        }
    }

    /// <summary>Adds or removes classes.</summary>
    public void SetClass(string? classname, bool active)
    {
        if (active)
        {
            AddClass(classname);
        }
        else
        {
            RemoveClass(classname);
        }
    }

    /// <summary>Adds a class for <paramref name="seconds"/>, then takes it off again. Flashing the same class again restarts the time.</summary>
    public void FlashClass(string classname, float seconds)
    {
        if (string.IsNullOrWhiteSpace(classname))
        {
            return;
        }

        AddClass(classname);
        InvokeOnce($"FlashClass;{classname}", seconds, () => RemoveClass(classname));
    }

    /// <summary>Adds the class if it's missing, removes it if it's there.</summary>
    public void ToggleClass(string classname)
    {
        if (!string.IsNullOrWhiteSpace(classname))
        {
            SetClass(classname, !HasClass(classname));
        }
    }

    /// <summary>Whether the panel has this class.</summary>
    public bool HasClass(string classname) => !string.IsNullOrWhiteSpace(classname) && (_classSet?.Contains(classname) ?? false);

    bool IStyleTarget.HasClasses(string[] classes) => _classSet is not null && classes.All(_classSet.Contains);

    /// <summary>Keeps a class on while <paramref name="func"/> returns true, checking every frame.</summary>
    public void BindClass(string className, Func<bool> func)
    {
        _classBinds ??= [];
        _classBinds[className] = func;
    }

    private void RunClassBinds()
    {
        foreach (var (className, condition) in _classBinds ?? [])
        {
            SetClass(className, condition());
        }
    }

    private static IEnumerable<string> SplitClasses(string? classes) =>
        string.IsNullOrWhiteSpace(classes) ? [] : classes.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(c => c.ToLowerInvariant());
}
