using Microsoft.AspNetCore.Components;
using Socotra.Tests.Razor;
using static Socotra.Tests.ControlTesting;

namespace Socotra.Tests;

public class NavigationTests
{
    private static (RootPanel Root, NavigationHost Host) Host()
    {
        var root = new RootPanel();
        var host = root.AddChild<NavigationHost>();
        Update(root);
        return (root, host);
    }

    [Fact]
    public void RoutesMatchPartsVariablesAndWildcards()
    {
        var route = new RouteAttribute("/profile/{name}/*");

        Assert.Equal(["profile", "{name}", "*"], route.Parts);
        Assert.True(route.IsUrl("/profile/mudkip/stats?tab=1"));
        Assert.False(route.IsUrl("/settings/mudkip"));
        Assert.False(new RouteAttribute("/profile/{name}").IsUrl("/profile/mudkip/stats"));
        Assert.Equal([("name", "mudkip")], route.ExtractProperties("/profile/mudkip/stats"));
    }

    [Fact]
    public void PageDirectivesAreFound()
    {
        var found = RouteAttribute.FindValidTarget("/profile/treecko", null);

        Assert.Equal(typeof(ProfilePage), found?.Type);
        Assert.Null(RouteAttribute.FindValidTarget("/nowhere", null));
    }

    [Fact]
    public void NavigatingMakesThePageAndSetsItsProperties()
    {
        var (_, host) = Host();

        var page = Assert.IsType<ProfilePage>(host.Navigate("/profile/torchic?tab=moves"));

        Assert.Equal("torchic", page.Name);
        Assert.Equal("moves", page.Tab);
        Assert.Equal("/profile/torchic", host.CurrentUrl);
        Assert.Equal("tab=moves", host.CurrentQuery);
        Assert.Same(host, page.Parent);
        Assert.True(page.HasClass("navigator-body"));
        Assert.Equal(1, page.Opened);
    }

    [Fact]
    public void BackAndForwardReuseTheVisitedPages()
    {
        var (root, host) = Host();
        var home = host.Navigate("/home")!;
        var profile = (ProfilePage)host.Navigate("/profile/chikorita")!;
        Update(root);

        Assert.True(home.HasClass("hidden"));
        Assert.True(host.GoBack());
        Assert.Same(home, host.CurrentPanel);
        Assert.True(profile.HasClass("hidden") && !home.HasClass("hidden"));
        Assert.Equal(1, profile.Closed);

        Assert.True(host.GoForward());
        Assert.Same(profile, host.CurrentPanel);
        Assert.False(host.GoForward());

        Assert.Same(home, host.Navigate("/home"));
        Assert.Equal(2, host.Children.Count);
    }

    [Fact]
    public void UnknownUrlsGoToTheDefault()
    {
        var (_, host) = Host();
        host.DefaultUrl = "/home";

        Assert.IsType<HomePage>(host.Navigate("/missing"));
        Assert.Equal("/home", host.CurrentUrl);
    }

    [Fact]
    public void DestinationsCanBeAddedByHand()
    {
        var (_, host) = Host();
        host.AddDestination("/custom/{name}", typeof(ProfilePage));

        var page = Assert.IsType<ProfilePage>(host.Navigate("/custom/cyndaquil"));

        Assert.Equal("cyndaquil", page.Name);
    }

    [Fact]
    public void CurrentUrlMatchingUsesWildcardsSuffixesAndQueries()
    {
        var (_, host) = Host();
        host.Navigate("/profile/totodile?tab=moves&page=2");

        Assert.True(host.CurrentUrlMatches("/profile/*"));
        Assert.True(host.CurrentUrlMatches("~/totodile"));
        Assert.True(host.CurrentUrlMatches("/profile/totodile?tab=moves"));
        Assert.False(host.CurrentUrlMatches("/profile/totodile?tab=stats"));
        Assert.False(host.CurrentUrlMatches("/home"));
    }

    [Fact]
    public void MarkupSetsTheDefaultCanvasAndLinks()
    {
        var root = new RootPanel();
        var site = root.AddChild<Site>();
        Update(root);
        Update(root);
        Update(root);

        var host = site.Descendants.OfType<NavigationHost>().Single();
        var canvas = host.Children.Single(c => c.HasClass("canvas"));
        var link = site.Descendants.OfType<NavLinkPanel>().Single();

        Assert.Equal("/home", host.DefaultUrl);
        Assert.Same(canvas, host.NavigatorCanvas);
        Assert.IsType<HomePage>(Assert.Single(canvas.Children));
        Assert.Equal("/home", link.HRef);
        Assert.True(link.HasClass("active"));

        host.Navigate("/profile/pikachu");
        Update(root);
        Assert.False(link.HasClass("active"));

        link.CreateEvent(new MousePanelEvent("onclick", link, "mouseleft"));
        Update(root);
        Assert.Equal("/home", host.CurrentUrl);
    }

    [Fact]
    public void ButtonsWithAnHrefNavigateAndShowWhenActive()
    {
        var (root, host) = Host();
        var button = host.AddChild<Button>();
        button.Href = "/profile/eevee";

        button.CreateEvent(new MousePanelEvent("onmousedown", button, "mouseleft"));
        Update(root);
        Update(root);

        Assert.Equal("/profile/eevee", host.CurrentUrl);
        Assert.True(button.HasClass("active"));
    }

    [Fact]
    public void TheMouseBackButtonGoesBack()
    {
        var (root, host) = Host();
        host.Navigate("/home");
        host.Navigate("/profile/vulpix");

        host.CreateEvent(new PanelEvent("onback", host));
        Update(root);

        Assert.Equal("/home", host.CurrentUrl);
    }
}
