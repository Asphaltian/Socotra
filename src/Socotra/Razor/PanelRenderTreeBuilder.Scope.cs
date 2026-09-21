namespace Socotra;

internal sealed partial class PanelRenderTreeBuilder
{
    private readonly Stack<Scope> _stack = new();
    private Scope _currentScope;

    private void PushScope(int sequence, object? key)
    {
        int loop = CurrentBlock.Increment(sequence);
        _stack.Push(_currentScope);
        _currentScope.Sequence = sequence;
        _currentScope.Loop = loop;
        _currentScope.ChildIndex = 0;
        _currentScope.Hash = HashCode.Combine(key ?? loop, sequence);
    }

    private void PopScope()
    {
        _content.Clear();
        _hasContent = false;
        _currentScope = _stack.Pop();
    }

    private struct Scope
    {
        public int Sequence;
        public int Loop;
        public Block? Block;
        public int ChildIndex;
        public int Hash;

        public readonly Panel? Element => Block?.ElementPanel;
    }
}
