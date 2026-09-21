namespace Socotra;

/// <summary>
/// A workspace of panels arranged in areas with draggable splitters between them, where each area holds one or more
/// panels with a tab for each. Register your panels once, then open and arrange them with <see cref="Dock"/>. The user
/// can drag a tab onto the guides that appear to move its panel to another area, split it off to one side, or drop it
/// among another area's tabs. A panel keeps its state when it's hidden, closed or moved. Save the arrangement with
/// <see cref="State"/> and put it back later. In markup it's <c>&lt;dockhost&gt;</c>.
/// </summary>
/// <example>
/// <code>
/// // A scene view with the hierarchy on its left and the inspector on its right
/// var dock = new DockHost { Parent = editor };
/// dock.Register("scene", "Scene", sceneView, canClose: false);
/// dock.Register("hierarchy", "Hierarchy", () => new HierarchyPanel());
/// dock.Register("inspector", "Inspector", () => new InspectorPanel());
/// dock.Dock("scene");
/// dock.Dock("hierarchy", "scene", DockPosition.Left, 0.25f);
/// dock.Dock("inspector", "scene", DockPosition.Right, 0.3f);
/// </code>
/// </example>
[StyleSheet.Inline("dockhost", Styles)]
public partial class DockHost : Panel
{
    private const string Styles = """
        .dockhost
        {
            position: relative;
            flex-grow: 1;
            min-width: 0;
            min-height: 0;
            overflow: hidden;
            pointer-events: all;
            color: #c6ccd6;
            background-color: #101319;
            font-family: Inter;
            font-size: 12px;

            .dock-parking { display: none; }
            .dock-workspace, .dock-group, .dock-split, .dock-branch, .dock-body, .dock-content
            {
                flex-grow: 1;
                min-width: 0;
                min-height: 0;
                overflow: hidden;
            }
            .dock-workspace { width: 100%; height: 100%; }
            .dock-group { flex-direction: column; background-color: #20242c; }
            .dock-body { position: relative; }
            .dock-content { width: 100%; height: 100%; }
            .dock-content > * { flex-grow: 1; min-width: 0; min-height: 0; }
            .dock-branch { flex-basis: 0px; }
            .dock-splitter
            {
                width: 5px;
                flex-shrink: 0;
                background-color: #101319;
                cursor: ew-resize;
                &:hover { background-color: #4389e8; }
            }
            .dock-split.vertical
            {
                flex-direction: column;
                > .dock-splitter { height: 5px; width: 100%; cursor: ns-resize; }
            }
            .dock-split.resizing > .dock-splitter { background-color: #4389e8; }
            .dock-preview
            {
                position: absolute;
                z-index: 2;
                pointer-events: none;
                background-color: #499cff55;
                border: 2px solid #96d3ff;
            }
            .dock-targets { position: absolute; left: 0; top: 0; width: 100%; height: 100%; z-index: 100; }
            .dock-targets, .dock-targets * { pointer-events: none; }
            .dock-section
            {
                position: absolute;
                z-index: 0;
                background-color: #2875d82b;
                border: 1px solid #469aee99;
                &.hovered { background-color: #2875d842; border-color: #79bfff; }
            }
            .dock-guide
            {
                position: absolute;
                z-index: 3;
                padding: 5px;
                border: 1px solid #88bded;
                border-radius: 3px;
                background-color: #123e69;
                box-shadow: 0 2px 8px #0009;
                &.hovered { background-color: #2388df; border-color: white; }
            }
            .dock-guide-frame { position: relative; width: 100%; height: 100%; border: 1px solid #a0cefa; }
            .dock-guide-fill
            {
                position: absolute;
                background-color: #9bd5ff;
                &.left { left: 0; top: 0; width: 50%; height: 100%; }
                &.right { right: 0; top: 0; width: 50%; height: 100%; }
                &.top { left: 0; top: 0; width: 100%; height: 50%; }
                &.bottom { left: 0; bottom: 0; width: 100%; height: 50%; }
                &.center { display: none; }
            }
            .dock-guide-icon { position: absolute; left: 0; top: 0; width: 100%; height: 100%; font-size: 16px; color: #d4edff; }
            .dock-empty { margin: auto; color: #8993a3; pointer-events: none; }
        }
        .style-light .dockhost
        {
            color: #303a48;
            background-color: #bdc7d6;
            .dock-group, .dock-tab.selected { background-color: #f4f6fa; color: #253249; }
            .dock-tabs { background-color: #bdc7d6; }
            .dock-tab { color: #586578; background-color: #f4f6fa; }
            .dock-tab:hover { background-color: #eaf0f8; }
            .dock-tab.selected
            {
                border-color: #c2cddd;
                border-top-color: transparent;
                border-bottom-color: #f4f6fa;
                background-color: #f4f6fa;
            }
            .dock-tab:focus { border-top-color: transparent; }
            .dock-tab-action:hover { background-color: #233c6014; color: #182c48; }
            .dock-splitter { background-color: #bdc7d6; }
        }
        """;

    private readonly DockLayout _layout = new();
    private readonly Dictionary<string, DockItem> _items = new(StringComparer.Ordinal);
    private readonly Dictionary<DockNode, Panel> _views = [];
    private readonly Dictionary<string, DockTab> _tabs = new(StringComparer.Ordinal);
    private readonly Panel _parking;
    private readonly Panel _workspace;
    private readonly Panel _preview;
    private readonly Label _empty;

    /// <summary>Makes an empty workspace.</summary>
    public DockHost()
    {
        AddClass("dockhost");
        CanDragScroll = false;
        _parking = Add.Panel("dock-parking");
        _workspace = Add.Panel("dock-workspace");
        _empty = _workspace.Add.Label("No panels open", "dock-empty");
        _preview = Add.Panel("dock-preview");
        _preview.Style.Display = DisplayMode.None;
        _layout.Changed += OnLayoutChanged;
    }

    /// <summary>Called after the arrangement changes. The panels themselves are kept, not made again.</summary>
    public event Action? LayoutChanged;

    /// <summary>Every registered panel, including closed ones.</summary>
    public IReadOnlyCollection<DockItem> Items => _items.Values;

    /// <summary>
    /// The arrangement as JSON: where each open panel is, which is showing in each area, and where the splitters are.
    /// Closed panels are left out. Setting it does the same as <see cref="RestoreState"/>.
    /// </summary>
    public string State
    {
        get => _layout.Save();
        set => RestoreState(value);
    }

    /// <summary>
    /// Registers <paramref name="content"/>, closed, under <paramref name="id"/>. The ID is what <see cref="State"/> saves,
    /// so keep it the same between runs. Pass <paramref name="canClose"/> as false for panels that must stay open once shown.
    /// A panel can only be registered once.
    /// </summary>
    /// <exception cref="ArgumentException">The ID is empty or already registered, or the panel is already in a dock host or can't be moved here.</exception>
    /// <exception cref="InvalidOperationException">The host is being deleted.</exception>
    public DockItem Register(string id, string? title, Panel content, bool canClose = true, string? icon = null)
    {
        CheckAlive();
        ArgumentNullException.ThrowIfNull(content);
        CheckNewId(id);
        if (content.IsDeleting || content.IsDeleted || content is RootPanel || AncestorsAndSelf.Contains(content) || content.Ancestors.OfType<DockHost>().Any())
        {
            throw new ArgumentException("The content cannot be owned by this docking host.", nameof(content));
        }

        return AddItem(new DockItem(id, title ?? id, content, canClose, icon));
    }

    /// <summary>
    /// Registers a panel under <paramref name="id"/> without making it yet: <paramref name="create"/> is called the first
    /// time the panel is opened, and the panel is kept after that. Otherwise the same as the other <c>Register</c>.
    /// </summary>
    /// <exception cref="ArgumentException">The ID is empty or already registered.</exception>
    /// <exception cref="InvalidOperationException">The host is being deleted.</exception>
    public DockItem Register(string id, string? title, Func<Panel> create, bool canClose = true, string? icon = null)
    {
        CheckAlive();
        ArgumentNullException.ThrowIfNull(create);
        CheckNewId(id);
        return AddItem(new DockItem(id, title ?? id, create, canClose, icon));
    }

    /// <summary>The registered panel with <paramref name="id"/>, open or closed, or null.</summary>
    public DockItem? Find(string? id) => id is not null && _items.TryGetValue(id, out var item) ? item : null;

    /// <summary>Whether the panel with <paramref name="id"/> is open.</summary>
    public bool IsOpen(string id) => _layout.FindGroup(id) is not null;

    /// <summary>
    /// Opens the registered panel <paramref name="id"/>, or moves it if it's open, and shows it. Put it with the panel
    /// <paramref name="relativeTo"/>, or leave that null to use the whole workspace. <paramref name="position"/> says where:
    /// <see cref="DockPosition.Center"/> joins that panel's area, the others split off a new area taking
    /// <paramref name="fraction"/> of the space, from 0.05 to 0.95. <paramref name="tabIndex"/> places it among the panels
    /// already in the area; -1 puts it last.
    /// </summary>
    /// <exception cref="ArgumentException">The panel isn't registered, or <paramref name="relativeTo"/> isn't open.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="fraction"/> or <paramref name="tabIndex"/> is out of range.</exception>
    /// <exception cref="InvalidOperationException">The host is being deleted, or the areas are nested too deep.</exception>
    public void Dock(string id, string? relativeTo = null, DockPosition position = DockPosition.Center, float fraction = 0.5f, int tabIndex = -1)
    {
        CheckAlive();
        if (Find(id) is not { IsAlive: true } item)
        {
            throw new ArgumentException("The panel is not registered or has been deleted.", nameof(id));
        }

        var candidate = new DockLayout();
        candidate.Restore(_layout.Save(), _items.Keys);
        candidate.Dock(id, relativeTo, position, fraction, tabIndex);
        item.EnsureContent();
        CancelDrag();
        _layout.Dock(id, relativeTo, position, fraction, tabIndex);
    }

    /// <summary>Closes the panel <paramref name="id"/> without deleting it, so it can be opened again as it was. Panels registered with <c>canClose</c> false stay open. Returns false if nothing closed.</summary>
    /// <exception cref="InvalidOperationException">The host is being deleted.</exception>
    public bool Close(string id)
    {
        CheckAlive();
        if (Find(id)?.CanClose != true)
        {
            return false;
        }

        CancelDrag();
        return _layout.Close(id);
    }

    /// <summary>Shows the open panel <paramref name="id"/> in its area, without moving it. Returns false if it isn't open or is already showing.</summary>
    /// <exception cref="InvalidOperationException">The host is being deleted.</exception>
    public bool Activate(string id)
    {
        CheckAlive();
        return _layout.Activate(id);
    }

    /// <summary>
    /// Puts back an arrangement saved from <see cref="State"/>. It only changes anything if the JSON is valid, every panel
    /// in it is registered, and every open panel that can't be closed stays open. Returns whether it did.
    /// </summary>
    /// <exception cref="InvalidOperationException">The host is being deleted.</exception>
    public bool RestoreState(string json)
    {
        CheckAlive();
        var candidate = new DockLayout();
        if (!candidate.Restore(json, _items.Keys) || _items.Values.Any(x => !x.CanClose && IsOpen(x.Id) && candidate.FindGroup(x.Id) is null))
        {
            return false;
        }

        foreach (var item in _items.Values.Where(x => candidate.FindGroup(x.Id) is not null))
        {
            item.EnsureContent();
        }

        CancelDrag();
        return _layout.Restore(json, _items.Keys);
    }

    /// <inheritdoc/>
    public override void Tick()
    {
        base.Tick();
        foreach (var item in _items.Values.Where(x => !x.IsAlive).ToArray())
        {
            CancelDrag();
            _items.Remove(item.Id);
            if (_tabs.Remove(item.Id, out var tab))
            {
                tab.Delete(true);
            }

            item.Container.Delete(true);
            _layout.Close(item.Id);
        }

        if (_dragId is not null && FindRootPanel()?.Input.MousePosition is null)
        {
            CancelDrag();
        }
    }

    /// <inheritdoc/>
    public override void OnDeleted()
    {
        _layout.Changed -= OnLayoutChanged;
        LayoutChanged = null;
        _items.Clear();
        _tabs.Clear();
        _views.Clear();
        base.OnDeleted();
    }

    private void CheckNewId(string id)
    {
        new DockLayout().Dock(id);
        if (_items.ContainsKey(id))
        {
            throw new ArgumentException("This panel ID is already registered.", nameof(id));
        }
    }

    private DockItem AddItem(DockItem item)
    {
        item.Container.Parent = _parking;
        _items.Add(item.Id, item);
        return item;
    }

    private void CheckAlive()
    {
        if (IsDeleted || AncestorsAndSelf.Any(x => x.IsDeleting))
        {
            throw new InvalidOperationException("The docking host is being deleted.");
        }
    }

    private void OnLayoutChanged()
    {
        if (IsDeleted || IsDeleting)
        {
            return;
        }

        var used = new HashSet<DockNode>();
        if (_layout.Root is not null)
        {
            BuildView(_layout.Root, _workspace, used);
        }

        _empty.Style.Display = _layout.Root is null ? DisplayMode.Flex : DisplayMode.None;
        foreach (var item in _items.Values)
        {
            if (IsOpen(item.Id))
            {
                continue;
            }

            item.Container.Parent = _parking;
            if (_tabs.TryGetValue(item.Id, out var tab))
            {
                tab.Parent = _parking;
            }
        }

        foreach (var node in _views.Keys.Where(x => !used.Contains(x)).ToArray())
        {
            _views[node].Delete(true);
            _views.Remove(node);
        }

        LayoutChanged?.Invoke();
    }

    private void BuildView(DockNode node, Panel parent, HashSet<DockNode> used)
    {
        used.Add(node);
        if (!_views.TryGetValue(node, out var view))
        {
            view = node is DockSplit split ? new SplitView(this, split) : new GroupView(this);
            _views.Add(node, view);
        }

        view.Parent = parent;
        if (node is DockSplit branch)
        {
            var splitView = (SplitView)view;
            BuildView(branch.First, splitView.First, used);
            BuildView(branch.Second, splitView.Second, used);
            splitView.UpdateFraction();
        }
        else if (node is DockGroup group)
        {
            var region = (GroupView)view;
            region.SetClass("single-tab", group.Items.Count == 1);
            for (int i = 0; i < group.Items.Count; i++)
            {
                var id = group.Items[i];
                var item = _items[id];
                item.EnsureContent();
                if (!_tabs.TryGetValue(id, out var tab))
                {
                    _tabs.Add(id, tab = new DockTab(this, item));
                }

                tab.Parent = region.Tabs;
                region.Tabs.SetChildIndex(tab, i);
                tab.SetClass("selected", group.Selected == id);
                item.Container.Parent = region.Body;
                item.Container.Style.Display = group.Selected == id ? DisplayMode.Flex : DisplayMode.None;
            }

            region.Tabs.SetSelection(group.Selected is { } selected ? _tabs[selected] : null);
        }
    }
}
