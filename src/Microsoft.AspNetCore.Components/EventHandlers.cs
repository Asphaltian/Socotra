namespace Microsoft.AspNetCore.Components
{
    /// <summary>Tells the Razor compiler that an attribute is an event, like <c>onclick</c>.</summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    public sealed class EventHandlerAttribute(string attributeName, Type eventArgsType, bool enableStopPropagation, bool enablePreventDefault) : Attribute
    {
        /// <summary>The event's attribute name, like <c>onclick</c>.</summary>
        public string AttributeName { get; } = attributeName;

        /// <summary>The type of the event's arguments.</summary>
        public Type EventArgsType { get; } = eventArgsType;

        /// <summary>Whether markup can stop the event going further up.</summary>
        public bool EnableStopPropagation { get; } = enableStopPropagation;

        /// <summary>Whether markup can stop the event's default action.</summary>
        public bool EnablePreventDefault { get; } = enablePreventDefault;
    }

    /// <summary>Only there so the Razor compiler's event handling compiles. Socotra doesn't use it.</summary>
    public interface IHandleEvent
    {
    }

    /// <summary>Only there so the Razor compiler's event handling compiles. Socotra doesn't use it.</summary>
    public readonly struct EventCallback
    {
        /// <summary>Makes event callbacks.</summary>
        public static readonly EventCallbackFactory Factory = new();

        /// <summary>Whether it has a delegate to call. Always false.</summary>
        public bool HasDelegate => false;
    }

    /// <summary>Only there so the Razor compiler's event handling compiles. Socotra doesn't use it.</summary>
    public readonly struct EventCallback<TValue>
    {
        /// <summary>Whether it has a delegate to call. Always false.</summary>
        public bool HasDelegate => false;
    }

    /// <summary>Only there so the Razor compiler's event handling compiles. Socotra doesn't use it.</summary>
    public sealed class EventCallbackFactory
    {
        /// <summary>Always throws, because Socotra doesn't use event callbacks.</summary>
        public EventCallback<TValue> Create<TValue>(object receiver, Action callback) => throw new NotSupportedException();
    }
}

namespace Microsoft.AspNetCore.Components.Web
{
    /// <summary>The events Razor markup can handle, like <c>onclick</c>.</summary>
    [EventHandler("onfocus", typeof(EventArgs), true, true)]
    [EventHandler("onblur", typeof(EventArgs), true, true)]
    [EventHandler("onfocusin", typeof(EventArgs), true, true)]
    [EventHandler("onfocusout", typeof(EventArgs), true, true)]
    [EventHandler("onmouseover", typeof(EventArgs), true, true)]
    [EventHandler("onmouseout", typeof(EventArgs), true, true)]
    [EventHandler("onmouseleave", typeof(EventArgs), true, true)]
    [EventHandler("onmouseenter", typeof(EventArgs), true, true)]
    [EventHandler("onmousemove", typeof(EventArgs), true, true)]
    [EventHandler("onmousedown", typeof(EventArgs), true, true)]
    [EventHandler("onmouseup", typeof(EventArgs), true, true)]
    [EventHandler("onclick", typeof(EventArgs), true, true)]
    [EventHandler("ondblclick", typeof(EventArgs), true, true)]
    public static class EventHandlers
    {
    }
}
