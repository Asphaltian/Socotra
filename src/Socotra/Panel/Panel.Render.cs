using System.Reflection;
using System.Runtime.CompilerServices;

namespace Socotra;

public partial class Panel
{
    private static readonly ConditionalWeakTable<Type, StrongBox<bool>> DrawCallbacks = [];

    private bool _hasDrawCallback;

    /// <summary>
    /// Draws custom content for this panel, on top of its background and under its children. The painter starts at the
    /// panel's top left corner and <see cref="Painter.Bounds"/> is the panel's size.
    /// </summary>
    public virtual void OnDraw(Painter painter)
    {
    }

    private void UpdateDrawCallbacks() =>
        _hasDrawCallback = DrawCallbacks.GetValue(GetType(), static type => new StrongBox<bool>(HasDrawOverride(type))).Value;

    private static bool HasDrawOverride(Type type)
    {
        for (var current = type; current != typeof(Panel) && current is not null; current = current.BaseType)
        {
            var method = current.GetMethod(nameof(OnDraw), BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, [typeof(Painter)]);
            if (method is not null && method.GetBaseDefinition().DeclaringType == typeof(Panel))
            {
                return true;
            }
        }

        return false;
    }
}
