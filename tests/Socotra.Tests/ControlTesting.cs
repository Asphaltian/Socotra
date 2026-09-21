namespace Socotra.Tests;

internal static class ControlTesting
{
    public const string Styles = """
        rootpanel { flex-direction: column; align-items: flex-start; font-family: Lato; font-size: 16px; pointer-events: all; }
        """;

    public static RootPanel Root()
    {
        Fonts.Load("Data/Fonts/Lato-Regular.ttf");
        var root = new RootPanel();
        root.StyleSheet.Parse(Styles);
        return root;
    }

    public static RootPanel Root(string styles)
    {
        var root = new RootPanel();
        root.StyleSheet.Parse(styles);
        return root;
    }

    public static void Update(RootPanel root, float deltaTime = 0.016f) => root.Update(new Rect(0, 0, 1920, 1080), deltaTime);

    public static void Mouse(RootPanel root, Panel target, string name)
    {
        target.CreateEvent(new MousePanelEvent(name, target, "mouseleft"));
        Update(root);
    }

    public static void Click(RootPanel root, Panel target) => Mouse(root, target, "onclick");

    public static void Hover(RootPanel root, Panel target) => Mouse(root, target, "onmouseover");

    public static void MoveTo(RootPanel root, Vector2 position)
    {
        root.SetMousePosition(position);
        Update(root);
    }

    public static void Press(RootPanel root, Vector2 position, MouseButtons button = MouseButtons.Left)
    {
        root.SetMousePosition(position);
        root.SetMouseButton(button, true);
        root.SetMouseButton(button, false);
        Update(root);
        Update(root);
    }

    public static void Key(RootPanel root, string button, KeyboardModifiers modifiers = KeyboardModifiers.None)
    {
        root.AddButtonEvent(new ButtonEvent(button, true, modifiers));
        root.AddButtonEvent(new ButtonEvent(button, false, modifiers));
        Update(root);
        Update(root);
    }

    public static void Escape(Panel panel) => panel.DispatchEventImmediate(new PanelEvent("onescape", panel));

    public static Panel Placed(Panel parent, float left, float top, float width, float height)
    {
        var panel = parent.AddChild<Panel>();
        panel.Style.Position = PositionMode.Absolute;
        panel.Style.Left = left;
        panel.Style.Top = top;
        panel.Style.Width = width;
        panel.Style.Height = height;
        return panel;
    }
}

internal sealed class UnscaledRoot : RootPanel
{
    protected override float GetScale(Rect bounds) => 1;
}
