namespace Socotra;

/// <summary>
/// A link: clicking it navigates the closest <see cref="NavigationHost"/> to <see cref="HRef"/>, and it has the
/// <c>active</c> class while that url is showing. Use it in markup as <c>&lt;a href="/settings"&gt;</c>.
/// </summary>
public class NavLinkPanel : Panel
{
    private NavigationHost? _navigator;

    /// <summary>The url to go to.</summary>
    public string? HRef { get; set; }

    /// <summary>The url pattern that makes the link active, if it's not <see cref="HRef"/>. See <see cref="NavigationHost.CurrentUrlMatches"/>.</summary>
    public string? Match { get; set; }

    /// <inheritdoc/>
    public override void OnParentChanged()
    {
        base.OnParentChanged();
        _navigator = Ancestors.OfType<NavigationHost>().FirstOrDefault();
    }

    /// <inheritdoc/>
    public override void Tick()
    {
        base.Tick();
        if (HRef is not null)
        {
            SetClass("active", _navigator?.CurrentUrlMatches(Match ?? HRef) ?? false);
        }
    }

    /// <summary>Navigates to <see cref="HRef"/> on a left click.</summary>
    protected override void OnClick(MousePanelEvent e)
    {
        if (e.Button == "mouseleft")
        {
            this.Navigate(HRef);
        }
    }
}
