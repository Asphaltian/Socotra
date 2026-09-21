namespace Socotra;

/// <summary>
/// A list of labeled controls, one <see cref="Field"/> per row, with optional headers between groups of rows. When a
/// control in it sends <c>onchange</c>, the form sends <c>form.changed</c> instead.
/// </summary>
/// <example>
/// <code>
/// // A settings form that saves whenever something in it changes
/// var form = new Form { Parent = window };
/// form.AddHeader("Audio", "volume_up");
/// form.AddRow("Music", musicSlider);
/// form.AddRow("Name", new TextEntry());
/// form.AddEventListener("form.changed", SaveSettings);
/// </code>
/// </example>
public class Form : Panel
{
    /// <summary>Makes an empty form.</summary>
    public Form()
    {
        AddClass("form");
    }

    /// <summary>Adds a row with <paramref name="entryTitle"/> as its label and <paramref name="control"/> next to it.</summary>
    public void AddRow(string? entryTitle, Panel control)
    {
        var row = AddChild<Field>();
        var title = row.Add.Panel("label");
        title.Add.Label(entryTitle);
        var value = row.AddChild<FieldControl>();
        control.Parent = value;
    }

    /// <summary>Adds a header showing <paramref name="title"/> and the icon named <paramref name="icon"/>, above the rows that follow.</summary>
    public void AddHeader(string? title, string? icon = "category")
    {
        var row = Add.Panel("field-header");
        row.Add.Icon(icon);
        row.Add.Label(title);
    }

    /// <summary>Deletes every row and header.</summary>
    public void Clear() => DeleteChildren(true);

    /// <summary>Turns <c>onchange</c> from the controls into <c>form.changed</c>.</summary>
    protected override void OnEvent(PanelEvent e)
    {
        if (e.Name == "onchange")
        {
            CreateEvent("form.changed");
            return;
        }

        base.OnEvent(e);
    }
}
