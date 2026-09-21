using Microsoft.AspNetCore.Components;

namespace Socotra;

public partial class CurveEditor
{
    private CurveChannel[]? _channels;

    /// <summary>
    /// The curves being edited, each with its own name and color, like the red, green and blue of a color over time. Each
    /// keeps its own time and value units. Setting it edits these curves and forgets the undo history, without calling
    /// <see cref="ChannelsChanged"/>.
    /// </summary>
    /// <exception cref="ArgumentException">There are no curves, or a key's time or value isn't a finite number.</exception>
    [Parameter]
    public IReadOnlyList<CurveChannel> Channels
    {
        get => _curves.Select((curve, index) => new CurveChannel(ChannelName(index), ChannelColor(index), curve)).ToArray();
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (value.Count == 0)
            {
                throw new ArgumentException("At least one curve is required.", nameof(value));
            }

            var channels = value.ToArray();
            Assign([.. channels.Select(x => Normalize(x.Value))], channels);
        }
    }

    /// <summary>Called with every curve after each change the user makes to any of them. The list is a copy.</summary>
    [Parameter]
    public Action<IReadOnlyList<CurveChannel>>? ChannelsChanged { get; set; }

    /// <summary>Shows a panel beside the graph for typing the selected key's time and value and setting its tangents. It's hidden by default.</summary>
    [Parameter]
    public bool ShowInspector
    {
        get => HasClass("show-inspector");
        set
        {
            if (ShowInspector == value)
            {
                return;
            }

            if (!value)
            {
                EndEdit();
                _canvas.Focus();
            }

            SetClass("show-inspector", value);
            _tools.Sync();
        }
    }

    private string ChannelName(int index) => _channels is not null ? _channels[index].Name : IsRange ? (index == 0 ? "A" : "B") : "Curve";

    private Color ChannelColor(int index) => _channels is not null ? _channels[index].Color : _canvas.ComputedStyle?.FontColor ?? Color.White;

    private bool SameChannels(CurveChannel[]? channels) => _channels is null
        ? channels is null
        : channels is not null && _channels.Length == channels.Length && _channels.Zip(channels).All(x => x.First.Name == x.Second.Name && x.First.Color == x.Second.Color);

    /// <summary>A named curve and the color it's drawn in.</summary>
    /// <param name="Name">What the curve is called.</param>
    /// <param name="Color">The color the curve is drawn in.</param>
    /// <param name="Value">The curve.</param>
    public readonly record struct CurveChannel(string Name, Color Color, Curve Value);
}
