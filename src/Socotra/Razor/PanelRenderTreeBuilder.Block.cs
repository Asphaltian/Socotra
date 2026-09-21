using Microsoft.AspNetCore.Components;

namespace Socotra;

internal sealed partial class PanelRenderTreeBuilder
{
    private Block _rootBlock;

    private Block CurrentBlock => _currentScope.Block ?? _rootBlock;

    private sealed class Block
    {
        private List<Block>? _children;
        private Dictionary<int, int>? _cache;
        private Dictionary<int, int>? _increments;
        private bool _hasBindsDeep = true;

        public int Hash;
        public Block? Parent;
        public bool IsRootElement;
        public Panel? ElementPanel;
        public Action? ReferenceClearer;
        public List<Func<bool>>? Binds;
        public List<Panel>? MarkupPanels;
        public string? Classes;
        public bool WasSeen;

        public void MarkHasBinds()
        {
            for (var block = this; block is not null; block = block.Parent)
            {
                block._hasBindsDeep = true;
            }
        }

        public void Destroy(Panel? outroParent = null, bool immediate = false)
        {
            var owner = immediate ? null : IsRootElement ? outroParent : ElementPanel ?? outroParent;
            if (_children is not null)
            {
                foreach (var child in _children)
                {
                    child.Destroy(owner, immediate);
                }

                _children = null;
            }

            if (MarkupPanels is not null)
            {
                foreach (var panel in MarkupPanels)
                {
                    panel.DeleteFromRenderTree(owner, true);
                }

                MarkupPanels = null;
            }

            if (!IsRootElement)
            {
                ElementPanel?.DeleteFromRenderTree(immediate ? null : outroParent, immediate);
            }

            ElementPanel = null;
            try
            {
                ReferenceClearer?.Invoke();
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }

        public Panel FindOrCreateElement(string elementName, Panel parent)
        {
            if (ElementPanel is not { IsDeleted: false })
            {
                ElementPanel = Panel.CreateElement(elementName);
                ElementPanel.Parent = parent;
            }

            if (ElementPanel.Parent != parent && parent is not (Label or Image))
            {
                ElementPanel.Parent = parent;
            }

            return ElementPanel;
        }

        public Panel? FindOrCreateElement<T>(Panel parent)
            where T : IComponent, new()
        {
            if (ElementPanel is not { IsDeleted: false } && new T() is Panel panel)
            {
                panel.Parent = parent;
                ElementPanel = panel;
            }

            return ElementPanel;
        }

        public bool UpdateBinds()
        {
            if (!_hasBindsDeep)
            {
                return false;
            }

            bool changed = false;
            bool foundBinds = Binds is { Count: > 0 };
            if (_children is not null)
            {
                foreach (var child in _children)
                {
                    changed = child.UpdateBinds() || changed;
                    foundBinds |= child._hasBindsDeep;
                }
            }

            _hasBindsDeep = foundBinds;
            if (Binds is not null)
            {
                for (int i = 0; i < Binds.Count; i++)
                {
                    try
                    {
                        changed = Binds[i]() || changed;
                    }
                    catch (Exception e)
                    {
                        Log.Warning($"Razor bind exception ({e.Message})");
                        Binds.RemoveAt(i);
                        return changed;
                    }
                }
            }

            return changed;
        }

        public void Reset()
        {
            WasSeen = false;
            _increments?.Clear();
            foreach (var child in _children ?? [])
            {
                child.Reset();
            }
        }

        public bool DestroyUnseen()
        {
            if (!WasSeen)
            {
                Destroy();
                return true;
            }

            _children?.RemoveAll(child => child.DestroyUnseen());
            return false;
        }

        public Block GetChild(int hash)
        {
            _children ??= [];
            Block? child = null;
            foreach (var candidate in _children)
            {
                if (candidate.Hash == hash)
                {
                    child = candidate;
                    break;
                }
            }

            if (child is null)
            {
                child = new Block { Hash = hash, Parent = this };
                _children.Add(child);
            }

            child.WasSeen = true;
            return child;
        }

        public bool CheckCacheValue(int key, int hash)
        {
            _cache ??= [];
            if (_cache.TryGetValue(key, out var previous) && previous == hash)
            {
                return true;
            }

            _cache[key] = hash;
            return false;
        }

        public int Increment(int sequence)
        {
            _increments ??= [];
            int counter = _increments.GetValueOrDefault(sequence) + 1;
            _increments[sequence] = counter;
            return counter;
        }
    }
}
