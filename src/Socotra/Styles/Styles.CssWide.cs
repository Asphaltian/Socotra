namespace Socotra;

internal enum CssWideKeyword
{
    Inherit,
    Initial,
    Unset,
    Revert,
}

public partial class Styles
{
    private static readonly Dictionary<string, string[]> ShorthandExpansions = new()
    {
        ["margin"] = ["margin-left", "margin-top", "margin-right", "margin-bottom"],
        ["padding"] = ["padding-left", "padding-top", "padding-right", "padding-bottom"],
        ["inset"] = ["top", "right", "bottom", "left"],
        ["gap"] = ["row-gap", "column-gap"],
        ["grid-column"] = ["grid-column-start", "grid-column-end"],
        ["grid-row"] = ["grid-row-start", "grid-row-end"],
        ["grid-area"] = ["grid-row-start", "grid-column-start", "grid-row-end", "grid-column-end"],
        ["grid-template"] = ["grid-template-rows", "grid-template-columns"],
        ["place-items"] = ["align-items", "justify-items"],
        ["place-self"] = ["align-self", "justify-self"],
        ["overflow"] = ["overflow-x", "overflow-y"],
        ["overscroll-behavior"] = ["overscroll-behavior-x", "overscroll-behavior-y"],
        ["border-radius"] =
        [
            "border-top-left-radius", "border-top-right-radius", "border-bottom-right-radius", "border-bottom-left-radius",
            "border-top-left-radius-v", "border-top-right-radius-v", "border-bottom-right-radius-v", "border-bottom-left-radius-v",
        ],
        ["border-width"] = ["border-top-width", "border-right-width", "border-bottom-width", "border-left-width"],
        ["border-color"] = ["border-left-color", "border-top-color", "border-right-color", "border-bottom-color"],
        ["border"] =
        [
            "border-left-width", "border-top-width", "border-right-width", "border-bottom-width",
            "border-left-color", "border-top-color", "border-right-color", "border-bottom-color", "border-style",
        ],
        ["border-left"] = ["border-left-width", "border-left-color"],
        ["border-right"] = ["border-right-width", "border-right-color"],
        ["border-top"] = ["border-top-width", "border-top-color"],
        ["border-bottom"] = ["border-bottom-width", "border-bottom-color"],
        ["outline"] = ["outline-width", "outline-color"],
        ["text-stroke"] = ["text-stroke-width", "text-stroke-color"],
        ["text-decoration"] = ["text-decoration-line", "text-decoration-color", "text-decoration-thickness", "text-decoration-style"],
        ["transform-origin"] = ["transform-origin-x", "transform-origin-y"],
        ["perspective-origin"] = ["perspective-origin-x", "perspective-origin-y"],
        ["background-size"] = ["background-size-x", "background-size-y"],
        ["background-position"] = ["background-position-x", "background-position-y"],
        ["mask-size"] = ["mask-size-x", "mask-size-y"],
        ["mask-position"] = ["mask-position-x", "mask-position-y"],
        ["background"] = ["background-color", "background-position-x", "background-position-y", "background-size-x", "background-size-y", "background-repeat"],
        ["mask"] = ["mask-position-x", "mask-position-y", "mask-size-x", "mask-size-y", "mask-repeat", "mask-mode"],
        ["animation"] =
        [
            "animation-duration", "animation-delay", "animation-timing-function", "animation-iteration-count", "animation-direction",
            "animation-fill-mode", "animation-play-state", "animation-name",
        ],
        ["filter"] =
        [
            "filter-blur", "filter-saturate", "filter-sepia", "filter-brightness", "filter-contrast", "filter-hue-rotate", "filter-invert",
            "filter-tint", "filter-border-width", "filter-border-color", "filter-drop-shadow",
        ],
        ["backdrop-filter"] =
        [
            "backdrop-filter-blur", "backdrop-filter-invert", "backdrop-filter-contrast", "backdrop-filter-brightness", "backdrop-filter-saturate",
            "backdrop-filter-sepia", "backdrop-filter-hue-rotate",
        ],
    };

    private Dictionary<string, CssWideKeyword>? _cssWide;

    internal void ResolveCssWide(Styles? parent)
    {
        if (_cssWide is not { Count: > 0 })
        {
            return;
        }

        foreach (var (name, keyword) in _cssWide)
        {
            var property = PropertiesByName[name];
            var source = keyword switch
            {
                CssWideKeyword.Initial => InitialValues,
                CssWideKeyword.Inherit => parent,
                _ => property.Inherited ? parent : InitialValues,
            };

            if (source is not null)
            {
                property.Copy(this, source);
            }
        }
    }

    private static Styles CreateInitialValues()
    {
        var styles = new Styles();
        foreach (var property in Properties)
        {
            property.SetInitial(styles);
        }

        return styles;
    }

    private static CssWideKeyword? ParseCssWideKeyword(string value) => value.Length is < 5 or > 7 ? null : value.ToLowerInvariant() switch
    {
        "inherit" => CssWideKeyword.Inherit,
        "initial" => CssWideKeyword.Initial,
        "unset" => CssWideKeyword.Unset,
        "revert" => CssWideKeyword.Revert,
        _ => null,
    };

    private bool MarkCssWide(string property, CssWideKeyword keyword)
    {
        if (ShorthandExpansions.TryGetValue(property, out var longhands))
        {
            foreach (var longhand in longhands)
            {
                MarkCssWide(longhand, keyword);
            }

            return true;
        }

        if (!PropertiesByName.TryGetValue(property, out var longhandProperty))
        {
            return false;
        }

        longhandProperty.Clear(this);
        _cssWide ??= new();
        _cssWide[property] = keyword;
        return true;
    }

    private void ClearCssWide(string property)
    {
        if (_cssWide is null)
        {
            return;
        }

        _cssWide.Remove(property);
        foreach (var longhand in ShorthandExpansions.GetValueOrDefault(property) ?? [])
        {
            _cssWide.Remove(longhand);
        }
    }

    private void MergeCssWide(Styles other)
    {
        if (_cssWide is { Count: > 0 })
        {
            foreach (var name in _cssWide.Keys.ToArray())
            {
                if (other._cssWide?.ContainsKey(name) != true && PropertiesByName[name].IsSet(other))
                {
                    _cssWide.Remove(name);
                }
            }
        }

        if (other._cssWide is null)
        {
            return;
        }

        foreach (var (name, keyword) in other._cssWide)
        {
            _cssWide ??= new();
            _cssWide[name] = keyword;
            PropertiesByName[name].Clear(this);
        }
    }
}
