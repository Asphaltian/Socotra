namespace Socotra;

public partial class Panel
{
    /// <summary>
    /// Lets the user select the text of the labels inside this panel by dragging across them, and copy it with Ctrl+C.
    /// Text in an inline paragraph copies as one string; separate labels are joined with a space, or a new line in a column.
    /// </summary>
    public bool AllowChildSelection { get; set; }

    /// <summary>Selects all the text inside this panel, including whole inline paragraphs.</summary>
    public void SelectAllInChildren()
    {
        if (LayoutTree.SetInlineSelection(0, int.MaxValue))
        {
            return;
        }

        if (this is Label label)
        {
            label.ShouldDrawSelection = true;
            label.SelectionStart = 0;
            label.SelectionEnd = int.MaxValue;
            return;
        }

        foreach (var child in Children)
        {
            child.SelectAllInChildren();
        }
    }

    /// <summary>Clears the text selection of every label and inline paragraph inside this panel.</summary>
    public void UnselectAllInChildren()
    {
        if (LayoutTree.SetInlineSelection(0, 0))
        {
            return;
        }

        if (this is Label label)
        {
            label.ShouldDrawSelection = false;
            return;
        }

        foreach (var child in Children)
        {
            child.UnselectAllInChildren();
        }
    }

    private static string? CollectSelectedChildrenText(Panel p)
    {
        if (!p.IsVisible)
        {
            return null;
        }

        if (p.LayoutTree.HasInlineContent)
        {
            return p.LayoutTree.SelectedInlineText;
        }

        if (p is Label label)
        {
            return label.GetSelectedText();
        }

        string? selection = null;
        var separator = p.ComputedStyle?.FlexDirection == FlexDirection.Column ? "\n" : " ";
        foreach (var child in p.Children)
        {
            var text = CollectSelectedChildrenText(child);
            if (string.IsNullOrEmpty(text))
            {
                continue;
            }

            selection = selection is null ? text : $"{selection}{separator}{text}";
        }

        return selection;
    }

    private static void UpdateSelection(Panel p, SelectionEvent e)
    {
        if (p.LayoutTree.SelectInlineText(e.StartPoint, e.EndPoint))
        {
            return;
        }

        var rect = e.SelectionRect;
        if (p.Box.Rect.Bottom < rect.Top || p.Box.Rect.Top > rect.Bottom)
        {
            p.UnselectAllInChildren();
            return;
        }

        if (p is not Label label)
        {
            foreach (var child in p.Children)
            {
                UpdateSelection(child, e);
            }

            return;
        }

        label.ShouldDrawSelection = true;
        var (startPoint, endPoint) = e.StartPoint.Y > e.EndPoint.Y ? (e.EndPoint, e.StartPoint) : (e.StartPoint, e.EndPoint);
        var start = p.Box.Rect.Top < rect.Top;
        var end = p.Box.Rect.Bottom > endPoint.Y;
        var negativeWidth = endPoint.X < startPoint.X;
        if (start && end)
        {
            label.SelectionStart = label.GetLetterAtScreenPosition(new Vector2(rect.Left, rect.Top));
            label.SelectionEnd = label.GetLetterAtScreenPosition(new Vector2(rect.Right, rect.Bottom));
        }
        else if (start)
        {
            label.SelectionStart = label.GetLetterAtScreenPosition(new Vector2(negativeWidth ? rect.Right : rect.Left, rect.Top));
            label.SelectionEnd = int.MaxValue;
        }
        else if (end)
        {
            label.SelectionStart = 0;
            label.SelectionEnd = label.GetLetterAtScreenPosition(new Vector2(negativeWidth ? rect.Left : rect.Right, rect.Bottom));
        }
        else
        {
            label.SelectionStart = 0;
            label.SelectionEnd = int.MaxValue;
        }
    }
}
