using Microsoft.AspNetCore.Components;

namespace Socotra;

/// <summary>
/// A scrolling list for lots of items, one per row. Every row is <see cref="ItemHeight"/> tall, and only the
/// rows in view have cells.
/// </summary>
/// <example>
/// <code>
/// // A list of 1000 rows, 40 pixels each. Give it a height in your SCSS so it has something to scroll in
/// var list = panel.AddChild&lt;VirtualList&gt;("saves");
/// list.ItemHeight = 40;
/// list.OnCreateCell = (cell, item) =&gt; cell.Add.Label($"Save {item}");
/// list.Items = Enumerable.Range(1, 1000).Cast&lt;object&gt;().ToList();
/// </code>
/// </example>
public sealed class VirtualList : BaseVirtualPanel
{
    internal VerticalListLayout Layout { get; } = new();

    /// <summary>How tall each row is, in CSS pixels.</summary>
    [Parameter]
    public float ItemHeight
    {
        get => Layout.ItemHeight;
        set => Layout.ItemHeight = value;
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
