using Microsoft.AspNetCore.Components.Rendering;
using Socotra;

namespace Microsoft.AspNetCore.Components;

/// <summary>Your Razor files compile to calls to these, so you won't need to call them yourself.</summary>
public static class RazorExtensions
{
    extension(RenderTreeBuilder builder)
    {
        /// <summary>An event handler that takes the event, like <c>onclick=@((PanelEvent e) => Select(e))</c>.</summary>
        public void AddAttribute(int sequence, string name, Action<PanelEvent> value) => (builder as PanelRenderTreeBuilder)?.AddPanelEventAttribute(name, value);

        /// <summary>An event handler that's an async method.</summary>
        public void AddAttribute(int sequence, string name, Func<Task> value) => (builder as PanelRenderTreeBuilder)?.AddAttributeAction(name, value);

        /// <summary>An event handler, like <c>onclick=@Close</c>.</summary>
        public void AddAttribute(int sequence, string name, Action value) => (builder as PanelRenderTreeBuilder)?.AddAttributeAction(name, value);

        /// <summary>An attribute with a value, like <c>disabled=@IsBusy</c>.</summary>
        public void AddAttribute(int sequence, string name, object? value) => (builder as PanelRenderTreeBuilder)?.AddAttributeObject(name, value);

        /// <summary>An attribute with text, like <c>class</c> or <c>style</c>.</summary>
        public void AddAttribute(int sequence, string name, string? value) => (builder as PanelRenderTreeBuilder)?.AddAttributeString(name, value);

        /// <summary>A property on a panel, like <c>&lt;Button Text=@Label&gt;</c>.</summary>
        public void AddAttribute<T>(int sequence, object? value, Action<T> setter) => (builder as PanelRenderTreeBuilder)?.AddAttributeWithSetter(sequence, value, setter);
    }
}
