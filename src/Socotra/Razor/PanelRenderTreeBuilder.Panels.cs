using System.Reflection;

namespace Socotra;

internal sealed partial class PanelRenderTreeBuilder
{
    public override void OpenElement<T>(int sequence) => OpenElement<T>(sequence, null);

    public override void OpenElement<T>(int sequence, object? key)
    {
        FlushContent();
        var parentElement = _currentScope.Element ?? _parent;
        int childIndex = _currentScope.ChildIndex++;
        PushScope(sequence, key);
        var block = GetBlock(_currentScope.Hash);
        var element = block.FindOrCreateElement<T>(parentElement);
        _currentScope.Block = block;
        if (element is null)
        {
            return;
        }

        element.SourceFile = _sourceFile;
        element.SourceLine = _sourceLine;
        element.Parent?.SetChildIndex(element, childIndex);
    }

    internal void AddAttributeWithSetter<T>(int sequence, object? value, Action<T> setter)
    {
        if (CurrentBlock.CheckCacheValue(HashCode.Combine(_currentScope.Element, sequence), value?.GetHashCode() ?? 0))
        {
            return;
        }

        if (_currentScope.Element is not T element)
        {
            return;
        }

        setter(element);
        (element as Panel)?.ParametersChanged();
    }

    public override void AddBind<T>(int sequence, string propertyName, Func<T> get, Action<T> set)
    {
        var element = _currentScope.Element;
        if (element is null || CurrentBlock.CheckCacheValue(HashCode.Combine(element, sequence, propertyName), 1))
        {
            return;
        }

        if (element.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance) is not { CanRead: true, CanWrite: true } theirProperty)
        {
            Log.Warning($"{element} does not have property '{propertyName}'");
            return;
        }

        int? theirOldValue = null;
        int? ourOldValue = null;
        bool Check()
        {
            if (element.IsDeleted)
            {
                return false;
            }

            try
            {
                var ours = get();
                int oursHash = ours?.GetHashCode() ?? -1;
                if (oursHash != ourOldValue)
                {
                    ourOldValue = oursHash;
                    if (Translation.TryConvert(ours, theirProperty.PropertyType, out var converted))
                    {
                        theirProperty.SetValue(element, converted);
                    }
                    else
                    {
                        Log.Warning($"Couldn't convert {ours?.GetType()} to {theirProperty.PropertyType}");
                    }

                    theirOldValue = theirProperty.GetValue(element)?.GetHashCode() ?? -1;
                    return true;
                }

                var theirs = theirProperty.GetValue(element);
                int theirsHash = theirs?.GetHashCode() ?? -1;
                if (theirsHash != theirOldValue)
                {
                    theirOldValue = theirsHash;
                    if (Translation.TryConvert(theirs, typeof(T), out var converted))
                    {
                        set((T)converted!);
                    }
                    else
                    {
                        Log.Warning($"Couldn't convert {theirs?.GetType()} to {typeof(T)}");
                    }

                    ourOldValue = get()?.GetHashCode() ?? -1;
                    return true;
                }
            }
            catch (Exception)
            {
            }

            return false;
        }

        CurrentBlock.Binds ??= [];
        CurrentBlock.Binds.Add(Check);
        CurrentBlock.MarkHasBinds();
        Check();
    }

    internal bool UpdateBinds() => _rootBlock.UpdateBinds();
}
