using System.Diagnostics.CodeAnalysis;

namespace Socotra;

public partial class ColorPickerControl
{
    private ColorPalette _palette = ColorPalette.Shared;
    private int _paletteVersion;
    private Panel _recentRow;
    private Panel _savedRow;
    private Menu _swatchMenu;
    private int _menuSwatch;

    /// <summary>
    /// The recent and saved colors the picker lists. Every picker shares <see cref="ColorPalette.Shared"/> unless you set
    /// another. See <see cref="ColorPalette"/> to keep the saved colors between runs.
    /// </summary>
    public ColorPalette Palette
    {
        get => _palette;
        set
        {
            if (_palette == value)
            {
                return;
            }

            _palette = value;
            RenderPalette();
        }
    }

    /// <inheritdoc/>
    public override void Tick()
    {
        base.Tick();
        if (_paletteVersion != _palette.Version)
        {
            RenderPalette();
        }
    }

    [MemberNotNull(nameof(_recentRow), nameof(_savedRow), nameof(_swatchMenu))]
    private void BuildPalette()
    {
        _recentRow = Section("Recent");
        _savedRow = Section("Saved");
        _swatchMenu = new Menu();
        _swatchMenu.AddOption("Replace with current", () => ChangePalette(() => _palette.ReplaceSaved(_menuSwatch, _color)));
        _swatchMenu.AddOption("Remove", () => ChangePalette(() => _palette.RemoveSaved(_menuSwatch)));
        _swatchMenu.AddOption("Clear all", () => ChangePalette(_palette.ClearSaved));
        RenderPalette();
    }

    private Panel Section(string title)
    {
        var section = Add.Panel("section");
        section.Add.Label(title, "title");
        return section.Add.Panel("swatches");
    }

    private void Commit() => ChangePalette(() => _palette.AddRecent(_color));

    private void ChangePalette(Action change)
    {
        change();
        RenderPalette();
    }

    private void RenderPalette()
    {
        _paletteVersion = _palette.Version;
        _recentRow.DeleteChildren(true);
        foreach (var color in _palette.RecentColors)
        {
            PaletteSwatch(_recentRow, color);
        }

        _savedRow.DeleteChildren(true);
        var saved = _palette.SavedColors;
        for (int i = 0; i < saved.Count; i++)
        {
            var index = i;
            var swatch = PaletteSwatch(_savedRow, saved[i]);
            swatch.AddEventListener("onrightclick", () =>
            {
                _menuSwatch = index;
                _swatchMenu.Open(swatch, Popup.PositionMode.UnderMouse);
            });
        }

        var add = _savedRow.Add.Icon("add", "add");
        add.AddEventListener("onclick", () => ChangePalette(() => _palette.AddSaved(_color)));
    }

    private ColorSwatch PaletteSwatch(Panel row, PickerColor color)
    {
        var swatch = row.AddChild(new ColorSwatch { Compact = true });
        swatch.AddClass("compact");
        swatch.Set(color, HasAlpha, IsHdr);
        swatch.AddEventListener("onclick", () => PickColor(color));
        return swatch;
    }

    private void SyncPaletteUsage()
    {
        foreach (var swatch in _recentRow.Children.Concat(_savedRow.Children).OfType<ColorSwatch>())
        {
            swatch.Set(swatch.Color, HasAlpha, IsHdr);
        }
    }
}
