using Socotra.Html;

namespace Socotra;

internal sealed partial class PanelRenderTreeBuilder
{
    public override void AddMarkupContent(int sequence, string markupContent)
    {
        if (string.IsNullOrWhiteSpace(markupContent))
        {
            return;
        }

        var parent = _currentScope.Element ?? _parent;
        var block = GetBlock(HashCode.Combine(CurrentBlock.Increment(sequence), sequence));
        if (block.MarkupPanels is { } existing && existing.All(panel => !panel.IsDeleted))
        {
            foreach (var panel in existing)
            {
                parent.SetChildIndex(panel, _currentScope.ChildIndex++);
            }

            return;
        }

        block.MarkupPanels = [];
        FlushContent();
        var root = Node.Parse(markupContent);
        if (root.NodeType != NodeType.Document)
        {
            return;
        }

        foreach (var node in root.ChildNodes)
        {
            if (CreateNodeMarkup(node, parent) is { } panel)
            {
                block.MarkupPanels.Add(panel);
                parent.SetChildIndex(panel, _currentScope.ChildIndex++);
                panel.SourceFile = _sourceFile;
                panel.SourceLine = _sourceLine;
            }
        }
    }

    private Panel? CreateNodeMarkup(Node node, Panel parent)
    {
        if (node.NodeType == NodeType.Element)
        {
            var panel = Panel.CreateElement(node.Name);
            panel.Parent = parent;
            panel.SourceFile = _sourceFile;
            panel.SourceLine = _sourceLine;
            string? slot = null;
            foreach (var attribute in node.Attributes)
            {
                if (attribute.Name == "slot")
                {
                    slot = attribute.Value;
                    continue;
                }

                panel.SetProperty(attribute.Name, attribute.Value);
            }

            foreach (var child in node.ChildNodes)
            {
                CreateNodeMarkup(child, panel);
            }

            if (slot is not null)
            {
                panel.Parent?.OnTemplateSlot(node, slot, panel);
            }

            return panel;
        }

        if (node is not TextNode text || string.IsNullOrWhiteSpace(text.InnerHtml))
        {
            return null;
        }

        if (parent is Label)
        {
            parent.SetContent(text.InnerHtml);
            return null;
        }

        var label = new Label { Parent = parent, IsGeneratedText = true };
        label.SetContent(text.InnerHtml);
        return label;
    }
}
