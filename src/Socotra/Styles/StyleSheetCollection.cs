using System.Text.RegularExpressions;

namespace Socotra;

/// <summary>The stylesheets on a panel, which you get from <see cref="Panel.StyleSheet"/>. They style the panel and everything inside it.</summary>
public sealed class StyleSheetCollection
{
    private readonly Panel _owner;
    private readonly List<StyleSheet> _sheets = [];

    internal StyleSheetCollection(Panel owner)
    {
        _owner = owner;
    }

    internal IReadOnlyList<StyleSheet> Sheets => _sheets;

    /// <summary>Adds <paramref name="sheet"/> to the panel.</summary>
    public void Add(StyleSheet sheet)
    {
        if (_sheets.Contains(sheet))
        {
            return;
        }

        _sheets.Insert(0, sheet);
        _owner.Style.Dirty();
        _owner.Style.InvalidateBroadphase();
    }

    /// <summary>
    /// Loads a stylesheet file and adds it to the panel, like <see cref="StyleSheet.FromFile"/>. The file can use the SCSS variables
    /// from stylesheets already on this panel and its ancestors, unless you pass false for <paramref name="inheritVariables"/>.
    /// </summary>
    public void Load(string path, bool inheritVariables = true) => Add(StyleSheet.FromFile(path, inheritVariables ? CollectVariables() : null));

    /// <summary>Reads a stylesheet from <paramref name="styles"/> and adds it to the panel, replacing the one you added with the last call.</summary>
    public void Parse(string styles, bool inheritVariables = true)
    {
        Remove("string");
        Add(StyleSheet.FromString(styles, "string", inheritVariables ? CollectVariables() : null));
    }

    /// <summary>Adds the stylesheet <see cref="StyleSheet.FromInline"/> gives you for <paramref name="styles"/> and <paramref name="key"/>.</summary>
    public void AddInline(string styles, string key) => Add(StyleSheet.FromInline(styles, key));

    /// <summary>Takes <paramref name="sheet"/> off the panel.</summary>
    public void Remove(StyleSheet sheet)
    {
        if (_sheets.Remove(sheet))
        {
            _owner.Style.InvalidateBroadphase();
        }
    }

    /// <summary>Takes off every stylesheet whose <see cref="StyleSheet.FileName"/> matches <paramref name="wildcardGlob"/>, where <c>*</c> matches anything and <c>?</c> matches one character.</summary>
    public void Remove(string wildcardGlob)
    {
        var pattern = new Regex($"^{Regex.Escape(wildcardGlob).Replace(@"\*", ".*").Replace(@"\?", ".")}$", RegexOptions.IgnoreCase);
        if (_sheets.RemoveAll(sheet => pattern.IsMatch(sheet.FileName)) > 0)
        {
            _owner.Style.Dirty();
            _owner.Style.InvalidateBroadphase();
        }
    }

    /// <summary>The SCSS variables from every stylesheet on this panel and its ancestors.</summary>
    public IEnumerable<(string Key, string Value)> CollectVariables() =>
        _owner.AllStyleSheets.SelectMany(sheet => sheet.Variables.Select(v => (v.Key, v.Value)));
}
