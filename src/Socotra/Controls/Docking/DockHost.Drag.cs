using System.Diagnostics.CodeAnalysis;

namespace Socotra;

internal readonly record struct DockDropTarget(string? RelativeTo, DockPosition Position, int TabIndex = -1, float Fraction = 0.5f);

public partial class DockHost
{
    private readonly Dictionary<DockGroup, Panel> _sections = [];
    private readonly Dictionary<DockPosition, Panel> _groupGuides = [];
    private readonly Dictionary<DockPosition, Panel> _rootGuides = [];
    private string? _dragId;
    private DockDropTarget? _drop;
    private Panel? _targets;
    private Panel? _emptyTarget;

    internal void CancelDrag()
    {
        if (_dragId is not null && _tabs.TryGetValue(_dragId, out var tab) && !tab.IsDeleted)
        {
            tab.RemoveClass("dragging");
        }

        _dragId = null;
        _drop = null;
        HideDockTargets();
    }

    internal DockDropTarget? UpdateDockTargets(Vector2? point, string draggedId)
    {
        if (IsDeleted || IsDeleting || !IsVisible)
        {
            HideDockTargets();
            return null;
        }

        CreateTargets();
        _targets.Style.Display = DisplayMode.Flex;
        _preview.Style.Display = DisplayMode.None;
        foreach (var guide in _groupGuides.Values.Concat(_rootGuides.Values))
        {
            guide.Style.Display = DisplayMode.None;
            guide.RemoveClass("hovered");
        }

        var bounds = _workspace.Box.Rect;
        if (point.HasValue && (!Contains(bounds, point.Value) || !IsInsideVisibleContent(point.Value)))
        {
            point = null;
        }

        DockGroup? hovered = null;
        foreach (var (node, view) in _views)
        {
            if (node is not DockGroup group)
            {
                continue;
            }

            if (!_sections.TryGetValue(group, out var section))
            {
                _sections.Add(group, section = _targets.Add.Panel("dock-section"));
            }

            PositionOverlay(section, view.Box.Rect);
            section.Style.Display = CanDrop(group, draggedId) ? DisplayMode.Flex : DisplayMode.None;
            var inside = point.HasValue && Contains(view.Box.Rect, point.Value);
            section.SetClass("hovered", inside);
            if (inside)
            {
                hovered = group;
            }
        }

        foreach (var group in _sections.Keys.Where(x => !_views.ContainsKey(x)).ToArray())
        {
            _sections[group].Delete(true);
            _sections.Remove(group);
        }

        _emptyTarget.Style.Display = _layout.Root is null ? DisplayMode.Flex : DisplayMode.None;
        PositionOverlay(_emptyTarget, bounds);

        DockDropTarget? result = null;
        Rect preview = default;
        var size = 30 / ScaleFromScreen;
        var pitch = size + (5 / ScaleFromScreen);
        if ((hovered is not null || (_layout.Root is null && point.HasValue)) && CanDrop(hovered, draggedId))
        {
            var region = hovered is null ? bounds : _views[hovered].Box.Rect;
            var guideSize = MathF.Min(size, MathF.Min(region.Width, region.Height) / 3.5f);
            var guidePitch = guideSize + (4 / ScaleFromScreen);
            foreach (var (position, guide) in _groupGuides)
            {
                if (hovered is null && position != DockPosition.Center)
                {
                    continue;
                }

                var rect = new Rect(region.Center + (Direction(position) * guidePitch) - new Vector2(guideSize * 0.5f), new Vector2(guideSize));
                PositionOverlay(guide, rect);
                guide.Style.Display = DisplayMode.Flex;
                if (!point.HasValue || !Contains(rect, point.Value))
                {
                    continue;
                }

                guide.AddClass("hovered");
                result = new DockDropTarget(hovered?.Selected, position);
                preview = PreviewBounds(region, position);
            }
        }

        if (_layout.Root is not null && CanDrop(_layout.Root as DockGroup, draggedId))
        {
            foreach (var (position, guide) in _rootGuides)
            {
                var center = bounds.Center + (Direction(position) * ((bounds.Size * 0.5f) - new Vector2(pitch)));
                var rect = new Rect(center - new Vector2(size * 0.5f), new Vector2(size));
                PositionOverlay(guide, rect);
                guide.Style.Display = DisplayMode.Flex;
                if (!point.HasValue || !Contains(rect, point.Value))
                {
                    continue;
                }

                foreach (var groupGuide in _groupGuides.Values)
                {
                    groupGuide.RemoveClass("hovered");
                }

                guide.AddClass("hovered");
                result = new DockDropTarget(null, position);
                preview = PreviewBounds(bounds, position);
            }
        }

        if (result is null && hovered is not null && point.HasValue && CanDrop(hovered, draggedId))
        {
            var tabs = ((GroupView)_views[hovered]).Tabs.Box.Rect;
            if (Contains(tabs, point.Value))
            {
                int index = 0;
                var insertion = tabs.Left;
                foreach (var id in hovered.Items)
                {
                    if (id == draggedId)
                    {
                        continue;
                    }

                    var tab = _tabs[id].Box.Rect;
                    if (point.Value.X < tab.Center.X)
                    {
                        break;
                    }

                    insertion = tab.Right;
                    index++;
                }

                result = new DockDropTarget(hovered.Selected, DockPosition.Center, index);
                var markerWidth = MathF.Min(3 / ScaleFromScreen, tabs.Width);
                preview = new Rect(Math.Clamp(insertion, tabs.Left, tabs.Right - markerWidth), tabs.Top, markerWidth, tabs.Height);
            }
        }

        if (result.HasValue)
        {
            PositionOverlay(_preview, preview);
            _preview.Style.Display = DisplayMode.Flex;
        }

        return result;
    }

    private static bool CanDrop(DockGroup? group, string draggedId) => group is null || group.Items.Count > 1 || group.Selected != draggedId;

    private static Vector2 Direction(DockPosition position) => position switch
    {
        DockPosition.Left => new Vector2(-1, 0),
        DockPosition.Right => new Vector2(1, 0),
        DockPosition.Top => new Vector2(0, -1),
        DockPosition.Bottom => new Vector2(0, 1),
        _ => Vector2.Zero,
    };

    private static bool Contains(Rect rect, Vector2 point) => point.X >= rect.Left && point.Y >= rect.Top && point.X < rect.Right && point.Y < rect.Bottom;

    private static Rect PreviewBounds(Rect bounds, DockPosition position)
    {
        var size = bounds.Size;
        var origin = bounds.Position;
        if (position is DockPosition.Left or DockPosition.Right)
        {
            size.X *= 0.5f;
        }

        if (position is DockPosition.Top or DockPosition.Bottom)
        {
            size.Y *= 0.5f;
        }

        if (position == DockPosition.Right)
        {
            origin.X += size.X;
        }

        if (position == DockPosition.Bottom)
        {
            origin.Y += size.Y;
        }

        return new Rect(origin, size);
    }

    private static Panel CreateGuide(Panel targets, DockPosition position, bool root)
    {
        var name = position.ToString().ToLowerInvariant();
        var guide = targets.Add.Panel($"dock-guide {(root ? "dock-root-guide" : "dock-group-guide")}-{name}");
        var frame = guide.Add.Panel("dock-guide-frame");
        frame.Add.Panel($"dock-guide-fill {name}");
        if (position == DockPosition.Center)
        {
            frame.Add.Icon("tab", "dock-guide-icon");
        }

        return guide;
    }

    private void HideDockTargets()
    {
        if (_targets is { IsDeleted: false })
        {
            _targets.Style.Display = DisplayMode.None;
        }

        if (!_preview.IsDeleted)
        {
            _preview.Style.Display = DisplayMode.None;
        }
    }

    [MemberNotNull(nameof(_targets), nameof(_emptyTarget))]
    private void CreateTargets()
    {
        if (_targets is not null && _emptyTarget is not null)
        {
            return;
        }

        _targets = Add.Panel("dock-targets");
        _emptyTarget = _targets.Add.Panel("dock-section");
        _preview.Parent = _targets;
        foreach (var position in Enum.GetValues<DockPosition>())
        {
            _groupGuides.Add(position, CreateGuide(_targets, position, false));
            if (position != DockPosition.Center)
            {
                _rootGuides.Add(position, CreateGuide(_targets, position, true));
            }
        }
    }

    private bool IsInsideVisibleContent(Vector2 point)
    {
        foreach (var panel in AncestorsAndSelf)
        {
            var clip = panel.ContentClipRect;
            if (panel is RootPanel && !Contains(panel.Box.Rect, point))
            {
                return false;
            }

            if (panel.ComputedStyle is not { } style)
            {
                return false;
            }

            if ((style.OverflowX ?? OverflowMode.Visible) != OverflowMode.Visible && (point.X < clip.Left || point.X >= clip.Right))
            {
                return false;
            }

            if ((style.OverflowY ?? OverflowMode.Visible) != OverflowMode.Visible && (point.Y < clip.Top || point.Y >= clip.Bottom))
            {
                return false;
            }
        }

        return true;
    }

    private void PositionOverlay(Panel panel, Rect rect)
    {
        var local = (rect.Position - Box.Rect.Position) * ScaleFromScreen;
        panel.Style.Left = local.X;
        panel.Style.Top = local.Y;
        panel.Style.Width = rect.Width * ScaleFromScreen;
        panel.Style.Height = rect.Height * ScaleFromScreen;
    }

    private void BeginDrag(string id)
    {
        CancelDrag();
        if (!IsOpen(id))
        {
            return;
        }

        _dragId = id;
        _tabs[id].AddClass("dragging");
        UpdateDockTargets(null, id);
    }

    private void UpdateDrag(Vector2 point)
    {
        if (_dragId is not null)
        {
            _drop = UpdateDockTargets(point, _dragId);
        }
    }

    private void EndDrag(Vector2 point)
    {
        if (_dragId is null)
        {
            return;
        }

        UpdateDrag(point);
        var id = _dragId;
        var drop = _drop;
        CancelDrag();
        if (drop is { } target)
        {
            Dock(id, target.RelativeTo, target.Position, target.Fraction, target.TabIndex);
        }
    }
}
