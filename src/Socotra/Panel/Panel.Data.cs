namespace Socotra;

public partial class Panel
{
    /// <summary>Sends a <c>{name}.changed</c> event with <paramref name="value"/>. Call it when the user changes a value, so listeners hear about it.</summary>
    protected void CreateValueEvent(string name, object? value) => CreateEvent($"{name}.changed", value);
}
