using System.Reflection;

namespace Socotra;

public partial class Panel
{
    /// <summary>The stylesheets on this panel and all of its ancestors. A menu's list also gets the ones on the row that opened it.</summary>
    public IEnumerable<StyleSheet> AllStyleSheets
    {
        get
        {
            for (Panel? p = this; p is not null; p = p.StyleParent)
            {
                foreach (var sheet in p.StyleSheet.Sheets)
                {
                    yield return sheet;
                }
            }
        }
    }

    private void LoadStyleSheets()
    {
        var ownInline = false;
        foreach (var type in TypeHierarchy(GetType()))
        {
            foreach (var inline in type.GetCustomAttributes<StyleSheet.InlineAttribute>(false))
            {
                StyleSheet.AddInline(inline.Styles, $"inline:{inline.Name}");
                ownInline |= type == GetType();
            }
        }

        if (ownInline)
        {
            return;
        }

        var location = GetType().GetCustomAttributes<ClassFileLocationAttribute>(false).MinBy(attribute => attribute.Path.Length)?.Path;
        var attributes = GetType().GetCustomAttributes<StyleSheetAttribute>(false).ToArray();
        foreach (var attribute in attributes)
        {
            if (StyleSheetPath(attribute.Name, location) is { } path)
            {
                StyleSheet.Load(path);
            }
            else
            {
                Log.Warning($"{GetType().Name} has [StyleSheet] with no path, but Socotra.Generator didn't record where its .razor file is.");
            }
        }

        if (attributes.Length == 0 && location is not null && File.Exists(Path.GetFullPath($"{location}.scss", AppContext.BaseDirectory)))
        {
            StyleSheet.Load($"{location}.scss");
        }
    }

    private static string? StyleSheetPath(string? name, string? location) => (name, location) switch
    {
        (null or "", null) => null,
        (null or "", { } file) => $"{file}.scss",
        ({ } relative, { } file) when !relative.StartsWith('/') && !relative.StartsWith('\\') => Path.Combine(Path.GetDirectoryName(file) ?? "", relative),
        _ => name!.TrimStart('/', '\\'),
    };

    private static IEnumerable<Type> TypeHierarchy(Type type)
    {
        var types = new Stack<Type>();
        for (Type? t = type; t is not null && t != typeof(Panel); t = t.BaseType)
        {
            types.Push(t);
        }

        return types;
    }
}

/// <summary>
/// Attaches a stylesheet file to panels of this type, but not to types built on it. A type with a
/// <see cref="StyleSheet.InlineAttribute"/> of its own doesn't load it.
/// </summary>
/// <remarks>
/// For a Razor component built with <c>Socotra.Generator</c>, a relative path starts next to its <c>.razor</c> file,
/// a path starting with <c>/</c> starts from your program's folder, and leaving the path out loads
/// <c>Name.razor.scss</c> from beside it. A component with no <see cref="StyleSheetAttribute"/> at all loads that file
/// too, if it's there. Anything else starts from your program's folder.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class StyleSheetAttribute(string? name = null) : Attribute
{
    /// <summary>The stylesheet's path, or null for the one beside the component.</summary>
    public string? Name => name;
}

/// <summary>
/// Where a Razor component's <c>.razor</c> file sits, relative to your program's folder, so its stylesheets can be
/// found beside it. <c>Socotra.Generator</c> adds it for you; you don't need to write it yourself.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class ClassFileLocationAttribute(string path) : Attribute
{
    /// <summary>The <c>.razor</c> file's path.</summary>
    public string Path => path;
}
