using Microsoft.AspNetCore.Components.Rendering;

namespace Microsoft.AspNetCore.Components;

/// <summary>A piece of Razor markup, like the markup you put inside a component's <c>&lt;ChildContent&gt;</c>.</summary>
public delegate void RenderFragment(RenderTreeBuilder builder);

/// <summary>A piece of Razor markup that takes a value, like a template. The markup gets the value as <c>@context</c>.</summary>
public delegate RenderFragment RenderFragment<TValue>(TValue value);

/// <summary>Anything you can use as a tag in Razor markup, like a panel.</summary>
public interface IComponent
{
}

/// <summary>Marks a property as one Razor markup sets, like <c>&lt;Button Text="Start"&gt;</c>.</summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class ParameterAttribute : Attribute
{
}

/// <summary>What a Razor file inherits when it doesn't say <c>@inherits</c>.</summary>
public class ComponentBase : IComponent
{
}

/// <summary>Marks a property that tools should warn about when markup leaves it out.</summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class EditorRequiredAttribute : Attribute
{
}
