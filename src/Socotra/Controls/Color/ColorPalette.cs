using System.Text.Json;

namespace Socotra;

/// <summary>
/// The colors a <see cref="ColorPickerControl"/> lists under Recent and Saved. Every picker uses <see cref="Shared"/> unless
/// you give it a palette of its own through <see cref="ColorPickerControl.Palette"/>. Recent colors last while your program
/// runs. To keep the saved ones between runs, store what <see cref="Save"/> gives you and hand it to <see cref="Load"/> next time.
/// </summary>
/// <example>
/// <code>
/// // Keep the user's saved colors in a file between runs
/// var palette = ColorPalette.Shared;
/// if (File.Exists(palettePath))
/// {
///     palette.Load(File.ReadAllText(palettePath));
/// }
///
/// palette.Changed += () => File.WriteAllText(palettePath, palette.Save());
/// </code>
/// </example>
public sealed class ColorPalette
{
    private const int RecentLimit = 12;

    private static readonly string[] DefaultSaved =
    [
        "#ffffff", "#000000", "#00000000", "#d0d3d8", "#7d828c", "#3a3d44",
        "#3273eb", "#5cc8ff", "#4ecdc4", "#7ed491", "#f0b34c", "#e8632b", "#ff7b7b", "#b07cd8", "#8b5a2b",
    ];

    private readonly Lock _gate = new();
    private readonly List<PickerColor> _recent = [];
    private readonly List<PickerColor> _saved = Parse(DefaultSaved);

    /// <summary>The palette every color picker uses unless you give it another.</summary>
    public static ColorPalette Shared { get; } = new();

    /// <summary>Called after the saved colors change.</summary>
    public event Action? Changed;

    /// <summary>The saved colors, written the way the picker's text box shows them.</summary>
    public IReadOnlyList<string> Saved => [.. SavedColors.Select(color => color.ToText())];

    internal int Version { get; private set; }

    internal IReadOnlyList<PickerColor> RecentColors
    {
        get
        {
            lock (_gate)
            {
                return [.. _recent];
            }
        }
    }

    internal IReadOnlyList<PickerColor> SavedColors
    {
        get
        {
            lock (_gate)
            {
                return [.. _saved];
            }
        }
    }

    /// <summary>The saved colors as JSON, for you to store and give back to <see cref="Load"/> later.</summary>
    public string Save() => JsonSerializer.Serialize(Saved);

    /// <summary>Replaces the saved colors with ones from JSON that <see cref="Save"/> gave you. Entries that aren't colors are skipped.</summary>
    /// <exception cref="JsonException"><paramref name="json"/> isn't a list of strings.</exception>
    public void Load(string json)
    {
        var colors = Parse(JsonSerializer.Deserialize<List<string>>(json) ?? throw new JsonException("A palette must be a list of colors."));
        ChangeSaved(saved =>
        {
            saved.Clear();
            saved.AddRange(colors);
            return true;
        });
    }

    internal void AddRecent(PickerColor color)
    {
        var text = color.ToText();
        lock (_gate)
        {
            _recent.RemoveAll(x => x.ToText() == text);
            _recent.Insert(0, color);
            if (_recent.Count > RecentLimit)
            {
                _recent.RemoveRange(RecentLimit, _recent.Count - RecentLimit);
            }

            Version++;
        }
    }

    internal void AddSaved(PickerColor color)
    {
        var text = color.ToText();
        ChangeSaved(saved =>
        {
            if (saved.Any(x => x.ToText() == text))
            {
                return false;
            }

            saved.Add(color);
            return true;
        });
    }

    internal void ReplaceSaved(int index, PickerColor color) => ChangeSaved(saved =>
    {
        if (index < 0 || index >= saved.Count)
        {
            return false;
        }

        saved[index] = color;
        return true;
    });

    internal void RemoveSaved(int index) => ChangeSaved(saved =>
    {
        if (index < 0 || index >= saved.Count)
        {
            return false;
        }

        saved.RemoveAt(index);
        return true;
    });

    internal void ClearSaved() => ChangeSaved(saved =>
    {
        saved.Clear();
        return true;
    });

    private static List<PickerColor> Parse(IEnumerable<string> texts)
    {
        var colors = new List<PickerColor>();
        foreach (var text in texts)
        {
            if (PickerColor.TryParse(text, 0, out var color))
            {
                colors.Add(color);
            }
        }

        return colors;
    }

    private void ChangeSaved(Func<List<PickerColor>, bool> change)
    {
        lock (_gate)
        {
            if (!change(_saved))
            {
                return;
            }

            Version++;
        }

        Changed?.Invoke();
    }
}
