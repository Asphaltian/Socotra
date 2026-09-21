namespace Socotra;

/// <summary>
/// Shows an icon from an icon font by its name, like <c>settings</c> or <c>cancel</c>. It uses the <c>Material Icons</c>
/// family, which isn't included: load it with <see cref="Fonts.Load"/> first, or set <c>font-family</c> to another icon font.
/// </summary>
/// <example>
/// <code>
/// // Load the icon font once at startup, then add icons by name
/// Fonts.Load("fonts/MaterialIcons-Regular.ttf");
/// panel.Add.Icon("settings", "menu-icon");
/// </code>
/// </example>
[StyleSheet.Inline("iconpanel", Styles)]
public class IconPanel : Label
{
    private const string Styles = """
        iconpanel
        {
            font-family: Material Icons;
            text-transform: none;
            letter-spacing: 0px;
        }

        iconpanel, i
        {
            font-family: Material Icons;
            background-position: center;
            background-size: contain;
        }
        """;

    /// <summary>Makes an icon panel with no icon.</summary>
    public IconPanel()
    {
        AddClass("iconpanel");
    }

    /// <summary>Makes an icon panel showing <paramref name="icon"/>, with the given classes.</summary>
    public IconPanel(string? icon, string? classes = null)
        : this()
    {
        Text = icon;
        AddClass(classes);
    }
}

/// <summary>Adds icon panels through <see cref="Panel.Add"/>.</summary>
public static class IconPanelConstructor
{
    /// <summary>Adds an <see cref="IconPanel"/> showing <paramref name="icon"/>, with the given classes.</summary>
    public static IconPanel Icon(this PanelCreator self, string? icon, string? classes = null)
    {
        var control = self.panel.AddChild<IconPanel>(classes);
        if (icon is not null)
        {
            control.Text = icon;
        }

        return control;
    }
}
