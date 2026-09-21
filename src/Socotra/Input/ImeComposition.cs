namespace Socotra;

internal static class ImeComposition
{
    public static bool Update(Panel? focus, bool composing, string? text)
    {
        if (!string.IsNullOrEmpty(text))
        {
            if (!composing)
            {
                focus?.CreateEvent("onimestart");
            }

            focus?.CreateEvent("onime", text);
            return true;
        }

        if (composing)
        {
            focus?.CreateEvent("onime", "");
            focus?.CreateEvent("onimeend");
        }

        return false;
    }
}
