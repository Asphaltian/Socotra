using Microsoft.AspNetCore.Components;

namespace Socotra;

/// <summary>
/// A strip of <see cref="Tab"/>s, one of them selected. It only picks a tab; use <see cref="TabPanel"/> to show a
/// page for each one. Left, Right, Home and End move between the tabs, and the strip scrolls when they don't fit.
/// </summary>
/// <example>
/// <code>
/// // Three tabs, reacting when the user picks one
/// var bar = new TabBar { Parent = header };
/// bar.AddTab("Scene", "landscape");
/// bar.AddTab("Assets", "folder");
/// bar.AddTab("Console", "terminal", count: 3);
/// bar.SelectionChanged += tab => ShowPage(tab?.Text);
/// </code>
/// </example>
[StyleSheet.Inline("tabbar", Styles)]
public class TabBar : Panel
{
    private const string Styles = """
        .tabbar { flex-direction: row; flex-shrink: 0; width: 100%; min-width: 0; overflow-x: scroll; overflow-y: hidden; pointer-events: all; }
        .tabbar > .tab { flex-shrink: 0; align-items: center; white-space: nowrap; pointer-events: all; }
        .tab > .tab-title, .tab > .tab-icon, .tab > .tab-count { pointer-events: none; }
        .tab > .tab-count { flex-shrink: 0; }
        .tab > .tab-icon { width: 1em; height: 1em; flex-shrink: 0; object-fit: contain; }
        .tab > .tab-close { align-items: center; justify-content: center; flex-shrink: 0; pointer-events: all; }
        .tab > .tab-close > * { pointer-events: none; }
        """;

    private Tab? _selected;
    private Tab? _dragged;
    private int _dropIndex;
    private bool _revealSelection;

    /// <summary>Makes an empty tab bar.</summary>
    public TabBar()
    {
        AddClass("tabbar");
        CanDragScroll = false;
    }

    /// <summary>Called after a different tab is selected, with null when the last tab goes.</summary>
    public event Action<Tab?>? SelectionChanged;

    /// <summary>Called when a tab is taken out of the bar, before it's deleted.</summary>
    public event Action<Tab>? TabRemoved;

    /// <summary>Called after a tab moves, with its new position.</summary>
    public event Action<Tab, int>? TabReordered;

    /// <summary>Lets the user drag tabs to change their order. It's off by default.</summary>
    [Parameter]
    public bool AllowReorder { get; set; }

    /// <summary>Hides the text of tabs that have an icon when the tabs get narrower than <see cref="MinimumTextWidth"/>.</summary>
    [Parameter]
    public bool AutoHideText { get; set; } = true;

    /// <summary>How wide each tab can get, in pixels, before <see cref="AutoHideText"/> hides the text. It's 100 by default.</summary>
    [Parameter]
    public float MinimumTextWidth { get; set; } = 100;

    /// <summary>The tabs, in the order they're shown.</summary>
    public IReadOnlyList<Tab> Tabs => [.. Children.OfType<Tab>().Where(static tab => !tab.IsDeleted && !tab.IsDeleting)];

    /// <summary>The selected tab, or null when there are none.</summary>
    public Tab? SelectedTab => _selected;

    /// <summary>Return false from it to stop a tab closing, for example to ask about unsaved changes first.</summary>
    [Parameter]
    public Func<Tab, bool>? CanCloseTab { get; set; }

    internal bool IsReordering => _dragged is not null;

    /// <summary>Adds a tab showing <paramref name="text"/> and the icon named <paramref name="icon"/>. The first tab added is selected.</summary>
    /// <param name="text">The text on the tab.</param>
    /// <param name="icon">The name of the icon, or null for none.</param>
    /// <param name="canClose">Whether the tab has a close button.</param>
    /// <param name="count">A number shown in a badge on the tab, or null for none.</param>
    public Tab AddTab(string? text, string? icon = null, bool canClose = false, int? count = null)
    {
        var tab = AddChild(new Tab { Text = text, Icon = icon, CanClose = canClose, Count = count });
        if (_selected is null)
        {
            SelectTab(tab);
        }

        return tab;
    }

    /// <summary>Selects <paramref name="tab"/>, or nothing when it's null.</summary>
    /// <exception cref="ArgumentException">The tab isn't in this bar.</exception>
    public virtual void SelectTab(Tab? tab)
    {
        if (tab is not null && (tab.Parent != this || tab.IsDeleted || tab.IsDeleting))
        {
            throw new ArgumentException("The tab does not belong to this bar.", nameof(tab));
        }

        if (_selected == tab)
        {
            return;
        }

        SetSelection(tab);
        SelectionChanged?.Invoke(tab);
    }

    /// <summary>
    /// Closes <paramref name="tab"/> the way its close button does. Returns false, leaving it open, when it can't be
    /// closed or <see cref="CanCloseTab"/> says no.
    /// </summary>
    public virtual bool CloseTab(Tab? tab)
    {
        if (tab?.Parent != this || !tab.CanClose || CanCloseTab?.Invoke(tab) == false)
        {
            return false;
        }

        return RemoveTab(tab);
    }

    /// <summary>Takes <paramref name="tab"/> out and deletes it, even if it can't be closed. Returns false if it isn't in this bar.</summary>
    public bool RemoveTab(Tab? tab)
    {
        if (tab?.Parent != this)
        {
            return false;
        }

        var focused = tab.HasFocus;
        CancelReorder();
        tab.Parent = null;
        if (focused)
        {
            _selected?.Focus();
        }

        tab.Delete(true);
        return true;
    }

    /// <summary>Moves <paramref name="tab"/> to <paramref name="index"/>, counting from 0, without changing which tab is selected.</summary>
    /// <exception cref="ArgumentException">The tab isn't in this bar.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> isn't the position of a tab.</exception>
    public void MoveTab(Tab tab, int index)
    {
        var tabs = Tabs;
        if (tab.Parent != this)
        {
            throw new ArgumentException("The tab does not belong to this bar.", nameof(tab));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, tabs.Count);
        if (tabs[index] == tab)
        {
            return;
        }

        SetChildIndex(tab, tabs[index].SiblingIndex);
        TabReordered?.Invoke(tab, index);
    }

    /// <inheritdoc/>
    public override void Tick()
    {
        base.Tick();
        if (!IsVisible || _dragged is not null)
        {
            return;
        }

        var tabs = Tabs.Where(static tab => tab.IsVisible).ToArray();
        var available = Box.RectInner.Width * ScaleFromScreen;
        if (available <= 0 || tabs.Length == 0)
        {
            return;
        }

        var gap = ComputedStyle?.ColumnGap?.Value ?? 0;
        var compact = AutoHideText && (available - (gap * (tabs.Length - 1))) / tabs.Length < MinimumTextWidth;
        foreach (var tab in tabs)
        {
            tab.SetIconOnly(compact);
        }
    }

    /// <summary>Moves the selection with Left, Right, Home and End while a tab has focus.</summary>
    public override void OnButtonTyped(ButtonEvent e)
    {
        if (MovesSelection(e, out var tabs, out var index))
        {
            var next = e.Button switch
            {
                "home" => 0,
                "end" => tabs.Length - 1,
                _ => (index + (e.Button == "right" ? 1 : -1) + tabs.Length) % tabs.Length,
            };
            SelectTab(tabs[next]);
            tabs[next].Focus();
            _revealSelection = true;
            e.StopPropagation = true;
            return;
        }

        base.OnButtonTyped(e);
    }

    internal void SetSelection(Tab? tab)
    {
        _selected = tab;
        _revealSelection = tab is not null;
        foreach (var item in Tabs)
        {
            item.SetClass("selected", item == tab);
        }
    }

    private int InsertionIndex(Vector2 point, Tab? excluded = null) => Tabs.Count(tab => tab != excluded && point.X >= tab.Box.Rect.Center.X);

    internal void BeginReorder(Tab tab)
    {
        if (!AllowReorder || tab.Parent != this)
        {
            return;
        }

        _dragged = tab;
        tab.AddClass("dragging");
    }

    internal void UpdateReorder(Vector2 point)
    {
        if (_dragged is null)
        {
            return;
        }

        ClearDropMarkers();
        if (!AllowReorder || !Box.Rect.IsInside(point))
        {
            _dropIndex = -1;
            return;
        }

        _dropIndex = InsertionIndex(point, _dragged);
        var others = Tabs.Where(tab => tab != _dragged).ToArray();
        if (others.Length == 0)
        {
            return;
        }

        if (_dropIndex < others.Length)
        {
            others[_dropIndex].AddClass("drop-before");
        }
        else
        {
            others[^1].AddClass("drop-after");
        }
    }

    internal void EndReorder(Vector2 point)
    {
        if (_dragged is null)
        {
            return;
        }

        UpdateReorder(point);
        var tab = _dragged;
        var index = _dropIndex;
        CancelReorder();
        if (index >= 0 && tab.Parent == this)
        {
            MoveTab(tab, index);
        }
    }

    internal void CancelReorder()
    {
        _dragged?.RemoveClass("dragging");
        _dragged = null;
        ClearDropMarkers();
    }

    internal override void FinalLayout(Vector2 offset)
    {
        base.FinalLayout(offset);
        if (!_revealSelection || _selected?.Parent != this || !_selected.IsVisible || _selected.Box.Rect.Width <= 0)
        {
            return;
        }

        _revealSelection = false;
        ScrollIntoView(_selected.Box.Rect);
    }

    /// <summary>Picks another tab when the selected one is taken out, then calls <see cref="TabRemoved"/>.</summary>
    protected override void OnChildRemoved(Panel child)
    {
        base.OnChildRemoved(child);
        if (IsDeleting || IsDeleted || child is not Tab tab)
        {
            return;
        }

        CancelReorder();
        if (_selected == tab)
        {
            var remaining = Tabs;
            SelectTab(remaining.Count == 0 ? null : remaining[Math.Clamp(tab.SiblingIndex, 0, remaining.Count - 1)]);
        }

        TabRemoved?.Invoke(tab);
    }

    private bool MovesSelection(ButtonEvent e, out Tab[] tabs, out int index)
    {
        tabs = [.. Tabs.Where(static tab => tab.IsVisible)];
        index = Array.FindIndex(tabs, static tab => tab.HasFocus);
        return index >= 0 && e.Button is "left" or "right" or "home" or "end";
    }

    private void ClearDropMarkers()
    {
        foreach (var tab in Tabs)
        {
            tab.RemoveClass("drop-before");
            tab.RemoveClass("drop-after");
        }
    }
}
