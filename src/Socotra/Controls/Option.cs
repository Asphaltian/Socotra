namespace Socotra;

/// <summary>One choice in a <see cref="DropDown"/> or <see cref="ButtonGroup"/>: the text shown for it and the value it stands for.</summary>
public class Option
{
    /// <summary>Makes an empty option.</summary>
    public Option()
    {
    }

    /// <summary>Makes an option showing <paramref name="title"/> that stands for <paramref name="value"/>.</summary>
    public Option(string? title, object? value)
    {
        Title = title;
        Value = value;
    }

    /// <summary>Makes an option showing <paramref name="title"/> and the icon named <paramref name="icon"/> that stands for <paramref name="value"/>.</summary>
    public Option(string? title, string? icon, object? value)
    {
        Title = title;
        Icon = icon;
        Value = value;
    }

    /// <summary>The text the user sees.</summary>
    public string? Title { get; set; }

    /// <summary>The name of an icon shown with the <see cref="Title"/>.</summary>
    public string? Icon { get; set; }

    /// <summary>The value the option stands for, which the control reports when it's picked.</summary>
    public object? Value { get; set; }
}
