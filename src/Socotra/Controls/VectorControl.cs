using System.Globalization;
using Microsoft.AspNetCore.Components;

namespace Socotra;

/// <summary>
/// Edits a <see cref="Vector2"/>, <see cref="Vector3"/> or <see cref="Vector4"/>: one <see cref="NumberEntry"/> per
/// component, each with a colored letter in front, X red, Y green, Z blue and W yellow. The letters can be dragged
/// sideways to change the numbers. When the user changes one, it sends <c>onchange</c>.
/// </summary>
/// <example>
/// <code>
/// // Edit a spawn point
/// var spawn = new VectorControl { Parent = row, Value = level.Spawn };
/// spawn.ValueChanged = value =&gt; level.Spawn = (Vector3)value;
/// </code>
/// </example>
[StyleSheet.Inline("vectorcontrol", Styles)]
public class VectorControl : Panel
{
    private const string Styles = """
        VectorControl
        {
            gap: 2px;
            flex-grow: 1;
            flex-shrink: 1;
            flex-basis: 0px;
            min-width: 0px;
            flex-direction: row;
            align-items: center;
        }

        VectorControl NumberEntry
        {
            flex-grow: 1;
            flex-shrink: 1;
            flex-basis: 0px;
            min-width: 0px;
            overflow: hidden;
        }

        VectorControl .prefix-label
        {
            font-weight: 600;
            margin-right: 6px;
            opacity: 0.9;
        }

        VectorControl .x .prefix-label { color: #FB5A5A; }
        VectorControl .y .prefix-label { color: #B0E24D; }
        VectorControl .z .prefix-label { color: #3273EB; }
        VectorControl .w .prefix-label { color: #E6DB74; }
        """;

    private readonly NumberEntry[] _entries;
    private object _value = Vector3.Zero;

    /// <summary>Makes a control showing <see cref="Vector3.Zero"/>.</summary>
    public VectorControl()
    {
        _entries = [.. "xyzw".Select(AddEntry)];
        Value = Vector3.Zero;
    }

    /// <summary>
    /// The vector being edited: a <see cref="Vector2"/>, <see cref="Vector3"/> or <see cref="Vector4"/>, which also decides
    /// how many entries show. The user's changes come back as the same type. Setting it doesn't call <see cref="ValueChanged"/>.
    /// </summary>
    /// <exception cref="ArgumentException">The value isn't a <see cref="Vector2"/>, <see cref="Vector3"/> or <see cref="Vector4"/>.</exception>
    [Parameter]
    public object Value
    {
        get => _value;
        set
        {
            float[] components = value switch
            {
                Vector2 v => [v.X, v.Y],
                Vector3 v => [v.X, v.Y, v.Z],
                Vector4 v => [v.X, v.Y, v.Z, v.W],
                _ => throw new ArgumentException("The value must be a Vector2, Vector3 or Vector4.", nameof(value)),
            };

            _value = value;
            for (int i = 0; i < _entries.Length; i++)
            {
                _entries[i].Style.Display = i < components.Length ? DisplayMode.Flex : DisplayMode.None;
                if (i < components.Length)
                {
                    _entries[i].Value = components[i].ToString(CultureInfo.InvariantCulture);
                }
            }
        }
    }

    /// <summary>Called with the new <see cref="Value"/> after the user changes a component.</summary>
    [Parameter]
    public Action<object>? ValueChanged { get; set; }

    private NumberEntry AddEntry(char axis)
    {
        var entry = AddChild<NumberEntry>(axis.ToString());
        entry.Prefix = char.ToUpperInvariant(axis).ToString();
        entry.OnTextEdited = _ => ReadEntries();
        return entry;
    }

    private void ReadEntries()
    {
        float Component(int i) => float.Parse(_entries[i].FixNumeric(), CultureInfo.InvariantCulture);

        _value = _value switch
        {
            Vector2 => new Vector2(Component(0), Component(1)),
            Vector3 => new Vector3(Component(0), Component(1), Component(2)),
            _ => new Vector4(Component(0), Component(1), Component(2), Component(3)),
        };
        ValueChanged?.Invoke(_value);
        CreateEvent("onchange", _value);
        CreateValueEvent("value", _value);
    }
}
