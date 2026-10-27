using Microsoft.AspNetCore.Components;

namespace Socotra;

/// <summary>
/// <c>CurveRangeControl</c> is a small preview of a <see cref="CurveRange"/>, showing both curves with the space between
/// them filled in. Set it with <see cref="RangeValue"/>, and you'll hear about the user's edits through
/// <see cref="RangeValueChanged"/>.
/// </summary>
public class CurveRangeControl : CurveControl
{
    /// <summary>The range shown.</summary>
    [Parameter]
    public CurveRange RangeValue
    {
        get => AppliedRange;
        set => AppliedRange = value;
    }

    /// <summary>Called with the range after every change the user makes to it in the editor.</summary>
    [Parameter]
    public Action<CurveRange>? RangeValueChanged { get; set; }

    /// <inheritdoc/>
    protected override bool IsRange => true;

    private protected override void OnRangeEdited(CurveRange value) => RangeValueChanged?.Invoke(value);
}
