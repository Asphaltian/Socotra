using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Socotra.Html;

namespace Socotra;

public partial class Panel
{
    private PanelRenderTreeBuilder? _renderTree;
    private bool _renderTreeDirty = true;
    private int _buildHash;

    /// <summary>The markup between this panel's tags when you use it in Razor, like <c>&lt;MyPanel&gt;...&lt;/MyPanel&gt;</c>.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    private bool HasRenderTree => GetRenderTreeChecksum() is not null;

    /// <summary>Call it after changing something your markup shows, so the panel rebuilds. Clicks and parameter changes do this for you.</summary>
    public void StateHasChanged() => _renderTreeDirty = true;

    /// <summary>
    /// Rebuilds this panel, and its ancestors up to <paramref name="upTo"/>, after you give it new markup like a new
    /// <see cref="ChildContent"/>.
    /// </summary>
    public void OnRenderFragmentChanged(Panel? upTo)
    {
        if (upTo == this)
        {
            return;
        }

        _renderTreeDirty = true;
        Parent?.OnRenderFragmentChanged(upTo);
    }

    /// <summary>
    /// Called for markup inside this panel with a <c>slot</c> attribute, like <c>&lt;div slot="navigator-canvas"&gt;</c>.
    /// Override it to put <paramref name="panel"/> where that slot goes; otherwise it's offered to the parent.
    /// </summary>
    public virtual void OnTemplateSlot(INode element, string slotName, Panel panel) => Parent?.OnTemplateSlot(element, slotName, panel);

    /// <summary>
    /// Says whether the panel builds children from markup: null when it doesn't. Razor files write this for you. Override it
    /// in a panel that overrides <see cref="BuildRenderTree"/> by hand.
    /// </summary>
    protected virtual string? GetRenderTreeChecksum() => ChildContent is null ? null : "1";

    /// <summary>Builds the panel's children. Razor files write this for you from their markup.</summary>
    protected virtual void BuildRenderTree(RenderTreeBuilder builder) => ChildContent?.Invoke(builder);

    /// <summary>
    /// Return a hash of whatever the markup shows, and the panel rebuilds whenever it changes, like
    /// <c>HashCode.Combine(Health, Name)</c>. That saves calling <see cref="StateHasChanged"/> by hand.
    /// </summary>
    protected virtual int BuildHash() => 0;

    /// <summary>Called after the panel's markup is built. <paramref name="firstTime"/> is true the first time, a good moment to focus a child.</summary>
    protected virtual void OnAfterTreeRender(bool firstTime)
    {
    }

    private bool TickRenderTree()
    {
        TickParameters();
        if (!HasRenderTree)
        {
            return false;
        }

        TickTreeBinds();

        if (!_renderTreeDirty)
        {
            return false;
        }

        _renderTreeDirty = false;
        _renderTree ??= new PanelRenderTreeBuilder(this);
        try
        {
            _renderTree.Build(BuildRenderTree);
        }
        catch (Exception e)
        {
            Log.Error(e);
        }

        return true;
    }

    private void TickTreeBinds()
    {
        try
        {
            var hash = BuildHash();
            if (hash != _buildHash)
            {
                _buildHash = hash;
                _renderTreeDirty = true;
            }
        }
        catch (Exception e)
        {
            Log.Error(e);
        }

        if (_renderTree?.UpdateBinds() == true)
        {
            _renderTreeDirty = true;
        }
    }
}
