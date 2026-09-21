namespace Socotra;

[StyleSheet.Inline("colortextentry", Styles)]
internal class ColorTextEntry : TextEntry
{
    private const string Styles = """
        .colortextentry.invalid
        {
            color: #ff6b6b;
        }
        """;

    public ColorTextEntry()
    {
        AddClass("colortextentry");
    }

    public Action<string>? ColorEntered { get; set; }

    public Action? Blurred { get; set; }

    public override void OnValueChanged()
    {
        base.OnValueChanged();
        var valid = Color.Parse(Text) is not null;
        SetClass("invalid", !valid);
        if (valid)
        {
            ColorEntered?.Invoke(Text);
        }
    }

    protected override void OnBlur(PanelEvent e)
    {
        base.OnBlur(e);
        SetClass("invalid", false);
        Blurred?.Invoke();
    }
}
