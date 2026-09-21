using SkiaSharp;
using Topten.RichTextKit;

namespace Socotra;

internal sealed class FontManager : FontMapper, ICharacterMatcher
{
    public static readonly FontManager Instance = new();

    private readonly Lock _gate = new();
    private readonly Dictionary<(string Family, int Weight, int Width, SKFontStyleSlant Slant), SKTypeface> _loaded = [];
    private readonly Dictionary<(string Family, int Weight, bool Italic), SKTypeface> _matches = [];
    private int _generation;

    static FontManager() => FontFallback.CharacterMatcher = Instance;

    public int Generation => Volatile.Read(ref _generation);

    public IReadOnlyList<string> Families
    {
        get
        {
            lock (_gate)
            {
                return [.. _loaded.Values.Select(face => face.FamilyName).Distinct(StringComparer.OrdinalIgnoreCase)];
            }
        }
    }

    public void Load(Stream stream)
    {
        var face = SKTypeface.FromStream(stream);
        if (face is null)
        {
            return;
        }

        lock (_gate)
        {
            if (!_loaded.TryAdd((face.FamilyName, face.FontWeight, face.FontWidth, face.FontSlant), face))
            {
                face.Dispose();
                return;
            }

            _matches.Clear();
            _generation++;
        }
    }

    public override SKTypeface TypefaceFromStyle(IStyle style, bool ignoreFontVariants)
    {
        var key = (style.FontFamily, style.FontWeight, style.FontItalic);
        SKTypeface? loaded;
        int generation;
        lock (_gate)
        {
            if (_matches.TryGetValue(key, out var match))
            {
                return match;
            }

            loaded = BestLoadedFace(style);
            generation = _generation;
        }

        var face = loaded ?? SystemFace(style, ignoreFontVariants);
        lock (_gate)
        {
            if (generation == _generation)
            {
                _matches.TryAdd(key, face);
            }
        }

        return face;
    }

    public SKTypeface MatchCharacter(string familyName, int weight, int width, SKFontStyleSlant slant, string[] bcp47, int character)
    {
        lock (_gate)
        {
            var match = _loaded.Values
                .Where(face => face.ContainsGlyph(character))
                .OrderBy(face => face.FontSlant == slant ? 0 : 1)
                .ThenBy(face => Math.Abs(face.FontWidth - width))
                .ThenBy(face => WeightDistance(face.FontWeight, weight))
                .ThenBy(face => face.FamilyName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(face => face.FontWidth)
                .ThenBy(face => face.FontSlant)
                .FirstOrDefault();
            if (match is not null)
            {
                return match;
            }
        }

        return SKFontManager.Default.MatchCharacter(familyName, weight, width, slant, bcp47, character);
    }

    private static int WeightDistance(int weight, int wanted)
    {
        bool preferHeavier = wanted == 400 || wanted > 500;
        bool preferredSide = (weight > wanted) == preferHeavier;
        return (Math.Abs(weight - wanted) * 2) + (preferredSide ? 0 : 1);
    }

    private SKTypeface? BestLoadedFace(IStyle style)
    {
        var family = _loaded.Values.Where(face => string.Equals(face.FamilyName, style.FontFamily, StringComparison.OrdinalIgnoreCase)).ToList();
        if (family.Count == 0)
        {
            return null;
        }

        var sameSlant = family.Where(face => face.IsItalic == style.FontItalic).ToList();
        return (sameSlant.Count > 0 ? sameSlant : family)
            .OrderBy(face => Math.Abs(face.FontWidth - (int)SKFontStyleWidth.Normal))
            .ThenBy(face => WeightDistance(face.FontWeight, style.FontWeight))
            .ThenBy(face => face.FontWidth)
            .ThenBy(face => face.FontSlant)
            .First();
    }

    private SKTypeface SystemFace(IStyle style, bool ignoreFontVariants)
    {
        if (!string.IsNullOrEmpty(style.FontFamily))
        {
            using var systemStyles = SKFontManager.Default.GetFontStyles(style.FontFamily);
            if (systemStyles.Count == 0)
            {
                Log.Warning($"Font \"{style.FontFamily}\" isn't loaded and the system doesn't have it.");
            }
        }

        return Default.TypefaceFromStyle(style, ignoreFontVariants);
    }
}
