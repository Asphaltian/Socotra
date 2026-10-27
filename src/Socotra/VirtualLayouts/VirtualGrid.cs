using Microsoft.AspNetCore.Components;

namespace Socotra;

/// <summary>
/// <c>VirtualGrid</c> is a scrolling grid for lots of items. It fits as many <see cref="ItemSize"/> columns across as it
/// can, and then stretches the cells to fill the width while keeping their shape. Only the cells that are in view
/// exist.
/// </summary>
public sealed class VirtualGrid : BaseVirtualPanel
{
    internal GridLayout Layout { get; } = new();

    /// <summary>How big each cell is before it stretches to fill the width, in CSS pixels.</summary>
    [Parameter]
    public Vector2 ItemSize
    {
        get => new(Layout.ItemWidth, Layout.ItemHeight);
        set
        {
            Layout.ItemWidth = value.X;
            Layout.ItemHeight = value.Y;
        }
    }

    /// <inheritdoc/>
    protected override void UpdateLayoutSpacing(Vector2 spacing) => Layout.Spacing = spacing;

    /// <inheritdoc/>
    protected override bool UpdateLayout() => Layout.Update(Box, ScaleFromScreen, ScrollOffset.Y * ScaleFromScreen);

    /// <inheritdoc/>
    protected override void GetVisibleRange(out int first, out int pastEnd) => Layout.GetVisibleRange(out first, out pastEnd);

    /// <inheritdoc/>
    protected override void PositionPanel(int index, Panel panel) => Layout.Position(index, panel);

    /// <inheritdoc/>
    protected override float GetTotalHeight(int itemCount) => Layout.GetHeight(itemCount);
}
