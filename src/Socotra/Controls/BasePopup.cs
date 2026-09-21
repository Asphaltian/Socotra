using Microsoft.AspNetCore.Components;

namespace Socotra;

/// <summary>A panel that closes by itself when the user presses a mouse button anywhere outside it.</summary>
public abstract class BasePopup : Panel
{
    /// <summary>Keeps the popup open through <see cref="RootPanel.ClosePopups"/>, so only closing it yourself or deleting it takes it away.</summary>
    [Parameter]
    public bool StayOpen { get; set; }

    /// <summary>
    /// The popup this one was opened from, like the menu a submenu hangs off. Override it so a click in this popup
    /// doesn't close the popups it came from.
    /// </summary>
    protected virtual BasePopup? ParentPopup => null;

    internal static void CloseAll(RootPanel root, Panel? exceptThisOne)
    {
        var floater = exceptThisOne?.AncestorsAndSelf.OfType<BasePopup>().FirstOrDefault();
        foreach (var popup in root.Descendants.OfType<BasePopup>().ToArray())
        {
            if (popup.IsDeleting || popup.IsDeleted || popup.StayOpen || popup == floater || floater?.IsInChainOf(popup) == true)
            {
                continue;
            }

            try
            {
                popup.Delete();
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }
    }

    private bool IsInChainOf(BasePopup ancestor)
    {
        for (var popup = ParentPopup; popup is not null; popup = popup.ParentPopup)
        {
            if (popup == ancestor)
            {
                return true;
            }
        }

        return false;
    }
}
