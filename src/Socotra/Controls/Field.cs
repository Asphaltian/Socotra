namespace Socotra;

/// <summary>A row in a <see cref="Form"/>, usually a label and a control. It has the <c>field</c> class.</summary>
public class Field : Panel
{
    /// <summary>Makes an empty field.</summary>
    public Field()
    {
        AddClass("field");
    }
}

/// <summary>The part of a <see cref="Field"/> that holds the control. It has the <c>field-control</c> and <c>control</c> classes.</summary>
public class FieldControl : Panel
{
    /// <summary>Makes an empty field control.</summary>
    public FieldControl()
    {
        AddClass("field-control control");
    }
}
