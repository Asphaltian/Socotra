using System.Collections.Concurrent;
using System.Reflection;

namespace Socotra;

public partial class Panel
{
    private static readonly ConcurrentDictionary<Type, (MethodInfo Method, string EventName)[]> EventMethods = new();

    private List<Listener>? _listeners;
    private List<PanelEvent>? _pendingEvents;

    /// <summary>Calls <paramref name="e"/> when the named event reaches this panel, like <c>onclick</c>.</summary>
    public void AddEventListener(string eventName, Action<PanelEvent> e) => AddListener(new Listener(eventName, e, ListenerSource.Code));

    /// <summary>Calls <paramref name="action"/> when the named event reaches this panel, without the event's details.</summary>
    public void AddEventListener(string eventName, Action action) => AddListener(new Listener(eventName, _ => action(), ListenerSource.Code));

    /// <summary>
    /// Sends an event called <paramref name="name"/> from this panel. Its ancestors get it too, until one stops it. Pass
    /// <paramref name="debounce"/> in seconds to wait for a pause first, like for a search box: sending it again before
    /// then only updates the value.
    /// </summary>
    public virtual void CreateEvent(string name, object? value = null, float? debounce = null)
    {
        var e = _pendingEvents?.FirstOrDefault(x => x.Name == name);
        if (e is null)
        {
            e = new PanelEvent(name, this);
            CreateEvent(e);
        }

        e.Value = value;
        if (debounce is { } delay)
        {
            e.Time = (float)(TimeNow + delay);
        }
    }

    /// <summary>Sends <paramref name="evnt"/> from this panel. Its ancestors get it too, until one calls <see cref="PanelEvent.StopPropagation"/>.</summary>
    public virtual void CreateEvent(PanelEvent evnt)
    {
        _pendingEvents ??= [];
        _pendingEvents.Add(evnt);
    }

    /// <summary>Hooks up the methods marked with <see cref="PanelEventAttribute"/> when the panel is made. Override it to add listeners of your own, and call the base method.</summary>
    protected virtual void InitializeEvents()
    {
        foreach (var (method, eventName) in EventMethods.GetOrAdd(GetType(), FindEventMethods))
        {
            var parameters = method.GetParameters();
            switch (parameters)
            {
                case [{ ParameterType: var type }] when type == typeof(PanelEvent):
                    AddListener(new Listener(eventName, e => method.Invoke(this, [e]), ListenerSource.Automatic));
                    break;
                case [{ ParameterType: var type }]:
                    AddListener(new Listener(eventName, e => StopUnlessTrue(e, method.Invoke(this, [Convert.ChangeType(e.Value, type)])), ListenerSource.Automatic));
                    break;
                case []:
                    AddListener(new Listener(eventName, e => StopUnlessTrue(e, method.Invoke(this, null)), ListenerSource.Automatic));
                    break;
                default:
                    Log.Warning($"PanelEvent {method} - couldn't set up (too many arguments)");
                    break;
            }
        }
    }

    /// <summary>Called for every event that reaches this panel. Override it to handle events by name, and call the base method so handlers like <see cref="OnClick"/> still run.</summary>
    protected virtual void OnEvent(PanelEvent e)
    {
        e.This = this;

        if (e is CopyEvent or CutEvent && GetClipboardValue(e is CutEvent) is { } text)
        {
            Clipboard.SetText(text);
        }

        if (e is PasteEvent paste)
        {
            OnPaste(paste.ClipboardValue);
        }

        switch (e.Name.ToLowerInvariant())
        {
            case "onclick" when e is MousePanelEvent mouse:
                OnClick(mouse);
                break;
            case "onmiddleclick" when e is MousePanelEvent mouse:
                OnMiddleClick(mouse);
                break;
            case "onrightclick" when e is MousePanelEvent mouse:
                OnRightClick(mouse);
                break;
            case "onmousedown" when e is MousePanelEvent mouse:
                OnMouseDown(mouse);
                break;
            case "onmouseup" when e is MousePanelEvent mouse:
                OnMouseUp(mouse);
                break;
            case "ondoubleclick" when e is MousePanelEvent mouse:
                OnDoubleClick(mouse);
                break;
            case "ontripleclick" when e is MousePanelEvent mouse:
                OnTripleClick(mouse);
                break;
            case "onmousemove" when e is MousePanelEvent mouse:
                OnMouseMove(mouse);
                break;
            case "onmouseover" when e is MousePanelEvent mouse:
                OnMouseOver(mouse);
                break;
            case "onmouseout" when e is MousePanelEvent mouse:
                OnMouseOut(mouse);
                break;
            case "ondragstart" when e is DragEvent drag:
                OnDragStart(drag);
                break;
            case "ondragend" when e is DragEvent drag:
                OnDragEnd(drag);
                break;
            case "ondrag" when e is DragEvent drag:
                OnDrag(drag);
                break;
            case "ondrop":
                OnDrop(e);
                break;
            case "ondragenter":
                OnDragEnter(e);
                break;
            case "ondragleave":
                OnDragLeave(e);
                break;
            case "onfocus":
                OnFocus(e);
                break;
            case "onblur":
                OnBlur(e);
                break;
            case "onback":
                OnBack(e);
                break;
            case "onforward":
                OnForward(e);
                break;
            case "onescape":
                OnEscape(e);
                break;
            case "ondragselect" when e is SelectionEvent selection:
                OnDragSelect(selection);
                break;
        }

        if (e is MousePanelEvent && !e.Is("onmousemove"))
        {
            StateHasChanged();
        }

        if (!e.Propagate)
        {
            return;
        }

        foreach (var listener in _listeners?.ToArray() ?? [])
        {
            if (e.Is(listener.EventName))
            {
                listener.Action(e);
            }
        }

        if (!e.Propagate)
        {
            return;
        }

        Parent?.OnEvent(e);
    }

    /// <summary>Called when the left mouse button is released over this panel after being pressed on it.</summary>
    protected virtual void OnClick(MousePanelEvent e)
    {
    }

    /// <summary>Called when the middle mouse button is released over this panel after being pressed on it.</summary>
    protected virtual void OnMiddleClick(MousePanelEvent e)
    {
    }

    /// <summary>Called when the right mouse button is released over this panel after being pressed on it.</summary>
    protected virtual void OnRightClick(MousePanelEvent e)
    {
    }

    /// <summary>Called when a mouse button goes down over this panel.</summary>
    protected virtual void OnMouseDown(MousePanelEvent e)
    {
    }

    /// <summary>Called when a mouse button that went down on this panel comes back up.</summary>
    protected virtual void OnMouseUp(MousePanelEvent e)
    {
    }

    /// <summary>Called when the panel is double clicked.</summary>
    protected virtual void OnDoubleClick(MousePanelEvent e)
    {
    }

    /// <summary>Called when the panel is clicked a third time in quick succession. It arrives after the double click, so a selection can grow from word to line.</summary>
    protected virtual void OnTripleClick(MousePanelEvent e)
    {
    }

    /// <summary>Called when the mouse moves over this panel, or anywhere while a button pressed on it is held.</summary>
    protected virtual void OnMouseMove(MousePanelEvent e)
    {
    }

    /// <summary>Called when the mouse moves onto the panel.</summary>
    protected virtual void OnMouseOver(MousePanelEvent e)
    {
    }

    /// <summary>Called when the mouse leaves the panel.</summary>
    protected virtual void OnMouseOut(MousePanelEvent e)
    {
    }

    /// <summary>Called when the back button on the side of the mouse is pressed over this panel.</summary>
    protected virtual void OnBack(PanelEvent e)
    {
    }

    /// <summary>Called when the forward button on the side of the mouse is pressed over this panel.</summary>
    protected virtual void OnForward(PanelEvent e)
    {
    }

    /// <summary>Called for an <c>onescape</c> event. By default it takes focus away from the panel.</summary>
    protected virtual void OnEscape(PanelEvent e)
    {
        if (HasFocus)
        {
            Blur();
        }
    }

    /// <summary>Called when the panel gets input focus.</summary>
    protected virtual void OnFocus(PanelEvent e)
    {
    }

    /// <summary>Called when the panel loses input focus.</summary>
    protected virtual void OnBlur(PanelEvent e)
    {
    }

    /// <summary>Called as the mouse drags from a press on this panel, selecting text. With <see cref="AllowChildSelection"/> on, it selects the text inside the panel.</summary>
    protected virtual void OnDragSelect(SelectionEvent e)
    {
        if (!AllowChildSelection)
        {
            return;
        }

        e.StopPropagation();
        if (LayoutTree.SelectInlineText(e.StartPoint, e.EndPoint))
        {
            return;
        }

        foreach (var child in Children)
        {
            UpdateSelection(child, e);
        }
    }

    internal void DispatchEventImmediate(PanelEvent evnt) => OnEvent(evnt);

    internal void SetEventListener(string eventName, Action<PanelEvent> handler)
    {
        _listeners?.RemoveAll(l => l.Source == ListenerSource.Markup && l.EventName == eventName);
        AddListener(new Listener(eventName, handler, ListenerSource.Markup));
    }

    internal void RunPendingEvents()
    {
        if (_pendingEvents is not { Count: > 0 })
        {
            return;
        }

        for (int i = 0; i < _pendingEvents.Count; i++)
        {
            var e = _pendingEvents[i];
            if (e.Time > TimeNow)
            {
                continue;
            }

            _pendingEvents.RemoveAt(i);
            i--;

            try
            {
                OnEvent(e);
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }

            if (_pendingEvents is null)
            {
                return;
            }
        }
    }

    private static void StopUnlessTrue(PanelEvent e, object? result)
    {
        if (result is false)
        {
            e.StopPropagation();
        }
    }

    private static (MethodInfo Method, string EventName)[] FindEventMethods(Type type) =>
    [
        .. type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Select(method => (Method: method, Attribute: method.GetCustomAttribute<PanelEventAttribute>()))
            .Where(m => m.Attribute is not null)
            .Select(m => (m.Method, EventName: m.Attribute!.Name?.ToLowerInvariant() ?? EventNameOf(m.Method))),
    ];

    private static string EventNameOf(MethodInfo method)
    {
        var name = method.Name.ToLowerInvariant();
        return name.EndsWith("event", StringComparison.Ordinal) ? name[..^5] : name;
    }

    private void AddListener(Listener listener)
    {
        _listeners ??= [];
        _listeners.Add(listener);
    }

    private enum ListenerSource
    {
        Code,
        Markup,
        Automatic,
    }

    private sealed record Listener(string EventName, Action<PanelEvent> Action, ListenerSource Source);
}
