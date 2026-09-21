namespace Socotra;

/// <summary>A panel registered with a <see cref="DockHost"/>. It keeps its state while it's hidden, closed or moved.</summary>
public sealed class DockItem
{
    private Func<Panel>? _create;

    internal DockItem(string id, string title, Func<Panel> create, bool canClose, string? icon)
        : this(id, title, (Panel?)null, canClose, icon)
    {
        _create = create;
    }

    internal DockItem(string id, string title, Panel? content, bool canClose, string? icon)
    {
        Id = id;
        Title = title;
        Icon = icon;
        Content = content;
        CanClose = canClose;
        Container = new Panel();
        Container.AddClass("dock-content");
        if (content is not null)
        {
            content.Parent = Container;
        }
    }

    /// <summary>The ID it was registered under, which saved arrangements use.</summary>
    public string Id { get; }

    /// <summary>The name shown on the panel's tab.</summary>
    public string Title { get; }

    /// <summary>The name of the icon shown before <see cref="Title"/> on the panel's tab, if any.</summary>
    public string? Icon { get; }

    /// <summary>The panel, or null if it was registered to be made later and hasn't been opened yet.</summary>
    public Panel? Content { get; private set; }

    /// <summary>Whether the user can close the panel.</summary>
    public bool CanClose { get; }

    internal Panel Container { get; }

    internal bool IsAlive => Content is null || (!Content.IsDeleted && !Content.IsDeleting);

    internal void EnsureContent()
    {
        if (Content is not null || _create is null)
        {
            return;
        }

        var content = _create() ?? throw new InvalidOperationException("The dock factory returned null.");
        if (content.IsDeleted || content.IsDeleting || content is RootPanel || Container.AncestorsAndSelf.Contains(content) || content.Ancestors.OfType<DockHost>().Any())
        {
            throw new InvalidOperationException("The dock factory returned content already owned by a docking host or an invalid panel.");
        }

        Content = content;
        content.Parent = Container;
        _create = null;
    }
}
