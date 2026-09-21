namespace Socotra;

/// <summary>
/// Load your font files here, then use them in <c>font-family</c> by the family name stored in the file.
/// Fonts installed on the system work too, and fill in any characters your font doesn't have.
/// </summary>
/// <example>
/// <code>
/// // Load every font in the assets/fonts folder next to the program
/// Fonts.LoadFolder("assets/fonts");
///
/// // Then use a family by name in your SCSS: font-family: Noto Sans;
/// </code>
/// </example>
public static class Fonts
{
    /// <summary>The family names you've loaded, as you'd write them in <c>font-family</c>.</summary>
    public static IReadOnlyList<string> Families => FontManager.Instance.Families;

    /// <summary>Loads every <c>.ttf</c> and <c>.otf</c> file in a folder and its subfolders. Relative paths start at the program's folder.</summary>
    public static void LoadFolder(string path)
    {
        var folder = Path.GetFullPath(path, AppContext.BaseDirectory);
        var files = Directory.EnumerateFiles(folder, "*.ttf", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(folder, "*.otf", SearchOption.AllDirectories));
        Parallel.ForEach(files, Load);
    }

    /// <summary>Loads one <c>.ttf</c> or <c>.otf</c> file. Relative paths start at the program's folder.</summary>
    public static void Load(string path)
    {
        using var stream = File.OpenRead(Path.GetFullPath(path, AppContext.BaseDirectory));
        FontManager.Instance.Load(stream);
    }
}
