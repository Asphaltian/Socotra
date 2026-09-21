using System.Text;
using System.Text.Json;

namespace Socotra;

/// <summary>Where a panel goes when it's docked, next to another panel or the whole <see cref="DockHost"/>.</summary>
public enum DockPosition
{
    /// <summary>In the same tabs as the other panel.</summary>
    Center,

    /// <summary>In a new area to the left.</summary>
    Left,

    /// <summary>In a new area to the right.</summary>
    Right,

    /// <summary>In a new area above.</summary>
    Top,

    /// <summary>In a new area below.</summary>
    Bottom,
}

internal abstract class DockNode
{
}

internal sealed class DockGroup : DockNode
{
    public List<string> Items { get; } = [];

    public string? Selected { get; set; }
}

internal sealed class DockSplit(DockNode first, DockNode second, bool vertical, float fraction) : DockNode
{
    public DockNode First { get; set; } = first;

    public DockNode Second { get; set; } = second;

    public bool Vertical { get; } = vertical;

    public float Fraction { get; set; } = fraction;
}

internal sealed class DockLayout
{
    private const int MaxDepth = 64;

    public event Action? Changed;

    public DockNode? Root { get; private set; }

    public void Dock(string id, string? relativeTo = null, DockPosition position = DockPosition.Center, float fraction = 0.5f, int tabIndex = -1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        for (var i = 0; i < id.Length; i++)
        {
            if (!char.IsSurrogate(id[i]))
            {
                continue;
            }

            if (!char.IsSurrogatePair(id, i))
            {
                throw new ArgumentException("Panel IDs must contain valid Unicode.", nameof(id));
            }

            i++;
        }

        if (!Enum.IsDefined(position))
        {
            throw new ArgumentOutOfRangeException(nameof(position));
        }

        ValidateFraction(fraction);

        var source = FindGroup(id);
        var target = relativeTo is null ? null : FindGroup(relativeTo);
        if (relativeTo is not null && target is null)
        {
            throw new ArgumentException("The target panel is not docked.", nameof(relativeTo));
        }

        if (position == DockPosition.Center && target is null)
        {
            var first = Root;
            while (first is DockSplit split)
            {
                first = split.First;
            }

            target = first as DockGroup;
        }

        var count = position == DockPosition.Center ? (target?.Items.Count ?? 0) - (source is not null && source == target ? 1 : 0) : 0;
        if (tabIndex < -1 || tabIndex > count)
        {
            throw new ArgumentOutOfRangeException(nameof(tabIndex));
        }

        if (position == DockPosition.Center && source is not null && source == target)
        {
            var oldIndex = source.Items.IndexOf(id);
            if (tabIndex == -1 || tabIndex == oldIndex)
            {
                Activate(id);
                return;
            }

            source.Items.RemoveAt(oldIndex);
            source.Items.Insert(tabIndex, id);
            source.Selected = id;
            Changed?.Invoke();
            return;
        }

        if (position != DockPosition.Center)
        {
            if (source is not null && source.Items.Count == 1 && (source == target || (relativeTo is null && source == Root)))
            {
                return;
            }

            var removed = source?.Items.Count == 1 ? source : null;
            var depth = GetDepth(Root, removed, target);
            if (target is null)
            {
                depth++;
            }

            if (depth > MaxDepth)
            {
                throw new InvalidOperationException("The docking layout is too deep.");
            }
        }

        if (source is not null)
        {
            Remove(source, id);
        }

        if (position == DockPosition.Center)
        {
            if (target is null)
            {
                Root = target = new DockGroup();
            }

            target.Items.Insert(tabIndex == -1 ? target.Items.Count : tabIndex, id);
            target.Selected = id;
        }
        else
        {
            var incoming = new DockGroup { Selected = id };
            incoming.Items.Add(id);
            DockNode? region = target ?? Root;
            if (region is null)
            {
                Root = incoming;
            }
            else
            {
                var before = position is DockPosition.Left or DockPosition.Top;
                var split = new DockSplit(before ? incoming : region, before ? region : incoming, position is DockPosition.Top or DockPosition.Bottom, before ? fraction : 1 - fraction);
                Root = Replace(Root, region, split);
            }
        }

        Changed?.Invoke();
    }

    public bool Close(string id)
    {
        if (FindGroup(id) is not { } group)
        {
            return false;
        }

        Remove(group, id);
        Changed?.Invoke();
        return true;
    }

    public bool Activate(string id)
    {
        if (FindGroup(id) is not { } group || group.Selected == id)
        {
            return false;
        }

        group.Selected = id;
        Changed?.Invoke();
        return true;
    }

    public DockGroup? FindGroup(string id) => FindGroup(Root, id);

    public void SetFraction(DockSplit split, float fraction)
    {
        ValidateFraction(fraction);
        if (!Contains(Root, split))
        {
            throw new ArgumentException("The split does not belong to this layout.", nameof(split));
        }

        if (split.Fraction == fraction)
        {
            return;
        }

        split.Fraction = fraction;
        Changed?.Invoke();
    }

    public string Save()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { MaxDepth = MaxDepth + 4 }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("version", 1);
            writer.WritePropertyName("root");
            WriteNode(writer, Root);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public bool Restore(string? json, IEnumerable<string>? knownIds)
    {
        if (json is null || knownIds is null)
        {
            return false;
        }

        var known = new HashSet<string>(knownIds, StringComparer.Ordinal);
        DockNode? root;
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = MaxDepth + 4 });
            var data = document.RootElement;
            RequireProperties(data, "version", "root");
            var version = data.GetProperty("version");
            if (version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var number) || number != 1)
            {
                return false;
            }

            var node = data.GetProperty("root");
            root = node.ValueKind == JsonValueKind.Null ? null : ReadNode(node, known, new HashSet<string>(StringComparer.Ordinal), 1);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or ArgumentException)
        {
            return false;
        }

        Root = root;
        Changed?.Invoke();
        return true;
    }

    private static DockGroup? FindGroup(DockNode? node, string id) => node switch
    {
        DockGroup group => group.Items.Contains(id) ? group : null,
        DockSplit split => FindGroup(split.First, id) ?? FindGroup(split.Second, id),
        _ => null,
    };

    private static bool Contains(DockNode? node, DockSplit target) =>
        node == target || (node is DockSplit split && (Contains(split.First, target) || Contains(split.Second, target)));

    private static void ValidateFraction(float fraction)
    {
        if (!float.IsFinite(fraction) || fraction < 0.05f || fraction > 0.95f)
        {
            throw new ArgumentOutOfRangeException(nameof(fraction), "Fraction must be between 0.05 and 0.95.");
        }
    }

    private void Remove(DockGroup group, string id)
    {
        var index = group.Items.IndexOf(id);
        group.Items.RemoveAt(index);
        if (group.Items.Count == 0)
        {
            group.Selected = null;
            Root = Replace(Root, group, null);
        }
        else if (group.Selected == id)
        {
            group.Selected = group.Items[Math.Min(index, group.Items.Count - 1)];
        }
    }

    private static DockNode? Replace(DockNode? node, DockNode target, DockNode? replacement)
    {
        if (node == target)
        {
            return replacement;
        }

        if (node is not DockSplit split)
        {
            return node;
        }

        var first = Replace(split.First, target, replacement);
        var second = Replace(split.Second, target, replacement);
        if (first is null)
        {
            return second;
        }

        if (second is null)
        {
            return first;
        }

        split.First = first;
        split.Second = second;
        return split;
    }

    private static int GetDepth(DockNode? node, DockNode? removed, DockNode? wrapped)
    {
        if (node is null || node == removed)
        {
            return 0;
        }

        var depth = 1;
        if (node is DockSplit split)
        {
            var first = GetDepth(split.First, removed, wrapped);
            var second = GetDepth(split.Second, removed, wrapped);
            depth = first == 0 ? second : second == 0 ? first : 1 + Math.Max(first, second);
        }

        return node == wrapped ? depth + 1 : depth;
    }

    private static void WriteNode(Utf8JsonWriter writer, DockNode? node)
    {
        if (node is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();
        if (node is DockGroup group)
        {
            writer.WriteString("type", "group");
            writer.WriteStartArray("tabs");
            foreach (var id in group.Items)
            {
                writer.WriteStringValue(id);
            }

            writer.WriteEndArray();
            writer.WriteString("activeId", group.Selected);
        }
        else if (node is DockSplit split)
        {
            writer.WriteString("type", "split");
            writer.WriteBoolean("vertical", split.Vertical);
            writer.WriteNumber("fraction", split.Fraction);
            writer.WritePropertyName("first");
            WriteNode(writer, split.First);
            writer.WritePropertyName("second");
            WriteNode(writer, split.Second);
        }

        writer.WriteEndObject();
    }

    private static DockNode ReadNode(JsonElement data, HashSet<string> known, HashSet<string> used, int depth)
    {
        if (depth > MaxDepth || data.ValueKind != JsonValueKind.Object || !data.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String)
        {
            throw new JsonException();
        }

        if (type.GetString() == "group")
        {
            RequireProperties(data, "type", "tabs", "activeId");
            var tabs = data.GetProperty("tabs");
            var active = data.GetProperty("activeId");
            if (tabs.ValueKind != JsonValueKind.Array || active.ValueKind != JsonValueKind.String)
            {
                throw new JsonException();
            }

            var group = new DockGroup { Selected = active.GetString() };
            foreach (var tab in tabs.EnumerateArray())
            {
                var id = tab.ValueKind == JsonValueKind.String ? tab.GetString() : null;
                if (string.IsNullOrWhiteSpace(id) || !known.Contains(id) || !used.Add(id))
                {
                    throw new JsonException();
                }

                group.Items.Add(id);
            }

            if (group.Selected is null || !group.Items.Contains(group.Selected))
            {
                throw new JsonException();
            }

            return group;
        }

        if (type.GetString() != "split")
        {
            throw new JsonException();
        }

        RequireProperties(data, "type", "vertical", "fraction", "first", "second");
        var vertical = data.GetProperty("vertical");
        var fraction = data.GetProperty("fraction");
        if (vertical.ValueKind is not (JsonValueKind.True or JsonValueKind.False)
            || fraction.ValueKind != JsonValueKind.Number || !fraction.TryGetSingle(out var value)
            || !float.IsFinite(value) || value < 0.05f || value > 0.95f)
        {
            throw new JsonException();
        }

        return new DockSplit(ReadNode(data.GetProperty("first"), known, used, depth + 1), ReadNode(data.GetProperty("second"), known, used, depth + 1), vertical.GetBoolean(), value);
    }

    private static void RequireProperties(JsonElement data, params string[] names)
    {
        if (data.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException();
        }

        var remaining = new HashSet<string>(names, StringComparer.Ordinal);
        foreach (var property in data.EnumerateObject())
        {
            if (!remaining.Remove(property.Name))
            {
                throw new JsonException();
            }
        }

        if (remaining.Count != 0)
        {
            throw new JsonException();
        }
    }
}
