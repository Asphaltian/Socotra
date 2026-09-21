using System.Text.RegularExpressions;

namespace Socotra;

/// <summary>
/// A stylesheet you attach to a panel through its <see cref="Panel.StyleSheet"/>. Write it in CSS, or in SCSS with
/// nesting, <c>&amp;</c>, variables, mixins, imports and <c>@keyframes</c>.
/// </summary>
/// <example><code>
/// // Load a file for a panel and everything inside it
/// panel.StyleSheet.Load("assets/ui/menu.scss");
///
/// // Or write one in code
/// var sheet = StyleSheet.FromString("$accent: #f80; .button { color: $accent; &amp;:hover { opacity: 0.8; } }");
/// panel.StyleSheet.Add(sheet);
/// </code></example>
public sealed partial class StyleSheet
{
    private static readonly Dictionary<string, StyleSheet> Loaded = new(StringComparer.OrdinalIgnoreCase);

    private Dictionary<string, List<StyleBlock>> _byClass = new(StringComparer.OrdinalIgnoreCase);
    private List<StyleBlock> _other = [];

    internal StyleSheet(string fileName)
    {
        FileName = fileName;
    }

    /// <summary>The file it was loaded from, or the name you gave <see cref="FromString"/> or <see cref="FromInline"/>.</summary>
    public string FileName { get; internal set; }

    internal List<StyleBlock> Blocks { get; } = [];

    internal Dictionary<string, string> Variables { get; } = new(StringComparer.OrdinalIgnoreCase);

    internal Dictionary<string, MixinDefinition> Mixins { get; } = new(StringComparer.OrdinalIgnoreCase);

    internal Dictionary<string, KeyFrames> KeyFrames { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Loads a stylesheet file. A relative <paramref name="path"/> starts from your program's folder. Pass
    /// <paramref name="variables"/> to give the file SCSS variables it can use, like <c>("$accent", "#f80")</c>.
    /// </summary>
    /// <exception cref="FormatException">The stylesheet has a mistake in it. The message says where.</exception>
    public static StyleSheet FromFile(string path, IEnumerable<(string Key, string Value)>? variables = null)
    {
        var fullPath = Path.GetFullPath(path, AppContext.BaseDirectory);
        var given = variables?.ToArray() ?? [];
        var key = string.Join(';', given.Select(v => $"{v.Key}: {v.Value}").Prepend(fullPath));
        lock (Loaded)
        {
            if (!Loaded.TryGetValue(key, out var sheet))
            {
                sheet = StyleParser.ParseFile(fullPath, given);
                Loaded[key] = sheet;
            }

            return sheet;
        }
    }

    /// <summary>
    /// Reads a stylesheet from <paramref name="styles"/>. Error messages name <paramref name="fileName"/>. When it's a full
    /// path, imports and <c>url()</c>s start from its folder, and otherwise from your program's folder. Pass
    /// <paramref name="variables"/> to give it SCSS variables it can use.
    /// </summary>
    /// <exception cref="FormatException">The stylesheet has a mistake in it. The message says where.</exception>
    public static StyleSheet FromString(string styles, string fileName = "none", IEnumerable<(string Key, string Value)>? variables = null) =>
        StyleParser.ParseString(styles, fileName, variables);

    /// <summary>Reads a stylesheet from <paramref name="styles"/> the first time you use <paramref name="key"/>. After that, the same key gives you the same stylesheet back.</summary>
    public static StyleSheet FromInline(string styles, string key)
    {
        lock (Loaded)
        {
            if (!Loaded.TryGetValue(key, out var sheet))
            {
                sheet = StyleParser.ParseString(styles, key, null);
                Loaded[key] = sheet;
            }

            return sheet;
        }
    }

    /// <summary>The value of an SCSS variable in this stylesheet, like <c>GetVariable("$accent")</c>, or <paramref name="defaultValue"/> if it isn't set.</summary>
    public string? GetVariable(string name, string? defaultValue = null) => Variables.GetValueOrDefault(name, defaultValue!);

    internal void SetVariable(string key, string value, bool isDefault = false)
    {
        if (isDefault && Variables.ContainsKey(key))
        {
            return;
        }

        Variables[key] = ReplaceVariables(value);
    }

    internal string ReplaceVariables(string text)
    {
        if (!text.Contains('$'))
        {
            return text;
        }

        return VariablePattern().Replace(text, match => Variables.TryGetValue(match.Value, out var value)
            ? value
            : throw new FormatException($"Unknown variable '{match.Value}'"));
    }

    internal void BuildIndex()
    {
        _byClass = new(StringComparer.OrdinalIgnoreCase);
        _other = [];
        foreach (var block in Blocks)
        {
            foreach (var selector in block.Selectors)
            {
                if (selector.Classes is [var first, ..])
                {
                    if (!_byClass.TryGetValue(first, out var list))
                    {
                        _byClass[first] = list = [];
                    }

                    list.Add(block);
                }
                else
                {
                    _other.Add(block);
                }
            }
        }
    }

    internal void GatherCandidates(IEnumerable<string> classes, IStyleTarget target, HashSet<StyleBlock> seen, List<StyleBlock> output)
    {
        Take(_other, target, seen, output);
        foreach (var className in classes)
        {
            if (_byClass.TryGetValue(className, out var list))
            {
                Take(list, target, seen, output);
            }
        }
    }

    private static void Take(List<StyleBlock> blocks, IStyleTarget target, HashSet<StyleBlock> seen, List<StyleBlock> output)
    {
        foreach (var block in blocks)
        {
            if (seen.Add(block) && block.TestBroadphase(target))
            {
                output.Add(block);
            }
        }
    }

    [GeneratedRegex(@"\$[A-Za-z_][A-Za-z0-9_-]*")]
    private static partial Regex VariablePattern();

    /// <summary>
    /// Put this on a panel class to give every panel of that type a stylesheet written right in the code.
    /// Give each one its own <paramref name="name"/>; two classes using the same name get the same stylesheet.
    /// </summary>
    /// <example><code>
    /// // Every Badge gets these styles
    /// [StyleSheet.Inline("badge", "badge { padding: 4px; border-radius: 4px; }")]
    /// public class Badge : Panel { }
    /// </code></example>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class InlineAttribute(string name, string styles) : Attribute
    {
        /// <summary>The stylesheet's name. It must be different for every stylesheet.</summary>
        public string Name => name;

        /// <summary>The stylesheet's CSS.</summary>
        public string Styles => styles;
    }
}
