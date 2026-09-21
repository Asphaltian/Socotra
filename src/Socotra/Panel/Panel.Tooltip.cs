using Microsoft.AspNetCore.Components;

namespace Socotra;

public partial class Panel
{
    /// <summary>Text shown in a tooltip while the mouse rests on this panel or anything inside it that has no tooltip of its own.</summary>
    [Parameter]
    public string? Tooltip { get; set; }

    /// <summary>Classes added to this panel's tooltip, for styling it. Every tooltip has the <c>tooltip</c> class.</summary>
    [Parameter]
    public string? TooltipClass { get; set; }

    /// <summary>
    /// Fills in this panel's tooltip with anything you like: labels, images, other panels. It's called with the tooltip
    /// panel each time it's about to show. If <see cref="Tooltip"/> is set too, its text is already in there, first.
    /// </summary>
    /// <example>
    /// <code>
    /// // A tooltip with a title and a picture
    /// item.Tooltip = "Potion";
    /// item.OnTooltip = tooltip => tooltip.Add.Image("assets/items/potion.png", "preview");
    /// </code>
    /// </example>
    [Parameter]
    public Action<Panel>? OnTooltip { get; set; }

    /// <summary>Whether this panel shows a tooltip: by default, when <see cref="Tooltip"/> or <see cref="OnTooltip"/> is set. Override it to return true when you override <see cref="CreateTooltipPanel"/>.</summary>
    public virtual bool HasTooltip => !string.IsNullOrWhiteSpace(Tooltip) || OnTooltip is not null;

    internal Panel? BuildTooltip() => CreateTooltipPanel();

    internal void UpdateTooltip(Panel tooltipPanel)
    {
        if (OnTooltip is null && tooltipPanel.ChildrenCount == 1 && tooltipPanel.Children[0] is Label textPanel)
        {
            textPanel.Text = Tooltip;
        }
    }

    /// <summary>
    /// Makes the tooltip panel. Override it to make a tooltip of your own; if you don't set <see cref="Tooltip"/>, also
    /// override <see cref="HasTooltip"/> to return true. Return null to show nothing.
    /// </summary>
    protected virtual Panel? CreateTooltipPanel()
    {
        if (string.IsNullOrWhiteSpace(Tooltip) && OnTooltip is null)
        {
            return null;
        }

        var tooltip = new Panel();
        tooltip.AddClass("tooltip");
        tooltip.AddClass(TooltipClass);
        tooltip.SetProperty("style", "position: absolute; pointer-events: none; z-index: 10000;");
        if (!string.IsNullOrWhiteSpace(Tooltip))
        {
            tooltip.AddChild(new Label { Text = Tooltip });
        }

        OnTooltip?.Invoke(tooltip);
        tooltip.Parent = FindRootPanel();
        return tooltip;
    }
}
