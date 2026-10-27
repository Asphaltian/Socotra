using Microsoft.AspNetCore.Components;
using static Socotra.Tests.ControlTesting;

namespace Socotra.Tests;

public class FloatingWindowTests
{
    private readonly RootPanel _root = Root();
    private readonly FloatingWindow _window;

    public FloatingWindowTests()
    {
        _window = new FloatingWindow { Parent = _root, Title = "Test window", Icon = "🪟", ShowCloseButton = true, ShowMinimizeButton = true };
    }

    private void Frame(int count = 3)
    {
        for (var i = 0; i < count; i++)
        {
            Update(_root);
        }
    }

    private FloatingWindowTitleBar TitleBar => _window.Children.OfType<FloatingWindowTitleBar>().Single();

    private Button Control(string icon) => TitleBar.Children.OfType<Button>().Single(x => x.Icon == icon);

    private void MoveMouse(Vector2 position)
    {
        _root.SetMousePosition(position);
        Frame();
    }

    private Vector2 DragTitleBy(Vector2 offset, bool release = true)
    {
        var start = TitleBar.Children.OfType<Label>().Single(x => x.HasClass("floating-window-title")).Box.Rect.Center;
        MoveMouse(start);
        _root.SetMouseButton(MouseButtons.Left, true);
        Frame();
        var steps = (int)MathF.Ceiling(offset.Length() / 10);
        for (var i = 1; i <= steps; i++)
        {
            MoveMouse(start + (offset * i / steps));
        }

        if (release)
        {
            _root.SetMouseButton(MouseButtons.Left, false);
            Frame();
        }

        return start;
    }

    [Fact]
    public void AWindowWithoutContentStillHasWorkingChrome()
    {
        var closed = false;
        _window.OnCloseRequested = () => closed = true;
        Frame();

        Assert.Equal("Test window", TitleBar.Children.OfType<Label>().Single(x => x.HasClass("floating-window-title")).Text);
        Assert.Equal("🪟", _window.Children.OfType<Button>().Single().Text);
        Assert.Equal(384, _window.Box.Rect.Width);
        Assert.Equal(new Color(1, 1, 1, 0.2f), _window.ComputedStyle!.BorderLeftColor);

        Click(_root, Control("close"));
        Assert.True(closed);
        Assert.False(_window.IsDeleted);
    }

    [Fact]
    public void ClosingWithoutAHandlerDeletesTheWindow()
    {
        Frame();

        Click(_root, Control("close"));
        Frame();

        Assert.True(_window.IsDeleted);
    }

    [Fact]
    public void ChangingTheChromeKeepsTheContent()
    {
        _window.ChildContent = tree =>
        {
            tree.OpenElement<TextEntry>(0);
            tree.AddAttribute(1, "text", "Initial text");
            tree.CloseElement();
        };
        Frame();

        var content = _window.Children.Single(x => x.HasClass("floating-window-content"));
        var entry = content.Children.OfType<TextEntry>().Single();
        entry.Text = "Edited text";
        _window.Title = "";
        _window.Icon = "🎉";
        _window.ShowMinimizeButton = false;
        Frame();

        Assert.Same(content, _window.Children.Single(x => x.HasClass("floating-window-content")));
        Assert.Same(entry, content.Children.OfType<TextEntry>().Single());
        Assert.Equal("Edited text", entry.Text);
        Assert.Equal("🎉", _window.Children.OfType<Button>().Single().Text);
        Assert.Equal("", TitleBar.Children.OfType<Label>().Single(x => x.HasClass("floating-window-title")).Text);
        Assert.Single(TitleBar.Children.OfType<Button>());
    }

    [Fact]
    public void DraggingTheTitleBarMovesTheWindow()
    {
        Frame();
        var before = _window.Box.Rect.Position;

        DragTitleBy(new Vector2(200, 100));

        Assert.Equal(before + new Vector2(200, 100), _window.Box.Rect.Position);
        Assert.True(_window.HasBeenDragged);
        Assert.False(_window.IsDragging);
        Assert.False(_window.IsThrowing);
    }

    [Fact]
    public void EscapePutsADraggedWindowBack()
    {
        Frame();
        var before = _window.Box.Rect.Position;

        DragTitleBy(new Vector2(200, 100), release: false);
        Assert.True(_window.IsDragging);
        _root.AddButtonEvent(new ButtonEvent("escape", true));
        _root.AddButtonEvent(new ButtonEvent("escape", false));
        Frame();
        _root.SetMouseButton(MouseButtons.Left, false);
        Frame();

        Assert.Equal(before, _window.Box.Rect.Position);
        Assert.False(_window.IsDragging);
        Assert.False(_window.HasBeenDragged);
    }

    [Fact]
    public void MinimizingShrinksToAnIconAndRestoringBringsItBack()
    {
        Frame();
        var before = _window.Box.Rect;

        Click(_root, Control("remove"));
        Assert.True(_window.IsAnimating);
        Frame(30);

        Assert.True(_window.IsMinimized);
        Assert.False(_window.IsAnimating);
        Assert.True(_window.HasClass("minimized"));
        Assert.Equal(new Vector2(64, 64), _window.Box.Rect.Size);
        Assert.True(_window.Box.Rect.Bottom > _root.Box.Rect.Bottom - 64);

        Click(_root, _window.Children.OfType<Button>().Single());
        Frame(30);

        Assert.False(_window.IsMinimized);
        Assert.Equal(before, _window.Box.Rect);
    }

    [Fact]
    public void KeepOnScreenStopsTheWindowLeavingTheScreen()
    {
        _window.KeepOnScreen = true;
        Frame();

        _window.MoveTo(new Vector2(5000, 5000));
        Frame();

        Assert.Equal(_root.Box.Rect.Right, _window.Box.Rect.Right);
        Assert.Equal(_root.Box.Rect.Bottom, _window.Box.Rect.Bottom);
    }

    [Fact]
    public void MarkupMakesAFloatingWindow() => Assert.IsType<FloatingWindow>(Panel.CreateElement("floatingwindow"));
}
