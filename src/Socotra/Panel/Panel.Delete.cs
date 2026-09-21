namespace Socotra;

public partial class Panel
{
    private HashSet<Panel>? _renderTreeDeletion;
    private bool _renderTreeDeletePending;

    /// <summary>Whether the panel is on its way out, playing its <c>:outro</c> transitions.</summary>
    public bool IsDeleting { get; private set; }

    /// <summary>Whether the panel has been deleted.</summary>
    public bool IsDeleted { get; private set; }

    /// <summary>
    /// Deletes the panel. It gets <c>:outro</c> first and goes once any transitions that starts
    /// have finished, unless <paramref name="immediate"/> is set.
    /// </summary>
    public virtual void Delete(bool immediate = false)
    {
        if (IsDeleted)
        {
            return;
        }

        _deleteTokenSource?.Cancel();

        if (immediate || FindRootPanel() is not { } root)
        {
            Parent = null;
            IsVisible = false;
            IsDeleting = true;
            OnDeleteRecursive();
            return;
        }

        if (IsDeleting)
        {
            return;
        }

        IsDeleting = true;
        Transitions.Clear();
        Switch(PseudoClass.Outro, true);
        root.AddDeferredDeletion(this);
    }

    /// <summary>Called when the panel is deleted.</summary>
    public virtual void OnDeleted()
    {
    }

    internal void DeleteFromRenderTree(Panel? outroParent, bool immediate)
    {
        if (IsDeleted)
        {
            return;
        }

        if (IsDeleting)
        {
            if (immediate)
            {
                Parent = null;
                IsVisible = false;
                OnDeleteRecursive();
            }

            return;
        }

        if (outroParent is not null && outroParent != this && IsAncestor(outroParent))
        {
            outroParent._renderTreeDeletion ??= [];
            outroParent._renderTreeDeletion.Add(this);
            _renderTreeDeletePending = true;
            return;
        }

        _renderTreeDeletePending = false;
        try
        {
            Delete(immediate);
        }
        catch (Exception e)
        {
            Log.Error(e);
            Parent = null;
            IsVisible = false;
            IsDeleting = true;
            OnDeleteRecursive();
        }
    }

    private void OnDeleteRecursive()
    {
        if (IsDeleted)
        {
            return;
        }

        IsDeleted = true;
        try
        {
            _renderTree?.Clear(immediate: true);
            _renderTree = null;

            var pending = _renderTreeDeletion;
            _renderTreeDeletion = null;
            foreach (var panel in pending ?? [])
            {
                panel.DeleteFromRenderTree(null, true);
            }

            foreach (var child in Children.ToArray())
            {
                if (child._renderTreeDeletePending && !child.IsDeleting)
                {
                    child.DeleteFromRenderTree(null, true);
                }
                else
                {
                    child.OnDeleteRecursive();
                }
            }
        }
        catch (Exception e)
        {
            Log.Error(e);
        }

        try
        {
            _deleteTokenSource?.Cancel();
        }
        catch (Exception e)
        {
            Log.Error(e);
        }

        _deleteTokenSource?.Dispose();
        _deleteTokenSource = null;

        try
        {
            OnDeleted();
        }
        catch (Exception e)
        {
            Log.Error(e);
        }

        LayoutTree.Detach();
        _children = null;
        _parent = null;
    }
}
