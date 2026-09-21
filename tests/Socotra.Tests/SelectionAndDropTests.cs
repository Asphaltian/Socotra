using static Socotra.Tests.ControlTesting;

namespace Socotra.Tests;

public class SelectionAndDropTests
{
    private const string Styles = """
        rootpanel { flex-direction: column; align-items: flex-start; pointer-events: all; }
        .item { width: 100px; height: 50px; flex-shrink: 0; }
        .ghost { width: 100px; height: 50px; pointer-events: none; }
        """;

    [Fact]
    public void DraggingFromAPanelSelectsBetweenTheTwoPoints()
    {
        var root = Root(Styles);
        var text = root.AddChild<SelectionCatcher>("item");
        Update(root);
        root.SetMousePosition(new Vector2(80, 40));
        Update(root);

        root.SetMouseButton(MouseButtons.Left, true);
        Update(root);
        root.SetMousePosition(new Vector2(10, 5));
        Update(root);
        root.SetMousePosition(new Vector2(10, 5));
        Update(root);
        Update(root);

        var e = Assert.Single(text.Selections);
        Assert.Equal(new Vector2(80, 40), e.StartPoint);
        Assert.Equal(new Vector2(10, 5), e.EndPoint);
        Assert.Equal(new Rect(10, 5, 70, 35), e.SelectionRect);
    }

    [Fact]
    public void ADragFromOutsideAsksThePanelUnderItThenDrops()
    {
        var root = Root(Styles);
        var target = root.AddChild<DropTarget>("item");
        var other = root.AddChild<DropTarget>("item");
        Update(root);

        root.DragEnter(["C:/save.sav"], null);
        Assert.Equal(DropAction.Copy, root.DragOver(new Vector2(50, 25)));
        Assert.Equal(DropAction.Copy, root.DragOver(new Vector2(50, 25)));
        Assert.Equal(DropAction.None, root.DragOver(new Vector2(50, 75)));
        Assert.Equal(DropAction.Copy, root.Drop(new Vector2(50, 25)));

        Assert.Equal(["over", "over", "leave", "drop C:/save.sav", "leave"], target.Calls);
        Assert.Equal(["over", "leave"], other.Calls);
    }

    [Fact]
    public void DropsThatOnlyArriveAsTheyLandAreGathered()
    {
        var root = Root(Styles);
        var target = root.AddChild<DropTarget>("item");
        Update(root);

        root.DropFile("a.txt");
        root.DropFile("b.txt");
        root.DropComplete(new Vector2(50, 25));

        Assert.Equal(["drop a.txt,b.txt", "leave"], target.Calls);
        Assert.Equal(DropAction.None, root.DragOver(new Vector2(50, 25)));
    }

    [Fact]
    public void FindingPanelsIgnoresPointerEventsUnlessAsked()
    {
        var root = Root(Styles);
        root.AddChild<Panel>("item");
        var ghost = root.AddChild<Panel>("ghost");
        Update(root);

        Assert.Same(ghost, root.FindPanelAt(new Vector2(50, 75)));
        Assert.Same(root, root.FindPanelAt(new Vector2(50, 75), p => p.ComputedStyle?.PointerEvents != PointerEvents.None));
        Assert.Null(root.FindPanelAt(new Vector2(5000, 75)));
    }

    private sealed class SelectionCatcher : Panel
    {
        public List<SelectionEvent> Selections { get; } = [];

        protected override void OnDragSelect(SelectionEvent e) => Selections.Add(e);
    }

    private sealed class DropTarget : Panel
    {
        public List<string> Calls { get; } = [];

        protected override void OnDrop(PanelEvent e)
        {
            var drop = (DropEvent)e;
            if (drop.IsDrop)
            {
                Calls.Add($"drop {string.Join(',', drop.Files)}");
            }
            else
            {
                Calls.Add("over");
            }

            if (this == Parent!.Children[0])
            {
                drop.Action = DropAction.Copy;
            }

            e.StopPropagation();
        }

        protected override void OnDragLeave(PanelEvent e) => Calls.Add("leave");
    }
}
