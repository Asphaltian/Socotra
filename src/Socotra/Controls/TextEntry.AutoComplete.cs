namespace Socotra;

public partial class TextEntry
{
    /// <summary>
    /// Gives suggestions for what's been typed. It's called with the text each time it changes, and whatever it returns
    /// shows in a list above the entry. Return strings, or <see cref="AutocompleteEntry"/>s to show something other than the value.
    /// The arrow keys and Tab walk the list, Enter picks and Escape puts the typed text back.
    /// </summary>
    /// <example>
    /// <code>
    /// // Suggest console commands that start with what's been typed
    /// input.AutoComplete = text => [.. commands.Where(c => c.StartsWith(text, StringComparison.OrdinalIgnoreCase))];
    /// </code>
    /// </example>
    public Func<string, object[]>? AutoComplete { get; set; }

    internal Popup? AutoCompletePanel { get; private set; }

    /// <summary>Asks <see cref="AutoComplete"/> for suggestions and shows them, or closes the list when there are none.</summary>
    public void UpdateAutoComplete()
    {
        if (!CanEdit)
        {
            AutoCompleteCancel();
            return;
        }

        if (AutoComplete?.Invoke(Text) is not { Length: > 0 } results)
        {
            DestroyAutoComplete();
            return;
        }

        UpdateAutoComplete(results);
    }

    /// <summary>Shows <paramref name="options"/> in the suggestion list. Pass strings, or <see cref="AutocompleteEntry"/>s to show something other than the value.</summary>
    public void UpdateAutoComplete(object[] options)
    {
        if (!CanEdit)
        {
            AutoCompleteCancel();
            return;
        }

        if (AutoCompletePanel is not { IsDeleted: false, IsDeleting: false })
        {
            AutoCompletePanel = new Popup(this, Popup.PositionMode.AboveLeft, 8);
            AutoCompletePanel.AddClass("autocomplete");
            AutoCompletePanel.SkipTransitions();
        }

        AutoCompletePanel.DeleteChildren(true);
        AutoCompletePanel.UserData = CurrentState();
        foreach (var option in options)
        {
            if (option is AutocompleteEntry entry)
            {
                AutoCompletePanel.AddOption(entry.Title, entry.Icon, () => AutoCompleteSelected(entry.Value)).UserData = entry.Value;
            }
            else
            {
                AutoCompletePanel.AddOption(option.ToString(), () => AutoCompleteSelected(option)).UserData = option;
            }
        }
    }

    /// <summary>Closes the suggestion list.</summary>
    public virtual void DestroyAutoComplete()
    {
        AutoCompletePanel?.Delete();
        AutoCompletePanel = null;
    }

    /// <summary>Called when the arrow keys or Tab move to another suggestion. By default it shows the suggestion in the entry, without counting it as an edit.</summary>
    protected virtual void AutoCompleteSelectionChanged()
    {
        if (!CanEdit)
        {
            AutoCompleteCancel();
            return;
        }

        if (AutoCompletePanel?.SelectedChild is not { IsDeleted: false } selected)
        {
            return;
        }

        Text = selected.UserData?.ToString();
        Label.MoveToLineEnd();
    }

    /// <summary>Called when the suggestions are dismissed with Escape. By default it puts back the text, caret and selection from before and closes the list.</summary>
    protected virtual void AutoCompleteCancel()
    {
        RestoreAutoCompletePreview();
        DestroyAutoComplete();
    }

    private void AutoCompleteSelected(object? value)
    {
        if (!CanEdit)
        {
            AutoCompleteCancel();
            return;
        }

        var text = value?.ToString() ?? "";
        RestoreAutoCompletePreview();
        var changed = Text != text;
        if (changed)
        {
            RecordEdit(EditKind.Single);
        }

        Text = text;
        Label.SetCaretPosition(TextLength);
        Focus();
        if (changed)
        {
            OnValueChanged();
        }

        DestroyAutoComplete();
    }

    private bool CommitAutoComplete()
    {
        if (AutoCompletePanel is not { IsDeleted: false } panel || panel.SelectedChild is not { IsDeleted: false } selected)
        {
            return false;
        }

        AutoCompleteSelected(selected.UserData);
        return true;
    }

    private void RestoreAutoCompletePreview()
    {
        if (AutoCompletePanel?.UserData is TextState state)
        {
            ApplyState(state);
        }
    }

    private bool MoveAutoCompleteSelection(int direction)
    {
        if (AutoCompletePanel is not { IsDeleted: false } panel)
        {
            return false;
        }

        panel.MoveSelection(direction);
        AutoCompleteSelectionChanged();
        return true;
    }

    /// <summary>A suggestion for <see cref="AutoComplete"/> that shows <see cref="Title"/> in the list and puts <see cref="Value"/> in the entry when it's picked.</summary>
    public struct AutocompleteEntry
    {
        /// <summary>The text shown in the list.</summary>
        public string? Title { get; set; }

        /// <summary>The name of an icon shown before the <see cref="Title"/>.</summary>
        public string? Icon { get; set; }

        /// <summary>What goes in the entry when the suggestion is picked, as text.</summary>
        public object? Value { get; set; }
    }
}
