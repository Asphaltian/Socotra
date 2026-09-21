using Microsoft.AspNetCore.Components;

namespace Socotra;

/// <summary>
/// A strip of commands and controls, left to right or top to bottom. Everything in it keeps its natural size, and the
/// strip scrolls when they don't fit. Add any panel to it to put other controls in it. The arrow keys, Home and End
/// move between its buttons.
/// </summary>
/// <example>
/// <code>
/// // Save and undo buttons, a toggle, and a menu pushed to the far end
/// var toolbar = new Toolbar { Parent = window };
/// toolbar.AddButton("Save", "save", Save);
/// toolbar.AddButton("", "undo", Undo).Tooltip = "Undo";
/// toolbar.AddSeparator();
/// toolbar.AddToggle("Grid", "grid_on", true, on => grid.Visible = on);
/// toolbar.AddSpacer();
/// toolbar.AddMenu("View", "visibility", viewMenu);
/// </code>
/// </example>
[StyleSheet.Inline("toolbar", Styles)]
public class Toolbar : Panel
{
    private const string Styles = """
        .toolbar
        {
            flex-direction: row;
            align-items: center;
            flex-shrink: 0;
            min-width: 0;
            min-height: 0;
            overflow-x: scroll;
            overflow-y: hidden;
            pointer-events: all;
        }
        .toolbar > * { flex-shrink: 0; }
        .toolbar > .button
        {
            align-items: center;
            justify-content: center;
            white-space: nowrap;
        }
        .toolbar > .button:disabled { pointer-events: none; }
        .toolbar > .toolbar-spacer { flex-grow: 1; flex-shrink: 1; }
        .toolbar.vertical
        {
            flex-direction: column;
            align-items: stretch;
            overflow-x: hidden;
            overflow-y: scroll;
        }
        """;

    /// <summary>Makes an empty toolbar.</summary>
    public Toolbar()
    {
        AddClass("toolbar");
    }

    /// <summary>Lays the toolbar out top to bottom instead of left to right. It adds the <c>vertical</c> class.</summary>
    [Parameter]
    public bool Vertical
    {
        get => HasClass("vertical");
        set => SetClass("vertical", value);
    }

    /// <summary>Adds a button that calls <paramref name="clicked"/> unless it's disabled. Pass empty text for a button with only an icon, and give it a <see cref="Panel.Tooltip"/>.</summary>
    public Button AddButton(string? text, string? icon, Action? clicked)
    {
        var button = AddChild(new Button(text, icon));
        button.AddEventListener("onclick", () =>
        {
            if (!button.Disabled)
            {
                clicked?.Invoke();
            }
        });
        return button;
    }

    /// <summary>Adds a button that turns on and off. Its <see cref="Button.Active"/> is the current state, starting at <paramref name="value"/>, and <paramref name="changed"/> is called with each new state.</summary>
    public Button AddToggle(string? text, string? icon, bool value, Action<bool>? changed)
    {
        Button? button = null;
        button = AddButton(text, icon, () =>
        {
            button!.Active = !button.Active;
            changed?.Invoke(button.Active);
        });
        button.Active = value;
        return button;
    }

    /// <summary>Adds a button that opens <paramref name="menu"/>. The toolbar owns the menu and deletes it along with the button.</summary>
    public Button AddMenu(string? text, string? icon, Menu menu)
    {
        ArgumentNullException.ThrowIfNull(menu);
        return AddChild(new MenuButton(text, icon, menu));
    }

    /// <summary>Adds a divider across the toolbar.</summary>
    public Panel AddSeparator() => Add.Panel("toolbar-separator");

    /// <summary>Adds space that pushes whatever comes after it to the far end of the toolbar.</summary>
    public Panel AddSpacer() => Add.Panel("toolbar-spacer");

    /// <summary>Moves focus between the buttons with the arrow keys, Home and End. Controls in the toolbar that use those keys keep them.</summary>
    public override void OnButtonTyped(ButtonEvent e)
    {
        if (MovesFocus(e, out var buttons, out var index))
        {
            var target = e.Button switch
            {
                "home" => 0,
                "end" => buttons.Length - 1,
                _ => (index + (e.Button == (Vertical ? "down" : "right") ? 1 : -1) + buttons.Length) % buttons.Length,
            };
            buttons[target].Focus();
            buttons[target].ScrollAncestorsIntoView();
            e.StopPropagation = true;
            return;
        }

        base.OnButtonTyped(e);
    }

    private bool MovesFocus(ButtonEvent e, out Button[] buttons, out int index)
    {
        buttons = [.. Children.OfType<Button>().Where(static button => button.IsVisible && !button.Disabled && button.AcceptsFocus)];
        index = Array.FindIndex(buttons, static button => button.HasFocus);
        var along = Vertical ? e.Button is "up" or "down" : e.Button is "left" or "right";
        return index >= 0 && (along || e.Button is "home" or "end");
    }

    private sealed class MenuButton : Button
    {
        private readonly Menu _menu;

        public MenuButton(string? text, string? icon, Menu menu)
            : base(text, icon)
        {
            _menu = menu;
            Add.Icon("expand_more", "toolbar-chevron");
        }

        public override void Tick()
        {
            Active = _menu.IsOpen;
            base.Tick();
        }

        public override void OnDeleted()
        {
            _menu.Delete(true);
            base.OnDeleted();
        }

        protected override void OnClick(MousePanelEvent e)
        {
            base.OnClick(e);
            if (Disabled)
            {
                return;
            }

            if (_menu.IsOpen)
            {
                _menu.Close();
            }
            else
            {
                _menu.Open(this, Parent is Toolbar { Vertical: true } ? Popup.PositionMode.RightTop : Popup.PositionMode.BelowLeft, 4);
            }

            e.StopPropagation();
        }
    }
}
