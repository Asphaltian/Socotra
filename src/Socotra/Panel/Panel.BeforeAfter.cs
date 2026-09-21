namespace Socotra;

public partial class Panel
{
    private Panel? _beforeElement;
    private Panel? _afterElement;

    private void UpdateBeforeAfterElements()
    {
        if (((IStyleTarget)this).IsBeforeOrAfter)
        {
            return;
        }

        BuildPseudoElement(Style.HasBeforeElement, PseudoClass.Before, ref _beforeElement);
        BuildPseudoElement(Style.HasAfterElement, PseudoClass.After, ref _afterElement);
        if (_beforeElement is not null)
        {
            SetChildIndex(_beforeElement, 0);
        }

        if (_afterElement is not null)
        {
            SetChildIndex(_afterElement, LastContentChildIndex);
        }
    }

    private void BuildPseudoElement(bool shouldExist, PseudoClass pseudoClass, ref Panel? element)
    {
        if (!shouldExist)
        {
            element?.Delete();
            element = null;
            return;
        }

        if (element is not { IsDeleted: false })
        {
            element = new Label { ElementName = "element", PseudoClass = pseudoClass, Parent = this };
            element.RemoveClass("label");
        }
    }
}
