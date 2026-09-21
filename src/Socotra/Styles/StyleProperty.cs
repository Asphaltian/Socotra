namespace Socotra;

internal enum Scaling
{
    None,
    Ceiling,
    Exact,
}

internal abstract class StyleProperty(string name, bool inherited)
{
    public string Name => name;

    public bool Inherited => inherited;

    public virtual bool IsTransitionable => false;

    public abstract bool IsSet(Styles styles);

    public abstract bool Set(Styles styles, string value);

    public abstract void Add(Styles target, Styles source);

    public abstract void Copy(Styles target, Styles source);

    public abstract void Clear(Styles styles);

    public abstract void Inherit(Styles target, Styles parent);

    public abstract void FillDefault(Styles target);

    public abstract void SetInitial(Styles target);

    public abstract bool IsDefault(Styles styles);

    public abstract int GetHashCode(Styles styles);

    public virtual void Scale(Styles styles, float scale)
    {
    }

    public virtual void Lerp(Styles target, Styles from, Styles to, float delta)
    {
    }

    public virtual void ResolveCurrentColor(Styles styles, Color color)
    {
    }
}

internal delegate ref T StyleField<T>(Styles styles);

internal class StyleProperty<T>(string name, StyleField<T> accessor, Func<string, T>? parse, T initial, bool inherited = false)
    : StyleProperty(name, inherited)
{
    public bool FillsDefault { get; init; } = true;

    protected StyleField<T> Field => accessor;

    protected T Initial => initial;

    public override bool IsSet(Styles styles) => accessor(styles) is not null;

    public override bool Set(Styles styles, string value)
    {
        if (parse is null || parse(value) is not { } parsed)
        {
            return false;
        }

        accessor(styles) = parsed;
        return true;
    }

    public override void Add(Styles target, Styles source)
    {
        if (accessor(source) is { } value)
        {
            accessor(target) = value;
        }
    }

    public override void Copy(Styles target, Styles source) => accessor(target) = accessor(source);

    public override void Clear(Styles styles) => accessor(styles) = default!;

    public override void Inherit(Styles target, Styles parent)
    {
        if (accessor(target) is null)
        {
            accessor(target) = accessor(parent);
        }
    }

    public override void FillDefault(Styles target)
    {
        if (FillsDefault && accessor(target) is null)
        {
            accessor(target) = initial;
        }
    }

    public override void SetInitial(Styles target) => accessor(target) = initial;

    public override bool IsDefault(Styles styles) => EqualityComparer<T>.Default.Equals(accessor(styles), initial);

    public override int GetHashCode(Styles styles) => accessor(styles) is { } value ? value.GetHashCode() : 0;
}

internal class ValueProperty<T>(string name, StyleField<T?> field, Func<string, T?>? parse, T? initial, bool inherited = false)
    : StyleProperty<T?>(name, field, parse, initial, inherited)
    where T : struct
{
    public StyleField<T?>? LerpFallback { get; init; }

    protected virtual T LerpInitial => Initial ?? default;

    public override void Lerp(Styles target, Styles from, Styles to, float delta)
    {
        var a = Field(from) ?? LerpFallback?.Invoke(from);
        var b = Field(to) ?? LerpFallback?.Invoke(to);
        if (a is null && b is null)
        {
            return;
        }

        var start = a ?? LerpInitial;
        var end = b ?? (Inherited ? start : LerpInitial);
        Field(target) = EqualityComparer<T>.Default.Equals(start, end) ? start : Interpolate(start, end, delta);
    }

    protected virtual T Interpolate(T from, T to, float delta) => to;
}

internal sealed class NumberProperty(string name, StyleField<float?> field, float initial, bool inherited = false, Func<string, float?>? parse = null)
    : ValueProperty<float>(name, field, parse ?? Styles.ParseFloat, initial, inherited)
{
    public override bool IsTransitionable => true;

    protected override float Interpolate(float from, float to, float delta) => from + ((to - from) * delta);
}

internal sealed class IntegerProperty(string name, StyleField<int?> field, int initial, bool inherited = false, Func<string, int?>? parse = null)
    : ValueProperty<int>(name, field, parse ?? Styles.ParseInt, initial, inherited)
{
    public override bool IsTransitionable => true;

    protected override int Interpolate(int from, int to, float delta) => (int)(from + ((to - from) * delta));
}

internal sealed class LengthProperty(string name, StyleField<Length?> field, Length? initial, Scaling scaling = Scaling.None, bool inherited = false, Func<string, Length?>? parse = null)
    : ValueProperty<Length>(name, field, parse ?? Length.Parse, initial, inherited)
{
    public override bool IsTransitionable => true;

    protected override Length LerpInitial => Initial ?? 0;

    public override void Scale(Styles styles, float scale)
    {
        if (scaling != Scaling.None && Field(styles) is { } length)
        {
            Field(styles) = length.Scaled(scale, scaling == Scaling.Ceiling);
        }
    }

    protected override Length Interpolate(Length from, Length to, float delta) => Length.Lerp(from, to, delta);
}

internal sealed class ColorProperty(string name, StyleField<Color?> field, Color? initial, bool inherited = false)
    : ValueProperty<Color>(name, field, Color.ParseStyle, initial, inherited)
{
    public override bool IsTransitionable => true;

    public override void ResolveCurrentColor(Styles styles, Color color)
    {
        if (Field(styles) is { IsCurrentColor: true })
        {
            Field(styles) = color;
        }
    }

    protected override Color Interpolate(Color from, Color to, float delta)
    {
        if (from.A <= 0)
        {
            from = to.WithAlpha(0);
        }
        else if (to.A <= 0)
        {
            to = from.WithAlpha(0);
        }

        return Color.Lerp(from, to, delta);
    }
}

internal sealed class EnumProperty<T>(string name, StyleField<T?> field, T initial, bool inherited = false, params (string Name, T Value)[] names)
    : ValueProperty<T>(name, field, value => Parse(value, names), initial, inherited)
    where T : struct, Enum
{
    private static T? Parse(string value, (string Name, T Value)[] names)
    {
        foreach (var (name, enumValue) in names)
        {
            if (value.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return enumValue;
            }
        }

        return null;
    }
}

internal sealed class TransformProperty(string name, StyleField<PanelTransform?> field)
    : ValueProperty<PanelTransform>(name, field, PanelTransform.Parse, default(PanelTransform))
{
    public override bool IsTransitionable => true;

    public override void Scale(Styles styles, float scale) => Field(styles) = Field(styles)?.Scaled(scale);

    protected override PanelTransform Interpolate(PanelTransform from, PanelTransform to, float delta) => PanelTransform.Lerp(from, to, delta);
}

internal sealed class ShadowProperty(string name, StyleField<IReadOnlyList<Shadow>?> field, bool inherited = false)
    : StyleProperty<IReadOnlyList<Shadow>?>(name, field, Shadow.ParseList, [], inherited)
{
    public override bool IsTransitionable => true;

    public override bool IsDefault(Styles styles) => Field(styles) is { Count: 0 };

    public override int GetHashCode(Styles styles)
    {
        var hash = new HashCode();
        foreach (var shadow in Field(styles) ?? [])
        {
            hash.Add(shadow);
        }

        return hash.ToHashCode();
    }

    public override void Scale(Styles styles, float scale)
    {
        if (Field(styles) is { Count: > 0 } shadows)
        {
            Field(styles) = [.. shadows.Select(s => s.Scale(scale))];
        }
    }

    public override void Lerp(Styles target, Styles from, Styles to, float delta)
    {
        if (Field(from) is null && Field(to) is null)
        {
            return;
        }

        var start = Field(from) ?? [];
        Field(target) = Shadow.Lerp(start, Field(to) ?? (Inherited ? start : []), delta);
    }

    public override void ResolveCurrentColor(Styles styles, Color color)
    {
        if (Field(styles) is { } shadows && shadows.Any(s => s.Color.IsCurrentColor))
        {
            Field(styles) = [.. shadows.Select(s => s.Color.IsCurrentColor ? s with { Color = color } : s)];
        }
    }
}

internal sealed class BorderShapeProperty(string name, StyleField<BorderShape?> field)
    : StyleProperty<BorderShape?>(name, field, BorderShape.Parse, BorderShape.None)
{
    public override void Scale(Styles styles, float scale) => Field(styles) = Field(styles)?.Scaled(scale);
}
