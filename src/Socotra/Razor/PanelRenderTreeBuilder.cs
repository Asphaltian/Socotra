using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Socotra;

internal sealed partial class PanelRenderTreeBuilder : RenderTreeBuilder
{
    private readonly Panel _parent;
    private readonly StringBuilder _content = new();
    private int _contentHash;
    private bool _hasContent;
    private string? _sourceFile;
    private int _sourceLine;

    internal PanelRenderTreeBuilder(Panel panel)
    {
        _parent = panel;
        _rootBlock = new Block { ElementPanel = panel, IsRootElement = true };
    }

    internal void Start()
    {
        _stack.Clear();
        _currentScope = default;
        _rootBlock.Reset();
        _rootBlock.WasSeen = true;
    }

    internal void Finish() => _rootBlock.DestroyUnseen();

    internal void Build(RenderFragment fragment)
    {
        Start();
        try
        {
            fragment(this);
        }
        finally
        {
            Finish();
        }
    }

    private Block GetBlock(int hash) => CurrentBlock.GetChild(hash);

    private void FlushContent()
    {
        if (!_hasContent)
        {
            return;
        }

        var text = _content.ToString();
        _content.Clear();
        _hasContent = false;
        var label = _currentScope.Element as Label;
        if (label is null)
        {
            if (text.Length == 0)
            {
                return;
            }

            label = GetBlock(_contentHash).FindOrCreateElement("label", _currentScope.Element ?? _parent) as Label;
            label?.IsGeneratedText = true;
        }

        if (label is not null)
        {
            _currentScope.ChildIndex++;
            label.SetContent(text);
        }
    }

    public override void AddLocation(string filename, int line, int column)
    {
        _sourceFile = filename;
        _sourceLine = line;
    }

    public override void OpenElement(int sequence, string elementName) => OpenElement(sequence, elementName, null);

    public override void OpenElement(int sequence, string elementName, object? key = null)
    {
        FlushContent();
        var parentElement = _currentScope.Element ?? _parent;
        int childIndex = _currentScope.ChildIndex++;
        PushScope(sequence, key);
        var block = GetBlock(_currentScope.Hash);

        if (string.Equals("root", elementName, StringComparison.OrdinalIgnoreCase) && _currentScope.Element is null)
        {
            block.IsRootElement = true;
            block.ElementPanel = _parent;
            _currentScope.Block = block;
            return;
        }

        var element = block.FindOrCreateElement(elementName, parentElement);
        element.Parent?.SetChildIndex(element, childIndex);
        element.SourceFile = _sourceFile;
        element.SourceLine = _sourceLine;
        _currentScope.Block = block;
    }

    internal void AddAttributeObject(string name, object? value) => AddAttributeString(name, $"{value}");

    internal void AddAttributeString(string name, string? value)
    {
        var block = CurrentBlock;
        if (block.CheckCacheValue(HashCode.Combine(_currentScope.Element, name), value?.GetHashCode() ?? 0))
        {
            return;
        }

        if (name == "class")
        {
            _currentScope.Element?.RemoveClass(block.Classes);
            _currentScope.Element?.AddClass(value);
            block.Classes = value;
            return;
        }

        _currentScope.Element?.SetProperty(name, value);
    }

    public override void AddStyleDefinitions(int sequence, string styles)
    {
        if (CurrentBlock.CheckCacheValue(HashCode.Combine(_currentScope.Element, sequence), styles?.GetHashCode() ?? 0))
        {
            return;
        }

        var sheet = StyleSheet.FromString(styles ?? "", _sourceFile ?? "none");
        sheet.FileName = $"__Razor.{_currentScope.Hash}";
        _parent.StyleSheet.Remove(sheet.FileName);
        _parent.StyleSheet.Add(sheet);
    }

    public override void AddAttribute<T>(int sequence, Action<T> value)
    {
        if (CurrentBlock.CheckCacheValue(HashCode.Combine(_currentScope.Element, sequence), 1))
        {
            return;
        }

        if (_currentScope.Element is T element)
        {
            value?.Invoke(element);
        }
    }

    public override void CloseElement()
    {
        FlushContent();
        PopScope();
    }

    public override void AddContent<T>(int sequence, T content)
    {
        if (content is RenderFragment fragment)
        {
            fragment(this);
            return;
        }

        _contentHash = HashCode.Combine(CurrentBlock.Increment(sequence), sequence);
        _content.Append(content);
        _hasContent = true;
    }

    internal void Clear(bool immediate = false)
    {
        _rootBlock.Destroy(immediate: immediate);
        _rootBlock = new Block { ElementPanel = _parent, IsRootElement = true };
        _parent.StyleSheet.Remove("*__Razor*");
    }

    public override void AddReferenceCapture<T>(int sequence, T current, Action<T> value)
    {
        var block = CurrentBlock;
        if (block.ReferenceClearer is not null)
        {
            return;
        }

        if (block.ElementPanel is T element)
        {
            value?.Invoke(element);
            block.ReferenceClearer = () => value?.Invoke(default!);
        }
    }

    public override void SetRenderFragment<T>(Action<T, RenderFragment> setter, RenderFragment builder)
    {
        if (CurrentBlock.ElementPanel is not T element)
        {
            return;
        }

        setter(element, builder);
        (element as Panel)?.OnRenderFragmentChanged(_parent);
    }

    public override void SetRenderFragmentWithContext<T, TValue>(Func<T, RenderFragment<TValue>> getter, Action<T, RenderFragment<TValue>> setter, RenderFragment<TValue> builder)
    {
        if (CurrentBlock.ElementPanel is not T element)
        {
            return;
        }

        setter(element, builder);
        (element as Panel)?.OnRenderFragmentChanged(_parent);
    }
}
