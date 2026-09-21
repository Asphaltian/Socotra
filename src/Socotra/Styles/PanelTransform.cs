using System.Collections.Immutable;

namespace Socotra;

/// <summary>
/// A <c>transform</c> value: a list of functions like <c>translate()</c>, <c>rotate3d()</c> or <c>perspective()</c>,
/// applied in order. Build one in code with the <c>Add</c> methods and give it to <see cref="Styles.Transform"/>.
/// </summary>
/// <example><code>
/// // Nudge the panel up a little and tilt it
/// var transform = new PanelTransform();
/// transform.AddTranslateY(-4);
/// transform.AddRotation(0, 0, 5);
/// panel.Style.Transform = transform;
/// </code></example>
public struct PanelTransform : IEquatable<PanelTransform>
{
    private ImmutableArray<Entry> _entries;

    /// <summary>The kinds of transform function.</summary>
    public enum EntryType
    {
        /// <summary>Not a transform function.</summary>
        Invalid,

        /// <summary><c>rotate()</c>, <c>rotateX()</c>, <c>rotateY()</c>, <c>rotateZ()</c> or <c>rotate3d()</c>, in degrees around each axis.</summary>
        Rotation,

        /// <summary><c>scale()</c> and its variants.</summary>
        Scale,

        /// <summary><c>translate()</c> and its variants.</summary>
        Translate,

        /// <summary><c>skew()</c>, <c>skewX()</c> or <c>skewY()</c>, in degrees.</summary>
        Skew,

        /// <summary><c>matrix()</c> or <c>matrix3d()</c>.</summary>
        Matrix,

        /// <summary><c>perspective()</c>.</summary>
        Perspective,
    }

    /// <summary>Whether it has no functions, like <c>transform: none</c>.</summary>
    public readonly bool IsEmpty => _entries.IsDefaultOrEmpty;

    internal readonly ImmutableArray<Entry> Entries => _entries.IsDefault ? [] : _entries;

    /// <summary>
    /// The matrix these functions make for a box <paramref name="width"/> by <paramref name="height"/> pixels, without <c>transform-origin</c>.
    /// <paramref name="perspectiveOrigin"/> is where <c>perspective()</c> looks from, measured from the box's center.
    /// </summary>
    public readonly Matrix4x4 BuildTransform(float width, float height, Vector2 perspectiveOrigin)
    {
        var matrix = Matrix4x4.Identity;
        foreach (var entry in Entries)
        {
            var m = entry.Type == EntryType.Perspective
                ? Matrix4x4.CreateTranslation(new Vector3(perspectiveOrigin, 0)) * entry.ToMatrix(width, height) * Matrix4x4.CreateTranslation(new Vector3(-perspectiveOrigin, 0))
                : entry.ToMatrix(width, height);
            matrix = m * matrix;
        }

        return matrix;
    }

    /// <summary>Adds a <c>translate3d()</c>.</summary>
    public void AddTranslate(Length x, Length y, Length z = default) => Add(new Entry { Type = EntryType.Translate, X = x, Y = y, Z = z });

    /// <summary>Adds a <c>translateX()</c>.</summary>
    public void AddTranslateX(Length x) => AddTranslate(x, default);

    /// <summary>Adds a <c>translateY()</c>.</summary>
    public void AddTranslateY(Length y) => AddTranslate(default, y);

    /// <summary>Adds a <c>translateZ()</c>.</summary>
    public void AddTranslateZ(Length z) => AddTranslate(default, default, z);

    /// <summary>Adds a <c>scale()</c> that's the same on every axis.</summary>
    public void AddScale(float scale) => AddScale(new Vector3(scale));

    /// <summary>Adds a <c>scale3d()</c>.</summary>
    public void AddScale(Vector3 scale) => Add(new Entry { Type = EntryType.Scale, Data = scale });

    /// <summary>Adds a <c>skew()</c>, in degrees along each axis.</summary>
    public void AddSkew(float x, float y, float z) => Add(new Entry { Type = EntryType.Skew, Data = new Vector3(x, y, z) });

    /// <summary>Adds a rotation, in degrees around each axis.</summary>
    public void AddRotation(float x, float y, float z) => Add(new Entry { Type = EntryType.Rotation, Data = new Vector3(x, y, z) });

    /// <summary>Adds a rotation, in degrees around each axis.</summary>
    public void AddRotation(Vector3 angles) => AddRotation(angles.X, angles.Y, angles.Z);

    /// <summary>Adds a <c>matrix3d()</c>.</summary>
    public void AddMatrix3D(Matrix4x4 matrix) => Add(new Entry { Type = EntryType.Matrix, Matrix = matrix });

    /// <summary>Adds a <c>perspective()</c>: how far away you look at the panel from. Closer makes 3D rotations look deeper.</summary>
    public void AddPerspective(Length distance) => Add(new Entry { Type = EntryType.Perspective, X = distance });

    private void Add(Entry entry) => _entries = Entries.Add(entry);

    internal static PanelTransform? Parse(string value)
    {
        value = value.Trim();
        var transform = new PanelTransform();
        if (value.Length == 0 || value.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            return transform;
        }

        var p = new Parse(value);
        while (!(p = p.SkipWhitespaceAndNewlines()).IsEnd)
        {
            var name = p.ReadUntil("(")?.Trim().ToLowerInvariant();
            var arguments = p.ReadInnerBrackets();
            if (name is null || arguments is null || !transform.AddFunction(name, arguments))
            {
                return null;
            }
        }

        return transform;
    }

    private bool AddFunction(string name, string arguments)
    {
        switch (name)
        {
            case "rotate" or "rotatez" when ReadAngle(arguments) is { } z:
                AddRotation(0, 0, z);
                return true;
            case "rotatex" when ReadAngle(arguments) is { } x:
                AddRotation(x, 0, 0);
                return true;
            case "rotatey" when ReadAngle(arguments) is { } y:
                AddRotation(0, y, 0);
                return true;
            case "rotate3d" when ReadAngles(arguments) is { } angles:
                AddRotation(angles);
                return true;
            case "scale" or "scale3d" when ReadVector(arguments, name == "scale3d") is { } scale:
                AddScale(scale);
                return true;
            case "scalex" when ReadFloat(arguments) is { } x:
                AddScale(new Vector3(x, 1, 1));
                return true;
            case "scaley" when ReadFloat(arguments) is { } y:
                AddScale(new Vector3(1, y, 1));
                return true;
            case "scalez" when ReadFloat(arguments) is { } z:
                AddScale(new Vector3(1, 1, z));
                return true;
            case "skew":
                return ReadSkew(arguments);
            case "skewx" when ReadAngle(arguments) is { } x:
                AddSkew(x, 0, 0);
                return true;
            case "skewy" when ReadAngle(arguments) is { } y:
                AddSkew(0, y, 0);
                return true;
            case "translate" or "translate3d":
                return ReadTranslate(arguments, name == "translate3d");
            case "translatex":
                AddTranslateX(Length.Parse(arguments) ?? default);
                return true;
            case "translatey":
                AddTranslateY(Length.Parse(arguments) ?? default);
                return true;
            case "translatez":
                AddTranslateZ(Length.Parse(arguments) ?? default);
                return true;
            case "matrix" when ReadMatrix(arguments, 6) is { } m:
                AddMatrix3D(new Matrix4x4(m[0], m[1], 0, 0, m[2], m[3], 0, 0, 0, 0, 1, 0, m[4], m[5], 0, 1));
                return true;
            case "matrix3d" when ReadMatrix(arguments, 16) is { } m:
                AddMatrix3D(new Matrix4x4(m[0], m[1], m[2], m[3], m[4], m[5], m[6], m[7], m[8], m[9], m[10], m[11], m[12], m[13], m[14], m[15]));
                return true;
            case "perspective":
                AddPerspective(Length.Parse(arguments) ?? default);
                return true;
            default:
                return false;
        }
    }

    private bool ReadSkew(string arguments)
    {
        var p = new Parse(arguments);
        if (!p.TryReadFloat(out var x))
        {
            return false;
        }

        p.SkipWhitespaceAndNewlines();
        var skewX = RotationDegrees(x, p.ReadUntilWhitespaceOrNewlineOrEnd(","));
        p.SkipWhitespaceAndNewlines(",");
        var skewY = p.TryReadFloat(out var y) ? RotationDegrees(y, p.ReadRemaining().Trim()) : 0;
        AddSkew(skewX, skewY, 0);
        return true;
    }

    private bool ReadTranslate(string arguments, bool is3d)
    {
        var p = new Parse(arguments);
        if (!p.TryReadLength(out var x))
        {
            return false;
        }

        p.SkipWhitespaceAndNewlines(",");
        if (!p.TryReadLength(out var y))
        {
            AddTranslate(x, default);
            return true;
        }

        p.SkipWhitespaceAndNewlines(",");
        AddTranslate(x, y, is3d && p.TryReadLength(out var z) ? z : default);
        return true;
    }

    private static float? ReadFloat(string value)
    {
        var p = new Parse(value);
        return p.TryReadFloat(out var result) ? result : null;
    }

    private static float? ReadAngle(string value)
    {
        var p = new Parse(value);
        return p.TryReadFloat(out var result) ? RotationDegrees(result, p.ReadRemaining().Trim()) : null;
    }

    private static Vector3? ReadAngles(string value)
    {
        var p = new Parse(value);
        var angles = Vector3.Zero;
        for (int i = 0; i < 3; i++)
        {
            p.SkipWhitespaceAndNewlines(",");
            if (!p.TryReadFloat(out var component))
            {
                return null;
            }

            angles[i] = RotationDegrees(component, p.ReadUntilWhitespaceOrNewlineOrEnd(","));
        }

        return angles;
    }

    private static Vector3? ReadVector(string value, bool is3d)
    {
        var p = new Parse(value);
        if (!p.TryReadFloat(out var x))
        {
            return null;
        }

        var vector = new Vector3(x, x, 1);
        p.SkipWhitespaceAndNewlines(",");
        if (p.TryReadFloat(out var y))
        {
            vector.Y = y;
        }

        p.SkipWhitespaceAndNewlines(",");
        if (is3d && p.TryReadFloat(out var z))
        {
            vector.Z = z;
        }

        return vector;
    }

    private static float[]? ReadMatrix(string value, int count)
    {
        var p = new Parse(value);
        var values = new float[count];
        for (int i = 0; i < count; i++)
        {
            if (!p.TryReadFloat(out values[i]))
            {
                return null;
            }

            p.SkipWhitespaceAndNewlines(",");
        }

        return values;
    }

    internal static float RotationDegrees(float value, string unit) =>
        unit.StartsWith("grad", StringComparison.OrdinalIgnoreCase) ? value * 0.9f
        : unit.StartsWith("rad", StringComparison.OrdinalIgnoreCase) ? float.RadiansToDegrees(value)
        : unit.StartsWith("turn", StringComparison.OrdinalIgnoreCase) ? value * 360.0f
        : value;

    internal readonly PanelTransform Scaled(float scale) => new()
    {
        _entries = [.. Entries.Select(e => e with { X = e.X.Scaled(scale), Y = e.Y.Scaled(scale), Z = e.Z.Scaled(scale) })],
    };

    internal static PanelTransform Lerp(PanelTransform a, PanelTransform b, float delta)
    {
        var entries = ImmutableArray.CreateBuilder<Entry>();
        foreach (var from in a.Entries)
        {
            var to = b.Entries.FirstOrDefault(e => e.Type == from.Type, from.Identity);
            entries.Add(Entry.Lerp(from, to, delta));
        }

        foreach (var to in b.Entries)
        {
            if (!a.Entries.Any(e => e.Type == to.Type))
            {
                entries.Add(Entry.Lerp(to.Identity, to, delta));
            }
        }

        return new PanelTransform { _entries = entries.ToImmutable() };
    }

    /// <inheritdoc/>
    public readonly bool Equals(PanelTransform other) => Entries.SequenceEqual(other.Entries);

    /// <inheritdoc/>
    public override readonly bool Equals(object? obj) => obj is PanelTransform other && Equals(other);

    /// <inheritdoc/>
    public override readonly int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var entry in Entries)
        {
            hash.Add(entry);
        }

        return hash.ToHashCode();
    }

    /// <inheritdoc/>
    public static bool operator ==(PanelTransform left, PanelTransform right) => left.Equals(right);

    /// <inheritdoc/>
    public static bool operator !=(PanelTransform left, PanelTransform right) => !left.Equals(right);

    internal readonly record struct Entry
    {
        public EntryType Type { get; init; }

        public Vector3 Data { get; init; }

        public Matrix4x4 Matrix { get; init; }

        public Length X { get; init; }

        public Length Y { get; init; }

        public Length Z { get; init; }

        public Entry Identity => this with
        {
            Data = Type == EntryType.Scale ? Vector3.One : Vector3.Zero,
            Matrix = Matrix4x4.Identity,
            X = X with { Value = 0 },
            Y = Y with { Value = 0 },
            Z = Z with { Value = 0 },
        };

        public Matrix4x4 ToMatrix(float width, float height) => Type switch
        {
            EntryType.Rotation => Matrix4x4.CreateRotationX(float.DegreesToRadians(Data.X))
                * Matrix4x4.CreateRotationY(float.DegreesToRadians(Data.Y))
                * Matrix4x4.CreateRotationZ(float.DegreesToRadians(Data.Z)),
            EntryType.Scale => Matrix4x4.CreateScale(Data),
            EntryType.Translate => Matrix4x4.CreateTranslation(X.GetPixels(width), Y.GetPixels(height), Z.GetPixels(0)),
            EntryType.Skew => new Matrix4x4(
                1, MathF.Tan(float.DegreesToRadians(Data.Y)), 0, 0,
                MathF.Tan(float.DegreesToRadians(Data.X)), 1, 0, 0,
                0, 0, 1, 0,
                0, 0, 0, 1),
            EntryType.Matrix => Matrix,
            EntryType.Perspective => new Matrix4x4(
                1, 0, 0, 0,
                0, 1, 0, 0,
                0, 0, 1, -1 / MathF.Max(X.GetPixels(width), 1),
                0, 0, 0, 1),
            _ => Matrix4x4.Identity,
        };

        public static Entry Lerp(Entry a, Entry b, float delta) => a with
        {
            Data = Vector3.Lerp(a.Data, b.Data, delta),
            Matrix = Matrix4x4.Lerp(a.Matrix, b.Matrix, delta),
            X = Length.Lerp(a.X, b.X, delta),
            Y = Length.Lerp(a.Y, b.Y, delta),
            Z = Length.Lerp(a.Z, b.Z, delta),
        };
    }
}
