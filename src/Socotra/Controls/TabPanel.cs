namespace Socotra;

/// <summary>
/// A <see cref="TabBar"/> with a page under it for each tab; picking a tab shows its page. Pages you're not looking at
/// keep their state, and closing a tab deletes its page.
/// </summary>
/// <example>
/// <code>
/// // Two pages, the second one closable
/// var tabs = new TabPanel { Parent = window };
/// tabs.AddTab("General", generalPage, "settings");
/// tabs.AddTab("Notes", new TextEntry { Multiline = true }, "notes", canClose: true);
/// </code>
/// </example>
[StyleSheet.Inline("tabpanel", Styles)]
public class TabPanel : Panel
{
    private const string Styles = """
        .tabpanel { flex-direction: column; flex-grow: 1; min-width: 0; min-height: 0; }
        .tabpanel > .tab-body, .tab-page { flex-grow: 1; min-width: 0; min-height: 0; }
        .tab-page > * { flex-grow: 1; min-width: 0; min-height: 0; }
        """;

    private readonly Dictionary<Tab, Panel> _pages = [];

    /// <summary>Makes an empty tab panel.</summary>
    public TabPanel()
    {
        AddClass("tabpanel");
        TabBar = AddChild(new TabBar());
        Body = Add.Panel("tab-body");
        TabBar.SelectionChanged += _ => UpdatePages();
        TabBar.TabRemoved += tab =>
        {
            if (_pages.Remove(tab, out var page))
            {
                page.Delete(true);
            }
        };
    }

    /// <summary>The tab bar. Turn on reordering, handle closing and listen for the selection changing here.</summary>
    public TabBar TabBar { get; }

    /// <summary>The panel holding the pages.</summary>
    public Panel Body { get; }

    /// <summary>Adds a tab showing <paramref name="text"/>, with <paramref name="content"/> as its page. The tab panel owns the page from then on.</summary>
    /// <param name="text">The text on the tab.</param>
    /// <param name="content">The page's content.</param>
    /// <param name="icon">The name of an icon for the tab, or null for none.</param>
    /// <param name="canClose">Whether the tab has a close button.</param>
    /// <param name="count">A number shown in a badge on the tab, or null for none.</param>
    /// <exception cref="ArgumentException"><paramref name="content"/> is deleted, a root panel, this tab panel or one of its ancestors, or already in a tab panel.</exception>
    public Tab AddTab(string? text, Panel content, string? icon = null, bool canClose = false, int? count = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (content.IsDeleted || content.IsDeleting || content is RootPanel || AncestorsAndSelf.Contains(content) || content.Ancestors.OfType<TabPanel>().Any())
        {
            throw new ArgumentException("The content is invalid or already belongs to a tab panel.", nameof(content));
        }

        var page = Body.Add.Panel("tab-page");
        content.Parent = page;
        var tab = TabBar.AddTab(text, icon, canClose, count);
        _pages.Add(tab, page);
        UpdatePages();
        return tab;
    }

    private void UpdatePages()
    {
        foreach (var (tab, page) in _pages)
        {
            page.Style.Display = tab == TabBar.SelectedTab ? DisplayMode.Flex : DisplayMode.None;
        }
    }
}
