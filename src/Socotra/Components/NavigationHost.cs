using System.IO.Enumeration;
using System.Web;
using Microsoft.AspNetCore.Components;
using Socotra.Html;

namespace Socotra;

/// <summary>
/// Shows one page at a time and lets you move between them by url, like a website. A page is any panel with
/// <c>@page "/url"</c> at the top of its Razor file. Pages keep their state when you leave them, and you can
/// go back and forward. In markup it's <c>&lt;navigator&gt;</c>.
/// </summary>
/// <example>
/// <code>
/// @* Starts on /home. Clicking a link shows that page, and the link gets the active class *@
/// &lt;navigator default="/home"&gt;
///     &lt;a href="/home"&gt;Home&lt;/a&gt;
///     &lt;a href="/settings/audio"&gt;Audio&lt;/a&gt;
/// &lt;/navigator&gt;
///
/// @* SettingsPage.razor: shows for /settings/audio, /settings/video and so on,
///    with its public Section property set to the last part *@
/// @page "/settings/{Section}"
/// </code>
/// </example>
[StyleSheet.Inline("navigator", Styles)]
public class NavigationHost : Panel
{
    private const string Styles = """
        .navigator-body
        {
            &.hidden
            {
                display: none;
            }
        }
        """;

    private readonly Stack<HistoryItem> _back = new();
    private readonly Stack<HistoryItem> _forward = new();
    private readonly Dictionary<string, Type> _destinations = [];
    private HistoryItem? _current;

    /// <summary>The page showing now.</summary>
    public Panel? CurrentPanel => _current?.Panel;

    /// <summary>The url showing now, without its query.</summary>
    public string? CurrentUrl => _current?.Url;

    /// <summary>The query part of the url showing now, after the <c>?</c>.</summary>
    public string? CurrentQuery => _current?.Query;

    /// <summary>The url to go to when there isn't one, or when a url isn't found. Set with <c>default</c> in markup.</summary>
    public string? DefaultUrl { get; set; }

    /// <summary>The panel pages show up in. Put <c>slot="navigator-canvas"</c> on a child in markup to use it instead of the host.</summary>
    public Panel? NavigatorCanvas { get; set; }

    /// <summary>Every page visited so far. Going back to one shows it as you left it.</summary>
    protected List<HistoryItem> Cache { get; } = [];

    /// <summary>Uses the child marked <c>slot="navigator-canvas"</c> as <see cref="NavigatorCanvas"/>.</summary>
    public override void OnTemplateSlot(INode element, string slotName, Panel panel)
    {
        if (slotName == "navigator-canvas")
        {
            NavigatorCanvas = panel;
            foreach (var page in Cache)
            {
                page.Panel.Parent = NavigatorCanvas;
            }

            return;
        }

        base.OnTemplateSlot(element, slotName, panel);
    }

    /// <summary>Shows a <paramref name="type"/> panel at <paramref name="url"/>, for pages without a <see cref="RouteAttribute"/>.</summary>
    public void AddDestination(string url, Type type) => _destinations[url] = type;

    /// <summary>
    /// Shows the page for <paramref name="url"/> and returns it. Route variables and the query, like
    /// <c>?tab=2</c>, set the page's properties. Start the url with <c>~/</c> to go relative to the host this one is in.
    /// </summary>
    /// <param name="url">The url to show.</param>
    /// <param name="redirectToDefault">Pass <see langword="false"/> to stay on the current page when nothing matches, instead of going to <see cref="DefaultUrl"/>.</param>
    public Panel? Navigate(string? url, bool redirectToDefault = true)
    {
        var query = "";
        var originalUrl = url;
        if (url?.IndexOf('?') is { } queryIndex and >= 0)
        {
            query = url[(queryIndex + 1)..];
            url = url[..queryIndex];
        }

        var parent = Ancestors.OfType<NavigationHost>().FirstOrDefault();
        if (url?.StartsWith("~/", StringComparison.Ordinal) == true && parent is not null)
        {
            url = $"{parent.CurrentUrl}/{url[2..]}";
        }

        if (url == CurrentUrl)
        {
            if (_current is not null)
            {
                _current.Query = query;
            }

            ApplyQuery(query);
            RunNavigatedEvent();
            return _current?.Panel;
        }

        NavigatorCanvas ??= this;
        if (FindTarget(url, parent?.CurrentUrl) is not { } target)
        {
            if (DefaultUrl is not null && redirectToDefault)
            {
                Navigate(DefaultUrl, false);
                return _current?.Panel;
            }

            NotFound(url);
            return _current?.Panel;
        }

        var parts = target.Url.Split('/', StringSplitOptions.RemoveEmptyEntries);
        bool foundPartial = target.Url.Contains('*');
        if (foundPartial)
        {
            url = string.Join("/", url!.Split("/").Take(parts.Length));
        }

        _forward.Clear();
        if (_current is not null)
        {
            _back.Push(_current);
            _current.Panel.AddClass("hidden");
            (_current.Panel as INavigatorPage)?.OnNavigationClose();
            _current = null;
        }

        if (Cache.FirstOrDefault(x => x.Url == url) is { } cached)
        {
            cached.Panel.RemoveClass("hidden");
            cached.Query = query;
            _current = cached;
            _current.Panel.Parent = NavigatorCanvas;
            RunNavigatedEvent();
        }
        else
        {
            if (Activator.CreateInstance(target.Type) is not Panel panel)
            {
                Log.Warning($"Found a route - but couldn't create the panel ({target.Type})");
                return _current?.Panel;
            }

            panel.AddClass("navigator-body");
            _current = new HistoryItem { Panel = panel, Url = url!, Query = query };
            panel.Parent = NavigatorCanvas;
            foreach (var (key, value) in ExtractProperties(parts, url!))
            {
                panel.SetProperty(key, value);
            }

            Cache.Add(_current);
            StateHasChanged();
            RunNavigatedEvent();
        }

        if (foundPartial)
        {
            var childNavigator = _current.Panel as NavigationHost ?? _current.Panel.Descendants.OfType<NavigationHost>().FirstOrDefault();
            childNavigator?.Navigate(originalUrl);
        }

        (_current.Panel as INavigatorPage)?.OnNavigationOpen();
        ApplyQuery(query);
        return _current.Panel;
    }

    /// <summary>Pairs each <c>{variable}</c> in the route <paramref name="parts"/> with its value from <paramref name="url"/>. A variable the url doesn't reach gets null.</summary>
    public IEnumerable<(string key, string? value)> ExtractProperties(string[] parts, string url) => RouteAttribute.ExtractProperties(parts, url);

    /// <summary>
    /// Whether the url showing now matches <paramref name="url"/>. You can use <c>*</c> and <c>?</c> wildcards,
    /// start with <c>~</c> to match only the end, and add a query whose values all have to match.
    /// </summary>
    public bool CurrentUrlMatches(string? url)
    {
        if (url is null)
        {
            return CurrentUrl is null;
        }

        var query = "";
        if (url.IndexOf('?') is var queryIndex and >= 0)
        {
            query = url[(queryIndex + 1)..];
            url = url[..queryIndex];
        }

        if (url.StartsWith('~'))
        {
            if (CurrentUrl?.EndsWith(url[1..], StringComparison.Ordinal) != true)
            {
                return false;
            }
        }
        else if (CurrentUrl is null || !FileSystemName.MatchesSimpleExpression(url, CurrentUrl, ignoreCase: true))
        {
            return false;
        }

        return QueryMatches(query);
    }

    /// <summary>Handles the <c>default</c> attribute, which sets <see cref="DefaultUrl"/>.</summary>
    public override void SetProperty(string name, string? value)
    {
        base.SetProperty(name, value);
        if (name == "default")
        {
            DefaultUrl = value;
        }
    }

    /// <summary>Goes back until the url no longer matches <paramref name="wildcard"/>. Returns false if it ran out of history first.</summary>
    public virtual bool GoBackUntilNot(string wildcard)
    {
        if (!GoBack())
        {
            return false;
        }

        return !FileSystemName.MatchesSimpleExpression(wildcard, _current!.Url, ignoreCase: true) || GoBackUntilNot(wildcard);
    }

    /// <summary>Goes back to the previous page. Returns false if there isn't one.</summary>
    public virtual bool GoBack()
    {
        if (!_back.TryPop(out var result))
        {
            return false;
        }

        if (!Cache.Contains(result) || result.Panel.IsDeleted)
        {
            return GoBack();
        }

        if (_current is not null)
        {
            _forward.Push(_current);
        }

        Switch(result);
        return true;
    }

    /// <summary>Goes forward again after going back. Returns false if there's nowhere to go.</summary>
    public virtual bool GoForward()
    {
        if (!_forward.TryPop(out var result))
        {
            return false;
        }

        if (!Cache.Contains(result) || result.Panel.IsDeleted)
        {
            return GoForward();
        }

        if (_current is not null)
        {
            _back.Push(_current);
        }

        Switch(result);
        return true;
    }

    /// <summary>Goes to <see cref="DefaultUrl"/> if no page is showing yet. Call the base method if you override it.</summary>
    protected override void OnParametersSet()
    {
        if (DefaultUrl is not null && CurrentUrl is null)
        {
            Navigate(DefaultUrl);
        }
    }

    /// <summary>Override to handle a url that no page matches. Logs a warning unless you override it.</summary>
    protected virtual void NotFound(string? url)
    {
        if (url is not null)
        {
            Log.Warning($"Url Not Found: {url}");
        }
    }

    /// <summary>Goes back when the mouse's back button is pressed over the host.</summary>
    protected override void OnBack(PanelEvent e)
    {
        if (GoBack())
        {
            e.StopPropagation();
        }
    }

    /// <summary>Goes forward when the mouse's forward button is pressed over the host.</summary>
    protected override void OnForward(PanelEvent e)
    {
        if (GoForward())
        {
            e.StopPropagation();
        }
    }

    private static bool DoesUrlMatch(string? url, string target) => new RouteAttribute(target).IsUrl(url);

    private (string Url, Type Type)? FindTarget(string? url, string? currentUrl)
    {
        foreach (var (destination, type) in _destinations)
        {
            if (DoesUrlMatch(url, destination))
            {
                return (destination, type);
            }
        }

        return RouteAttribute.FindValidTarget(url, currentUrl) is { } found ? (found.Attribute.Url, found.Type) : null;
    }

    private void ApplyQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query) || _current is null)
        {
            return;
        }

        var parts = HttpUtility.ParseQueryString(query);
        foreach (var key in parts.AllKeys.OfType<string>())
        {
            _current.Panel.SetProperty(key, parts.Get(key));
        }
    }

    private bool QueryMatches(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        var wanted = HttpUtility.ParseQueryString(query);
        var have = HttpUtility.ParseQueryString(CurrentQuery ?? "");
        return wanted.AllKeys.OfType<string>().All(key => have.Get(key) == wanted.Get(key));
    }

    private void Switch(HistoryItem item)
    {
        if (_current == item)
        {
            return;
        }

        (_current?.Panel as INavigatorPage)?.OnNavigationClose();
        _current?.Panel.AddClass("hidden");
        _current = item;
        _current.Panel.RemoveClass("hidden");
        (_current.Panel as INavigatorPage)?.OnNavigationOpen();
        RunNavigatedEvent();
    }

    private void RunNavigatedEvent()
    {
        foreach (var listener in Descendants.OfType<INavigationEvent>())
        {
            listener.OnNavigated(CurrentUrl);
        }
    }

    /// <summary>A page the host has visited.</summary>
    protected class HistoryItem
    {
        /// <summary>The page.</summary>
        public required Panel Panel { get; init; }

        /// <summary>Its url, without the query.</summary>
        public required string Url { get; init; }

        /// <summary>The query it was last shown with, like <c>tab=2</c>.</summary>
        public string Query { get; set; } = "";
    }
}

/// <summary>For panels inside a <see cref="NavigationHost"/> that want to know when its url changes.</summary>
public interface INavigationEvent
{
    /// <summary>Called when the host's url changes.</summary>
    void OnNavigated(string? url);
}

/// <summary>For pages of a <see cref="NavigationHost"/> that want to know when they're shown and hidden.</summary>
public interface INavigatorPage
{
    /// <summary>Called when the page is shown.</summary>
    public virtual void OnNavigationOpen()
    {
    }

    /// <summary>Called when the page is hidden, because another page was navigated to.</summary>
    public virtual void OnNavigationClose()
    {
    }
}

/// <summary>Navigation helpers for panels.</summary>
public static class NavigationExtensions
{
    extension(Panel panel)
    {
        /// <summary>The closest <see cref="NavigationHost"/>: this panel or one of its ancestors.</summary>
        public NavigationHost? GetNavigator() => panel.AncestorsAndSelf.OfType<NavigationHost>().FirstOrDefault();

        /// <summary>Navigates the closest <see cref="NavigationHost"/> to <paramref name="url"/>, and returns the page it shows.</summary>
        public Panel? Navigate(string? url) => panel.GetNavigator()?.Navigate(url);
    }
}
