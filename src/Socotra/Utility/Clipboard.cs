namespace Socotra;

/// <summary>
/// Copy and paste text. Hook it up to your platform's clipboard once at startup by setting
/// <see cref="GetTextHandler"/> and <see cref="SetTextHandler"/>. Until you do, copying does nothing and pasting finds nothing.
/// </summary>
/// <example>
/// <code>
/// // At startup, connect Socotra to SDL's clipboard
/// Clipboard.GetTextHandler = () =&gt; SDL_GetClipboardText();
/// Clipboard.SetTextHandler = text =&gt; SDL_SetClipboardText(text);
///
/// // Then anywhere in your UI code
/// Clipboard.SetText("Hello");
/// var pasted = Clipboard.GetText();
/// </code>
/// </example>
public static class Clipboard
{
    /// <summary>Set this to a function that returns the text on your platform's clipboard, or null if there isn't any.</summary>
    public static Func<string?>? GetTextHandler { get; set; }

    /// <summary>Set this to a function that puts text on your platform's clipboard.</summary>
    public static Action<string>? SetTextHandler { get; set; }

    /// <summary>Puts <paramref name="text"/> on the clipboard. Empty text leaves the clipboard as it was.</summary>
    public static void SetText(string text)
    {
        if (!string.IsNullOrEmpty(text))
        {
            SetTextHandler?.Invoke(text);
        }
    }

    /// <summary>The text on the clipboard, or null if there isn't any.</summary>
    public static string? GetText() => GetTextHandler?.Invoke();
}
