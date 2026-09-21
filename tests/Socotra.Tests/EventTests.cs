using static Socotra.Tests.ControlTesting;

namespace Socotra.Tests;

public class EventTests
{
    [Fact]
    public void EventsWaitForTheNextUpdateThenBubbleUp()
    {
        var root = new RootPanel();
        var outer = root.AddChild<Panel>();
        var inner = outer.AddChild<Panel>();
        var seen = new List<(Panel? This, Panel? Target)>();
        outer.AddEventListener("onchange", e => seen.Add((e.This, e.Target)));
        root.AddEventListener("onchange", e => seen.Add((e.This, e.Target)));

        inner.CreateEvent("onchange", 5);
        Assert.Empty(seen);

        Update(root);
        Assert.Equal([(outer, inner), (root, inner)], seen);
    }

    [Fact]
    public void StoppingPropagationKeepsTheEventFromAncestors()
    {
        var root = new RootPanel();
        var outer = root.AddChild<Panel>();
        var inner = outer.AddChild<Panel>();
        int reachedRoot = 0;
        inner.AddEventListener("onchange", e => e.StopPropagation());
        root.AddEventListener("onchange", () => reachedRoot++);

        inner.CreateEvent("onchange");
        Update(root);

        Assert.Equal(0, reachedRoot);
    }

    [Fact]
    public void DebouncedEventsWaitAndKeepTheLatestValue()
    {
        var root = new RootPanel();
        var panel = root.AddChild<Panel>();
        var values = new List<object?>();
        panel.AddEventListener("onchange", e => values.Add(e.Value));

        panel.CreateEvent("onchange", "a", debounce: 0.1f);
        Update(root, 0.05f);
        panel.CreateEvent("onchange", "b", debounce: 0.1f);
        Update(root, 0.05f);
        Assert.Empty(values);

        Update(root, 0.1f);
        Assert.Equal(["b"], values);
    }

    [Fact]
    public void PanelEventMethodsListenByName()
    {
        var root = new RootPanel();
        var panel = root.AddChild<EventMethods>();

        panel.CreateEvent("onsave");
        panel.CreateEvent("onvalue", "42");
        panel.CreateEvent("renamed", "x");
        Update(root);

        Assert.Equal(["save", "value 42", "custom"], panel.Calls);
    }

    [Fact]
    public void AMethodReturningFalseStopsTheEvent()
    {
        var root = new RootPanel();
        var panel = root.AddChild<EventMethods>();
        int reachedRoot = 0;
        root.AddEventListener("onblocked", () => reachedRoot++);

        panel.CreateEvent("onblocked");
        Update(root);

        Assert.Equal(0, reachedRoot);
    }

    [Fact]
    public void ValueEventsAreNamedAfterTheValue()
    {
        var root = new RootPanel();
        var panel = root.AddChild<EventMethods>();
        object? value = null;
        root.AddEventListener("volume.changed", e => value = e.Value);

        panel.ChangeVolume(0.5f);
        Update(root);

        Assert.Equal(0.5f, value);
    }

    [Fact]
    public void MouseEventsRebuildRazorPanelsButOtherEventsDoNot()
    {
        var root = new RootPanel();
        var panel = root.AddChild<RenderCounter>();
        Update(root);
        var builds = panel.Builds;

        panel.CreateEvent("onchange");
        Update(root);
        Update(root);
        Assert.Equal(builds, panel.Builds);

        panel.CreateEvent(new MousePanelEvent("onclick", panel, "mouseleft"));
        Update(root);
        Update(root);
        Assert.Equal(builds + 1, panel.Builds);
    }

    [Fact]
    public void EventTypesCallTheirHandlers()
    {
        var root = new RootPanel();
        var panel = root.AddChild<Handlers>();

        foreach (var name in (string[])["onclick", "onmiddleclick", "onrightclick", "onmousedown", "onmouseup", "ondoubleclick", "ontripleclick", "onmousemove", "onmouseover", "onmouseout"])
        {
            panel.CreateEvent(new MousePanelEvent(name, panel, "mouseleft"));
        }

        foreach (var name in (string[])["onfocus", "onblur", "onback", "onforward", "ondrop", "ondragenter", "ondragleave"])
        {
            panel.CreateEvent(new PanelEvent(name, panel));
        }

        Update(root);

        Assert.Equal(
            ["click", "middle", "right", "down", "up", "double", "triple", "move", "over", "out", "focus", "blur", "back", "forward", "drop", "enter", "leave"],
            panel.Calls);
    }

    private sealed class EventMethods : Panel
    {
        public List<string> Calls { get; } = [];

        public void ChangeVolume(float volume) => CreateValueEvent("volume", volume);

        [PanelEvent]
        private void OnSave() => Calls.Add("save");

        [PanelEvent]
        private void OnValueEvent(int value) => Calls.Add($"value {value}");

        [PanelEvent("renamed")]
        private void Custom(PanelEvent e) => Calls.Add("custom");

        [PanelEvent]
        private bool OnBlocked() => false;
    }

    private sealed class RenderCounter : Panel
    {
        public int Builds { get; private set; }

        protected override string? GetRenderTreeChecksum() => "";

        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder) => Builds++;
    }

    private sealed class Handlers : Panel
    {
        public List<string> Calls { get; } = [];

        protected override void OnClick(MousePanelEvent e) => Calls.Add("click");

        protected override void OnMiddleClick(MousePanelEvent e) => Calls.Add("middle");

        protected override void OnRightClick(MousePanelEvent e) => Calls.Add("right");

        protected override void OnMouseDown(MousePanelEvent e) => Calls.Add("down");

        protected override void OnMouseUp(MousePanelEvent e) => Calls.Add("up");

        protected override void OnDoubleClick(MousePanelEvent e) => Calls.Add("double");

        protected override void OnTripleClick(MousePanelEvent e) => Calls.Add("triple");

        protected override void OnMouseMove(MousePanelEvent e) => Calls.Add("move");

        protected override void OnMouseOver(MousePanelEvent e) => Calls.Add("over");

        protected override void OnMouseOut(MousePanelEvent e) => Calls.Add("out");

        protected override void OnFocus(PanelEvent e) => Calls.Add("focus");

        protected override void OnBlur(PanelEvent e) => Calls.Add("blur");

        protected override void OnBack(PanelEvent e) => Calls.Add("back");

        protected override void OnForward(PanelEvent e) => Calls.Add("forward");

        protected override void OnDrop(PanelEvent e) => Calls.Add("drop");

        protected override void OnDragEnter(PanelEvent e) => Calls.Add("enter");

        protected override void OnDragLeave(PanelEvent e) => Calls.Add("leave");
    }
}
