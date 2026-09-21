namespace Socotra;

public partial class Panel
{
    /// <summary>The transitions running on this panel.</summary>
    public Transitions Transitions { get; }

    /// <summary>Whether any transitions are running.</summary>
    public bool HasActiveTransitions => Transitions.HasAny;

    /// <summary>Jumps straight to the end of any transitions running or about to start.</summary>
    public void SkipTransitions() => Style.SkipTransitions = true;
}
