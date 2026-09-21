namespace Microsoft.AspNetCore.Components.Rendering;

/// <summary>Your Razor files compile to calls on this, so you won't need to call it yourself.</summary>
public abstract class RenderTreeBuilder
{
    /// <summary>Records which file and line the next element comes from.</summary>
    public abstract void AddLocation(string filename, int line, int column);

    /// <summary>Starts an element, like <c>&lt;div&gt;</c>.</summary>
    public abstract void OpenElement(int sequence, string elementName);

    /// <summary>Starts an element, like <c>&lt;div&gt;</c>, with an <c>@key</c>.</summary>
    public abstract void OpenElement(int sequence, string elementName, object? key = null);

    /// <summary>Starts a panel of type <typeparamref name="T"/>.</summary>
    public abstract void OpenElement<T>(int sequence)
        where T : IComponent, new();

    /// <summary>Starts a panel of type <typeparamref name="T"/> with an <c>@key</c>.</summary>
    public abstract void OpenElement<T>(int sequence, object? key)
        where T : IComponent, new();

    /// <summary>Ends the current element.</summary>
    public abstract void CloseElement();

    /// <summary>Adds the styles from a <c>&lt;style&gt;</c> block.</summary>
    public abstract void AddStyleDefinitions(int sequence, string styles);

    /// <summary>Runs <paramref name="value"/> on the current panel.</summary>
    public abstract void AddAttribute<T>(int sequence, Action<T> value)
        where T : IComponent;

    /// <summary>Adds text, or a piece of markup.</summary>
    public abstract void AddContent<T>(int sequence, T content);

    /// <summary>Adds markup that never changes.</summary>
    public abstract void AddMarkupContent(int sequence, string markupContent);

    /// <summary>Hands the current panel to a <c>@ref</c>.</summary>
    public abstract void AddReferenceCapture<T>(int sequence, T current, Action<T> value)
        where T : IComponent?;

    /// <summary>Sets a piece of markup on the current panel, like its <c>&lt;ChildContent&gt;</c>.</summary>
    public abstract void SetRenderFragment<T>(Action<T, RenderFragment> setter, RenderFragment builder)
        where T : IComponent;

    /// <summary>Sets a template on the current panel, one that takes a value.</summary>
    public abstract void SetRenderFragmentWithContext<T, TValue>(Func<T, RenderFragment<TValue>> getter, Action<T, RenderFragment<TValue>> setter, RenderFragment<TValue> builder)
        where T : IComponent;

    /// <summary>Keeps a variable and the current panel's <paramref name="propertyName"/> the same, for <c>Value:bind=@name</c>.</summary>
    public abstract void AddBind<T>(int sequence, string propertyName, Func<T> get, Action<T> set);
}
