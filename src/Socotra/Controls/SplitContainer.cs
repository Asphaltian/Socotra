using Microsoft.AspNetCore.Components;
using Socotra.Html;

namespace Socotra;

/// <summary>
/// Two panes side by side with a splitter between them that the user drags to share out the space. Put children in
/// <see cref="Left"/> and <see cref="Right"/>, or in markup give them <c>slot="left"</c> or <c>slot="right"</c>. In
/// markup it's <c>&lt;split&gt;</c>, and <c>direction="vertical"</c>, <c>min-left</c>, <c>min-right</c>,
/// <c>default-left</c> and <c>default-right</c> set it up.
/// </summary>
/// <example>
/// <code>
/// // A file list on the left taking a third of the width, and a preview on the right
/// var split = new SplitContainer { Parent = window };
/// split.Left.AddChild(fileList);
/// split.Right.AddChild(preview);
/// split.UpdateSplitFraction(0.33f);
/// </code>
/// </example>
[StyleSheet.Inline("splitcontainer", Styles)]
public class SplitContainer : Panel
{
    private const string Styles = """
        .splitcontainer
        {
            flex-grow: 1;

            > .split-left, > .split-right
            {
                flex-grow: 1;
            }

            > .split-left
            {
                height: 100%;
            }

            > .splitter
            {
                flex-grow: 0;
                flex-shrink: 0;
                width: 8px;
                cursor: ew-resize;
            }

            &.vertical
            {
                flex-direction: column;

                > .splitter
                {
                    height: 8px;
                    width: 100%;
                    cursor: ns-resize;
                }
            }
        }
        """;

    private bool _vertical;

    /// <summary>Makes a side by side split with the splitter in the middle.</summary>
    public SplitContainer()
    {
        AddClass("splitcontainer");
        Left = Add.Panel("split-left");
        Splitter = Add.Panel("splitter");
        Right = Add.Panel("split-right");
        Splitter.AddEventListener("onmousedown", StartDragging);
        Splitter.AddEventListener("onmouseup", StopDragging);
    }

    /// <summary>The left pane, or the top one when <see cref="Vertical"/>. It has the <c>split-left</c> class.</summary>
    public Panel Left { get; protected set; }

    /// <summary>The right pane, or the bottom one when <see cref="Vertical"/>. It has the <c>split-right</c> class.</summary>
    public Panel Right { get; protected set; }

    /// <summary>The bar between the panes that the user drags. It has the <c>splitter</c> class.</summary>
    public Panel Splitter { get; protected set; }

    /// <summary>The smallest share of the space the left pane can have, from 0 to 1. It also limits how big the right pane gets.</summary>
    [Parameter]
    public float MinimumFractionLeft { get; set; } = 0.2f;

    /// <summary>The smallest share of the space the right pane can have, from 0 to 1. It also limits how big the left pane gets.</summary>
    [Parameter]
    public float MinimumFractionRight { get; set; } = 0.2f;

    /// <summary>Whether the user is dragging the splitter right now. The container has the <c>dragging</c> class meanwhile.</summary>
    public bool IsDragging { get; protected set; }

    /// <summary>Stacks the panes top to bottom instead of side by side. <see cref="Left"/> is then the top pane and <see cref="Right"/> the bottom one.</summary>
    [Parameter]
    public bool Vertical
    {
        get => _vertical;
        set
        {
            if (_vertical == value)
            {
                return;
            }

            _vertical = value;
            SetClass("vertical", value);
        }
    }

    /// <summary>
    /// Gives the left (or top) pane <paramref name="f"/> of the space, from 0 to 1, and the other pane the rest. The
    /// value is kept inside <see cref="MinimumFractionLeft"/> and <see cref="MinimumFractionRight"/>.
    /// </summary>
    public virtual void UpdateSplitFraction(float f)
    {
        f = MathF.Max(f, MinimumFractionLeft);
        if (1 - f < MinimumFractionRight)
        {
            f = 1 - MinimumFractionRight;
        }

        if (Vertical)
        {
            Left.Style.Height = Length.Fraction(f);
            Right.Style.Height = Length.Fraction(1 - f);
        }
        else
        {
            Left.Style.Width = Length.Fraction(f);
            Right.Style.Width = Length.Fraction(1 - f);
        }
    }

    /// <summary>Puts markup with <c>slot="left"</c> in <see cref="Left"/> and <c>slot="right"</c> in <see cref="Right"/>.</summary>
    public override void OnTemplateSlot(INode element, string slotName, Panel panel)
    {
        switch (slotName)
        {
            case "left":
                panel.Parent = Left;
                return;
            case "right":
                panel.Parent = Right;
                return;
        }

        base.OnTemplateSlot(element, slotName, panel);
    }

    /// <summary>Sets <c>direction</c>, <c>min-left</c>, <c>min-right</c>, <c>default-left</c> and <c>default-right</c> from markup.</summary>
    public override void SetProperty(string name, string? value)
    {
        switch (name)
        {
            case "direction":
                Vertical = value == "vertical";
                break;
            case "min-left":
                MinimumFractionLeft = ParseFloat(value);
                break;
            case "min-right":
                MinimumFractionRight = ParseFloat(value);
                break;
            case "default-left":
                UpdateSplitFraction(ParseFloat(value));
                break;
            case "default-right":
                UpdateSplitFraction(1 - ParseFloat(value));
                break;
        }

        base.SetProperty(name, value);
    }

    /// <summary>Moves the split to the mouse while the splitter is being dragged.</summary>
    protected override void OnMouseMove(MousePanelEvent e)
    {
        if (IsDragging)
        {
            var local = ScreenPositionToPanelDelta(ScreenMousePosition);
            UpdateSplitFraction(Vertical ? local.Y : local.X);
        }

        base.OnMouseMove(e);
    }

    private static float ParseFloat(string? value) => Translation.TryParseFloat(value, out var f) ? f : 0;

    private void StartDragging(PanelEvent e)
    {
        SetClass("dragging", true);
        IsDragging = true;
    }

    private void StopDragging(PanelEvent e)
    {
        SetClass("dragging", false);
        IsDragging = false;
    }
}
