using static Socotra.Tests.ControlTesting;

namespace Socotra.Tests;

public class PointerTests
{
    private const string Styles = """
        rootpanel { flex-direction: column; align-items: flex-start; pointer-events: all; }
        .item { width: 100px; height: 50px; flex-shrink: 0; }
        .list { width: 100px; height: 100px; flex-direction: column; overflow: scroll; }
        """;

    private static void MoveTo(RootPanel root, Vector2 position)
    {
        root.SetMousePosition(position);
        Update(root);
    }

    private static void Click(RootPanel root, MouseButtons button = MouseButtons.Left, KeyboardModifiers modifiers = KeyboardModifiers.None)
    {
        root.SetMouseButton(button, true, modifiers);
        root.SetMouseButton(button, false, modifiers);
        Update(root);
    }

    private static Recorder Recording(Panel panel, bool fromOthers = false)
    {
        var recorder = new Recorder(fromOthers);
        foreach (var name in (string[])["onmousedown", "onmouseup", "onclick", "onrightclick", "onmiddleclick", "ondoubleclick", "ontripleclick", "onmouseover", "onmouseout", "onmousemove", "ondragstart", "ondrag", "ondragend", "ondragenter", "ondragleave", "ondrop", "onback", "onforward"])
        {
            panel.AddEventListener(name, e => recorder.Add(e));
        }

        return recorder;
    }

    [Fact]
    public void ButtonsSendDownUpAndTheirOwnClicks()
    {
        var root = Root(Styles);
        var item = root.AddChild<Panel>("item");
        var events = Recording(item);
        Update(root);
        MoveTo(root, new Vector2(50, 25));

        Click(root);
        Click(root, MouseButtons.Right, KeyboardModifiers.Shift);
        Update(root, 1);
        Click(root, MouseButtons.Middle);
        Update(root);

        Assert.Equal(
            ["onmouseover", "onmousemove", "onmousedown", "onmouseup", "onclick", "onmousedown", "onmouseup", "onrightclick", "ondoubleclick", "onmousedown", "onmouseup", "onmiddleclick"],
            events.Names);
        var rightClick = events.Of<MousePanelEvent>("onrightclick").Single();
        Assert.Equal(MouseButtons.Right, rightClick.MouseButton);
        Assert.True(rightClick.HasShift);
        Assert.Equal(new Vector2(50, 25), rightClick.LocalPosition);
    }

    [Fact]
    public void QuickClicksInOnePlaceMakeDoubleAndTripleClicks()
    {
        var root = Root(Styles);
        var item = root.AddChild<Panel>("item");
        var events = Recording(item);
        Update(root);
        MoveTo(root, new Vector2(50, 25));

        Click(root);
        Click(root);
        Click(root);
        Update(root);

        Assert.Equal(1, events.Count("ondoubleclick"));
        Assert.Equal(1, events.Count("ontripleclick"));
        Assert.Equal([1, 2, 3], events.Of<MousePanelEvent>("onmousedown").Select(e => e.ClickCount));
    }

    [Fact]
    public void SlowOrDistantClicksAreSingleClicks()
    {
        var root = Root(Styles);
        var item = root.AddChild<Panel>("item");
        var events = Recording(item);
        Update(root);
        MoveTo(root, new Vector2(50, 25));

        Click(root);
        Update(root, 0.5f);
        Click(root);
        MoveTo(root, new Vector2(60, 25));
        Click(root);
        Update(root);

        Assert.Equal(0, events.Count("ondoubleclick"));
        Assert.Equal(3, events.Count("onclick"));
    }

    [Fact]
    public void MovingOverPanelsSendsOverOutAndMove()
    {
        var root = Root(Styles);
        var (first, second) = (root.AddChild<Panel>("item"), root.AddChild<Panel>("item"));
        var firstEvents = Recording(first);
        var secondEvents = Recording(second);
        Update(root);

        MoveTo(root, new Vector2(50, 25));
        MoveTo(root, new Vector2(50, 75));
        Update(root);

        Assert.Equal(["onmouseover", "onmousemove", "onmouseout"], firstEvents.Names);
        Assert.Equal(["onmouseover", "onmousemove"], secondEvents.Names);
    }

    [Fact]
    public void MovesGoToThePressedPanelWhileAButtonIsHeld()
    {
        var root = Root(Styles);
        var (first, second) = (root.AddChild<Panel>("item"), root.AddChild<Panel>("item"));
        Update(root);
        MoveTo(root, new Vector2(50, 25));
        var firstEvents = Recording(first);
        var secondEvents = Recording(second);

        root.SetMouseButton(MouseButtons.Left, true);
        Update(root);
        MoveTo(root, new Vector2(50, 75));
        Update(root);

        Assert.Contains("onmousemove", firstEvents.Names);
        Assert.DoesNotContain("onmousemove", secondEvents.Names);
    }

    [Fact]
    public void DraggingAPanelSendsDragEventsAndDropsOnTheTarget()
    {
        var root = Root(Styles);
        var source = root.AddChild<Draggable>("item");
        var target = root.AddChild<Panel>("item");
        var sourceEvents = Recording(source);
        var targetEvents = Recording(target, fromOthers: true);
        Update(root);
        MoveTo(root, new Vector2(20, 20));

        root.SetMouseButton(MouseButtons.Left, true);
        Update(root);
        MoveTo(root, new Vector2(22, 21));
        Assert.DoesNotContain("ondragstart", sourceEvents.Names);

        MoveTo(root, new Vector2(40, 30));
        MoveTo(root, new Vector2(40, 70));
        root.SetMouseButton(MouseButtons.Left, false);
        Update(root);
        Update(root);

        Assert.Equal(["onmouseover", "onmousemove", "onmousedown", "onmousemove", "ondragstart", "onmouseup", "ondrag", "onmousemove", "ondrag", "onmousemove", "ondragend"], sourceEvents.Names);
        Assert.Equal(["ondragenter", "ondrop", "ondragleave"], targetEvents.Names);
        Assert.Same(source, targetEvents.Events.Last().Target);

        var drag = sourceEvents.Of<DragEvent>("ondrag").Last();
        Assert.Equal(new Vector2(20, 20), drag.LocalGrabPosition);
        Assert.Equal(new Vector2(40, 70), drag.ScreenPosition);
        Assert.Equal(0, sourceEvents.Count("onclick"));
    }

    [Fact]
    public void DraggingAScrollingPanelScrollsIt()
    {
        var root = Root(Styles);
        var list = root.AddChild<Panel>("list");
        for (int i = 0; i < 6; i++)
        {
            list.AddChild<Panel>("item");
        }

        Update(root);
        MoveTo(root, new Vector2(50, 90));
        root.SetMouseButton(MouseButtons.Left, true);
        Update(root);
        MoveTo(root, new Vector2(50, 60));
        MoveTo(root, new Vector2(50, 50));
        Update(root);

        Assert.True(list.IsDragScrolling);
        Assert.Equal(new Vector2(0, 40), list.ScrollOffset);

        root.SetMouseButton(MouseButtons.Left, false);
        Update(root);
        Update(root);
        Assert.False(list.IsDragScrolling);
    }

    [Fact]
    public void StoppingTheMouseDownEventStopsDragging()
    {
        var root = Root(Styles);
        var source = root.AddChild<Draggable>("item");
        var events = Recording(source);
        source.AddEventListener("onmousedown", e => e.StopPropagation());
        Update(root);
        MoveTo(root, new Vector2(20, 20));

        root.SetMouseButton(MouseButtons.Left, true);
        Update(root);
        MoveTo(root, new Vector2(40, 40));
        Update(root);

        Assert.DoesNotContain("ondragstart", events.Names);
    }

    [Fact]
    public void BackAndForwardButtonsSendNavigationEvents()
    {
        var root = Root(Styles);
        var item = root.AddChild<Panel>("item");
        var events = Recording(item);
        Update(root);
        MoveTo(root, new Vector2(50, 25));

        Click(root, MouseButtons.Back);
        Update(root, 1);
        Click(root, MouseButtons.Forward);
        Update(root);

        Assert.Contains("onback", events.Names);
        Assert.Contains("onforward", events.Names);
        Assert.DoesNotContain("onmousedown", events.Names);
    }

    [Fact]
    public void APressedPanelMovedToAnotherUILetsGoOfTheMouse()
    {
        var root = Root(Styles);
        var holder = root.AddChild<Panel>();
        var item = holder.AddChild<Panel>("item");
        Update(root);
        MoveTo(root, new Vector2(50, 25));
        var events = Recording(item);
        root.SetMouseButton(MouseButtons.Left, true);
        Update(root);
        item.SetMouseCapture(true);

        item.Parent = new RootPanel();
        root.SetMouseButton(MouseButtons.Left, false);
        Update(root);

        Assert.Null(root.MouseCapture);
        Assert.Null(root.Input.Active);
        Assert.False(holder.HasActive);
        Assert.False(item.HasActive || item.HasHovered);
        Assert.DoesNotContain("onclick", events.Names);
    }

    [Fact]
    public void CapturedMouseMovementIsReported()
    {
        var root = Root(Styles);
        var item = root.AddChild<Panel>("item");
        Update(root);

        item.SetMouseCapture(true);
        Assert.Same(item, root.MouseCapture);
        Assert.True(item.HasMouseCapture);

        root.AddMouseDelta(new Vector2(3, -2));
        root.AddMouseDelta(new Vector2(1, 1));
        Update(root);
        Assert.Equal(new Vector2(4, -1), root.MouseDelta);

        Update(root);
        Assert.Equal(Vector2.Zero, root.MouseDelta);

        item.Delete(true);
        Assert.Null(root.MouseCapture);
    }

    [Fact]
    public void WantsMouseInputFollowsPointerEvents()
    {
        var root = new RootPanel();
        root.StyleSheet.Parse("rootpanel { pointer-events: none; } .none { pointer-events: none; } .all { pointer-events: all; }");
        var child = root.AddChild<Panel>("none");
        Update(root);
        Assert.False(root.WantsMouseInput());

        child.AddChild<Panel>("all");
        Update(root);
        Assert.True(root.WantsMouseInput());
    }

    private sealed class Draggable : Panel
    {
        public override bool WantsDrag => true;
    }

    private sealed class Recorder(bool fromOthers)
    {
        public List<PanelEvent> Events { get; } = [];

        public List<string> Names => [.. Events.Select(e => e.Name)];

        public void Add(PanelEvent e)
        {
            if (fromOthers || e.This == e.Target)
            {
                Events.Add(e);
            }
        }

        public int Count(string name) => Events.Count(e => e.Name == name);

        public IEnumerable<T> Of<T>(string name)
            where T : PanelEvent => Events.Where(e => e.Name == name).OfType<T>();
    }
}
