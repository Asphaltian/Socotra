using System.Collections.Concurrent;
using System.Reflection;
using Socotra;

namespace Microsoft.AspNetCore.Components;

/// <summary>
/// The url a panel shows up at in a <see cref="NavigationHost"/>. In Razor, write <c>@page "/settings/{Section}"</c>:
/// a part in braces matches anything and sets the panel's property of that name. End with <c>*</c> for a page
/// that has its own navigation host inside, so it matches every url under it.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class RouteAttribute : Attribute
{
    private static readonly ConcurrentDictionary<Assembly, (Type Type, RouteAttribute Attribute)[]> Routes = new();

    /// <summary>A route for <paramref name="url"/>, like <c>/settings/{Section}</c>.</summary>
    public RouteAttribute(string url)
    {
        Url = url;
        Parts = url.Split('/', StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>The full url, like <c>/home/section/page</c>.</summary>
    public string Url { get; }

    /// <summary>The url split into parts, like <c>home</c>, <c>section</c> and <c>page</c>.</summary>
    public string[] Parts { get; }

    /// <summary>The panel type whose route matches <paramref name="url"/> but not <paramref name="parentUrl"/>, or null if there isn't one. Routes ending in <c>*</c> win over exact ones.</summary>
    public static (Type Type, RouteAttribute Attribute)? FindValidTarget(string? url, string? parentUrl)
    {
        var found = AppDomain.CurrentDomain.GetAssemblies()
            .Where(ReferencesRoutes)
            .SelectMany(assembly => Routes.GetOrAdd(assembly, FindRoutes))
            .Where(x => x.Attribute.IsUrl(url) && !x.Attribute.IsUrl(parentUrl))
            .OrderByDescending(x => x.Attribute.Url.Count(c => c == '*'))
            .FirstOrDefault();

        return found.Type is null ? null : found;
    }

    /// <summary>Whether this route matches <paramref name="url"/>. Anything after a <c>?</c> is ignored.</summary>
    public bool IsUrl(string? url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return false;
        }

        if (url.IndexOf('?') is var query and >= 0)
        {
            url = url[..query];
        }

        var a = url.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < Parts.Length || i < a.Length; i++)
        {
            var left = i < a.Length ? a[i] : null;
            var right = i < Parts.Length ? Parts[i] : null;
            if (right == "*")
            {
                return true;
            }

            if (!TestPart(left, right))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Pairs each <c>{variable}</c> in this route with its value from <paramref name="url"/>. A variable the url doesn't reach gets null.</summary>
    public IEnumerable<(string key, string? value)> ExtractProperties(string url) => ExtractProperties(Parts, url);

    internal static IEnumerable<(string key, string? value)> ExtractProperties(string[] parts, string url)
    {
        var a = url.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].StartsWith('{') && parts[i].EndsWith('}'))
            {
                yield return (parts[i][1..^1].Trim('?'), i < a.Length ? a[i] : null);
            }
        }
    }

    private static bool TestPart(string? part, string? ours) => (ours is not null && ours.StartsWith('{') && ours.EndsWith('}')) || part == ours;

    private static bool ReferencesRoutes(Assembly assembly)
    {
        if (assembly.IsDynamic)
        {
            return false;
        }

        var name = typeof(RouteAttribute).Assembly.GetName().Name;
        return assembly == typeof(RouteAttribute).Assembly || assembly.GetReferencedAssemblies().Any(r => r.Name == name);
    }

    private static (Type Type, RouteAttribute Attribute)[] FindRoutes(Assembly assembly)
    {
        Type?[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            types = e.Types;
        }

        return [.. types.OfType<Type>().SelectMany(type => type.GetCustomAttributes<RouteAttribute>(false).Select(attribute => (type, attribute)))];
    }
}
