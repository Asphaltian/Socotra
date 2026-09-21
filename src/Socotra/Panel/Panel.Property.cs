using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;

namespace Socotra;

public partial class Panel
{
    private static readonly ConcurrentDictionary<(Type, string), PropertyInfo?> SettableProperties = new();

    private Dictionary<string, string>? _attributes;
    private string? _previousPropertyClass;
    private bool _parametersChanged = true;
    private Task? _parametersSetTask;

    /// <summary>Whether the panel has <c>:disabled</c>. Disabled panels don't take input, but still show their <see cref="Tooltip"/>.</summary>
    public bool Disabled
    {
        get => (PseudoClass & PseudoClass.Disabled) != 0;
        set => Switch(PseudoClass.Disabled, value);
    }

    /// <summary>
    /// Called for each attribute in markup, like <c>class</c>, <c>style</c> or <c>id</c>. <c>tabindex</c> makes the panel
    /// focusable and <c>autofocus</c> focuses it when it appears. Any other attribute also sets the public property with the
    /// same name. Override it to handle attributes of your own, and call the base method for the rest.
    /// </summary>
    public virtual void SetProperty(string name, string? value)
    {
        switch (name)
        {
            case "id":
                Id = value;
                return;
            case "class":
                RemoveClass(_previousPropertyClass);
                _previousPropertyClass = value;
                AddClass(value);
                return;
            case "style":
                Style.Set(value ?? "");
                return;
            case "disabled":
                Disabled = value is not null && (value.Length == 0 || Translation.ToBool(value));
                return;
            case "tabindex":
                AcceptsFocus |= value is not null;
                TabIndex = int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index) ? index : 0;
                return;
            case "autofocus" when value is not null && GetAttribute(name) is null:
                Focus();
                break;
        }

        SetAttribute(name, value);
        SetPropertyValue(name, value);
    }

    /// <summary>Stores an attribute you can read back with <see cref="GetAttribute"/>. Pass null to remove it.</summary>
    public void SetAttribute(string k, string? v)
    {
        if (string.IsNullOrEmpty(k))
        {
            return;
        }

        _attributes ??= [];
        if (v is null)
        {
            _attributes.Remove(k);
        }
        else
        {
            _attributes[k] = v;
        }
    }

    /// <summary>An attribute's value, or <paramref name="defaultIfNotFound"/> if it isn't set.</summary>
    public string? GetAttribute(string k, string? defaultIfNotFound = null) => _attributes?.GetValueOrDefault(k) ?? defaultIfNotFound;

    /// <summary>Called with the text between the panel's tags in markup. Controls that show text, like buttons, override it.</summary>
    public virtual void SetContent(string? value)
    {
    }

    internal static Panel CreateElement(string elementName)
    {
        Panel panel = elementName.ToLowerInvariant() switch
        {
            "label" or "text" => new Label(),
            "button" => new Button(),
            "img" or "image" => new Image(),
            "navigator" => new NavigationHost(),
            "a" or "navlink" => new NavLinkPanel(),
            "textentry" => new TextEntry(),
            "iconpanel" or "icon" or "i" => new IconPanel(),
            "select" or "dropdown" => new DropDown(),
            "buttongroup" => new ButtonGroup(),
            "menu" => new Menu(),
            "menubar" => new MenuBar(),
            "tab" => new Tab(),
            "tabbar" => new TabBar(),
            "tabpanel" => new TabPanel(),
            "toolbar" => new Toolbar(),
            "statusbar" => new StatusBar(),
            "form" => new Form(),
            "field" => new Field(),
            "control" => new FieldControl(),
            "checkbox" => new Checkbox(),
            "split" => new SplitContainer(),
            "svg" => new SvgPanel(),
            "graphpanel" => new GraphPanel(),
            "graphaxis" => new GraphAxis(),
            "curveeditor" => new CurveEditor(),
            "dockhost" => new DockHost(),
            _ => new Panel(),
        };

        panel.ElementName = elementName.ToLowerInvariant();
        return panel;
    }

    /// <summary>Called when Razor sets the panel's parameters, and once when the panel first appears.</summary>
    protected virtual void OnParametersSet()
    {
    }

    /// <summary>Called when Razor sets the panel's parameters, for loading things that take a while. <see cref="OnParametersSet"/> runs once it finishes.</summary>
    protected virtual Task OnParametersSetAsync() => Task.CompletedTask;

    internal void ParametersChanged() => _parametersChanged = true;

    private void TickParameters()
    {
        if (_parametersSetTask is { } task)
        {
            if (!task.IsCompleted)
            {
                return;
            }

            _parametersSetTask = null;
            FinishParametersSet(task);
        }

        if (!_parametersChanged)
        {
            return;
        }

        _parametersChanged = false;
        StateHasChanged();

        Task parametersSet;
        try
        {
            parametersSet = OnParametersSetAsync();
        }
        catch (Exception e)
        {
            parametersSet = Task.FromException(e);
        }

        if (parametersSet.IsCompleted)
        {
            FinishParametersSet(parametersSet);
        }
        else
        {
            _parametersSetTask = parametersSet;
        }
    }

    private void FinishParametersSet(Task parametersSet)
    {
        if (parametersSet.Exception is { } exception)
        {
            Log.Error(exception.InnerException ?? exception);
        }

        if (parametersSet.IsCanceled || IsDeleted)
        {
            return;
        }

        try
        {
            OnParametersSet();
        }
        catch (Exception e)
        {
            Log.Error(e);
        }

        StateHasChanged();
    }

    private void SetPropertyValue(string name, string? value)
    {
        if (SettableProperties.GetOrAdd((GetType(), name), static key => FindSettableProperty(key.Item1, key.Item2)) is not { } property)
        {
            return;
        }

        var type = property.PropertyType;
        if (value is null && type.IsValueType && Nullable.GetUnderlyingType(type) is null)
        {
            if (type == typeof(bool))
            {
                property.SetValue(this, false);
            }

            return;
        }

        try
        {
            if (Translation.TryConvert(value, type, out var converted))
            {
                property.SetValue(this, converted);
                return;
            }
        }
        catch (Exception e) when (e is TargetInvocationException or ArgumentException)
        {
            Log.Error(e);
            return;
        }

        Log.Warning($"Couldn't set property {GetType()}.{property.Name} ({type}) to {value}");
    }

    private static PropertyInfo? FindSettableProperty(Type type, string name) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && p.SetMethod is { IsPublic: true } && p.GetIndexParameters().Length == 0);
}
