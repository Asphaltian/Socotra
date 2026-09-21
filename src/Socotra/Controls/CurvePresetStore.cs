using System.Text.Json;

namespace Socotra;

/// <summary>
/// The curves the user has saved as presets in a <see cref="CurveEditor"/>. Every curve editor uses <see cref="Shared"/>
/// unless you give it a store of its own through <see cref="CurveEditor.PresetStore"/>. Presets are kept while your program
/// runs; to keep them between runs, store what <see cref="Save"/> gives you and hand it to <see cref="Load"/> next time.
/// </summary>
/// <example>
/// <code>
/// // Keep the user's curve presets in a file between runs
/// var presets = CurvePresetStore.Shared;
/// if (File.Exists(presetPath))
/// {
///     presets.Load(File.ReadAllText(presetPath));
/// }
///
/// presets.Changed += () => File.WriteAllText(presetPath, presets.Save());
/// </code>
/// </example>
public sealed class CurvePresetStore
{
    private readonly Lock _gate = new();
    private readonly List<Curve> _presets = [];

    /// <summary>The store every curve editor uses unless you give it another.</summary>
    public static CurvePresetStore Shared { get; } = new();

    /// <summary>Called after a preset is added, replaced or removed, or presets are loaded.</summary>
    public event Action? Changed;

    /// <summary>The saved presets, oldest first.</summary>
    public IReadOnlyList<Curve> Presets
    {
        get
        {
            lock (_gate)
            {
                return [.. _presets];
            }
        }
    }

    internal int Version { get; private set; }

    /// <summary>Saves <paramref name="curve"/> as a new preset.</summary>
    public void Add(Curve curve) => Change(presets =>
    {
        presets.Add(curve);
        return true;
    });

    /// <summary>Replaces preset <paramref name="index"/> with <paramref name="curve"/>. Does nothing if there's no such preset.</summary>
    public void Replace(int index, Curve curve) => Change(presets =>
    {
        if (index < 0 || index >= presets.Count)
        {
            return false;
        }

        presets[index] = curve;
        return true;
    });

    /// <summary>Removes preset <paramref name="index"/>. Does nothing if there's no such preset.</summary>
    public void Remove(int index) => Change(presets =>
    {
        if (index < 0 || index >= presets.Count)
        {
            return false;
        }

        presets.RemoveAt(index);
        return true;
    });

    /// <summary>The presets as JSON, for you to store and give back to <see cref="Load"/> later.</summary>
    public string Save() => JsonSerializer.Serialize(Presets);

    /// <summary>Replaces the presets with ones from JSON that <see cref="Save"/> gave you.</summary>
    /// <exception cref="JsonException"><paramref name="json"/> isn't a list of curves.</exception>
    public void Load(string json)
    {
        var curves = JsonSerializer.Deserialize<List<Curve>>(json) ?? throw new JsonException("Curve presets must be a list of curves.");
        Change(presets =>
        {
            presets.Clear();
            presets.AddRange(curves);
            return true;
        });
    }

    private void Change(Func<List<Curve>, bool> change)
    {
        lock (_gate)
        {
            if (!change(_presets))
            {
                return;
            }

            Version++;
        }

        Changed?.Invoke();
    }
}
