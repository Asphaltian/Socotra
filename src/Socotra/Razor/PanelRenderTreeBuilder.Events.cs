namespace Socotra;

internal sealed partial class PanelRenderTreeBuilder
{
    internal void AddAttributeAction(string name, Action value) => AddPanelEventAttribute(name, _ => value());

    internal void AddAttributeAction(string name, Func<Task> value)
    {
        var element = _currentScope.Element;
        AddPanelEventAttribute(name, _ =>
        {
            value();
            element?.StateHasChanged();
        });
    }

    internal void AddPanelEventAttribute(string name, Action<PanelEvent> value) => _currentScope.Element?.SetEventListener(name, value);
}
