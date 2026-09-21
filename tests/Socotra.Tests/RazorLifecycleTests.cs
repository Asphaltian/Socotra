using Socotra.Tests.Razor;
using static Socotra.Tests.ControlTesting;

namespace Socotra.Tests;

public class RazorLifecycleTests
{
    private static IEnumerable<string> Texts(Panel panel) => panel.Descendants.OfType<Label>().Select(l => l.Text);

    [Fact]
    public void ParametersAreSetBeforeTheFirstRenderAndAfterEachChange()
    {
        var root = new RootPanel();
        var host = root.AddChild<LifecycleHost>();
        Update(root);
        Update(root);
        var child = host.Child!;

        Assert.Equal(["set 1", "first render"], child.Calls);

        host.Items = ["a", "b"];
        Update(root);
        Update(root);

        Assert.Equal(["set 1", "first render", "set 2", "render"], child.Calls);
    }

    [Fact]
    public void TemplatesAndChildContentAreRenderedByTheChild()
    {
        var root = new RootPanel();
        var host = root.AddChild<LifecycleHost>();
        Update(root);
        Update(root);

        Assert.Equal(["[a]", "x"], Texts(host.Child!));

        host.Extra = "y";
        Update(root);
        Update(root);

        Assert.Equal(["[a]", "y"], Texts(host.Child!));
    }

    [Fact]
    public void AsyncParameterWorkFinishesBeforeOnParametersSet()
    {
        var root = new RootPanel();
        var host = root.AddChild<LifecycleHost>();
        Update(root);
        Update(root);
        var child = host.Child!;
        child.Pending = new TaskCompletionSource();

        host.Items = ["a", "b", "c"];
        Update(root);
        Update(root);
        Assert.DoesNotContain("set 3", child.Calls);

        child.Pending.SetResult();
        Update(root);
        Update(root);
        Assert.Contains("set 3", child.Calls);
    }

    [Fact]
    public void AsyncEventHandlersRebuildTheirPanel()
    {
        var root = new RootPanel();
        var host = root.AddChild<LifecycleHost>();
        Update(root);
        Update(root);
        var button = host.Children.Single(c => c.HasClass("async"));

        button.CreateEvent("onclick");
        Update(root);
        Update(root);

        Assert.Equal(1, host.Saves);
        Assert.Equal("1", button.Children.OfType<Label>().Single().Text);
    }

    [Fact]
    public void RenderFragmentChangesMarkPanelsUpToTheOwner()
    {
        var root = new RootPanel();
        var outer = root.AddChild<Counter>();
        var inner = outer.AddChild<Counter>();
        Update(root);
        var (outerBuilds, innerBuilds) = (outer.Builds, inner.Builds);

        inner.OnRenderFragmentChanged(outer);
        Update(root);

        Assert.Equal(outerBuilds, outer.Builds);
        Assert.Equal(innerBuilds + 1, inner.Builds);
    }

    [Fact]
    public void RemovedPanelsKeepTheirContentUntilTheirOutroEnds()
    {
        Tracked.Deleted.Clear();
        var root = new RootPanel();
        root.StyleSheet.Parse(".row { opacity: 1; transition: opacity 0.5s linear; } .row:outro { opacity: 0; }");
        var list = root.AddChild<OutroList>();
        Update(root, 0);
        Update(root, 0);
        var row = list.Children[0];
        var tracked = row.Children.OfType<Tracked>().Single();

        list.Items.RemoveAt(0);
        Update(root, 0.1f);

        Assert.True(row.HasOutro);
        Assert.False(tracked.IsDeleted);
        Assert.Same(row, tracked.Parent);
        Assert.Empty(Tracked.Deleted);

        Update(root, 1);
        Update(root, 0);

        Assert.True(row.IsDeleted);
        Assert.True(tracked.IsDeleted);
        Assert.Equal(["a"], Tracked.Deleted);
    }

    [Fact]
    public void DeletingARazorPanelDeletesWhatItBuilt()
    {
        Tracked.Deleted.Clear();
        var root = new RootPanel();
        var list = root.AddChild<OutroList>();
        Update(root);
        var tracked = list.Descendants.OfType<Tracked>().ToList();

        list.Delete(true);

        Assert.All(tracked, t => Assert.True(t.IsDeleted));
        Assert.Equal(["a", "b"], Tracked.Deleted.Order());
    }

    private sealed class Counter : Panel
    {
        public int Builds { get; private set; }

        protected override string? GetRenderTreeChecksum() => "";

        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder) => Builds++;
    }
}
