using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Socotra;

/// <summary>
/// A curve through key frames, for easing, falloff and anything else that maps one number to another. Keys sit between 0
/// and 1 on both axes; <see cref="TimeRange"/> and <see cref="ValueRange"/> say what those mean in your own units. Curves
/// save to and load from JSON with <see cref="JsonSerializer"/>. Edit one with a <see cref="CurveEditor"/>.
/// </summary>
/// <example>
/// <code>
/// // Fade in over two seconds, easing out at the end
/// var fade = Curve.EaseOut;
/// fade.TimeRange = new Vector2(0, 2);
/// panel.Style.Opacity = fade.Evaluate(secondsSinceShown);
/// </code>
/// </example>
[JsonConverter(typeof(Curve.JsonConverter))]
public struct Curve
{
    /// <summary>A straight line from 0 to 1.</summary>
    public static readonly Curve Linear = new(new Frame(0, 0, -1, 1), new Frame(1, 1, -1, 1));

    /// <summary>A curve from 0 to 1 that starts and ends gently.</summary>
    public static readonly Curve Ease = new(new Frame(0, 0), new Frame(1, 1));

    /// <summary>A curve from 0 to 1 that starts gently and ends sharply.</summary>
    public static readonly Curve EaseIn = new(new Frame(0, 0, 0, 0), new Frame(1, 1, -MathF.PI, MathF.PI));

    /// <summary>A curve from 0 to 1 that starts sharply and ends gently.</summary>
    public static readonly Curve EaseOut = new(new Frame(0, 0, -MathF.PI, MathF.PI), new Frame(1, 1, 0, 0));

    /// <summary>The key frames, in time order.</summary>
    public ImmutableArray<Frame> Frames;

    /// <summary>Makes a curve through <paramref name="frames"/>, with both ranges 0 to 1.</summary>
    public Curve(ImmutableArray<Frame> frames)
    {
        TimeRange = new Vector2(0, 1);
        ValueRange = new Vector2(0, 1);
        Frames = frames;
    }

    /// <summary>Makes a curve through <paramref name="frames"/>, with both ranges 0 to 1.</summary>
    public Curve(IEnumerable<Frame> frames)
        : this(frames.ToImmutableArray())
    {
    }

    /// <summary>Makes a curve through <paramref name="frames"/>, with both ranges 0 to 1.</summary>
    public Curve(params Frame[] frames)
        : this(frames.ToImmutableArray())
    {
    }

    /// <summary>Makes a curve with no key frames, with both ranges 0 to 1.</summary>
    public Curve()
        : this(ImmutableArray<Frame>.Empty)
    {
    }

    /// <summary>How a key frame's curve leaves it and reaches the next one.</summary>
    public enum HandleMode
    {
        /// <summary>Smooth, with one tangent the user sets; the other side mirrors it.</summary>
        Mirrored,

        /// <summary>Smooth, with separate tangents in and out.</summary>
        Split,

        /// <summary>Smooth and level: both tangents are 0.</summary>
        Flat,

        /// <summary>A straight line to the next key frame.</summary>
        Linear,

        /// <summary>Holds this key frame's value until the next one.</summary>
        Stepped,
    }

    /// <summary>What the key frames' times from 0 to 1 stand for, in your own units.</summary>
    [JsonPropertyName("x")]
    public Vector2 TimeRange { readonly get; set; }

    /// <summary>What the key frames' values from 0 to 1 stand for, in your own units.</summary>
    [JsonPropertyName("y")]
    public Vector2 ValueRange { readonly get; set; }

    /// <summary>How many key frames there are.</summary>
    public readonly int Length => Frames.IsDefaultOrEmpty ? 0 : Frames.Length;

    /// <summary>The key frame at <paramref name="index"/>.</summary>
    public Frame this[int index]
    {
        readonly get => Frames[index];
        set => Frames = Frames.SetItem(index, value);
    }

    /// <summary>A flat curve at <paramref name="value"/>.</summary>
    public static implicit operator Curve(float value)
    {
        var curve = new Curve();
        curve.AddPoint(MathX.Lerp(curve.TimeRange.X, curve.TimeRange.Y, 0.5f), value);
        return curve;
    }

    internal readonly Curve WithValidRanges()
    {
        var curve = this;
        curve.TimeRange = ValidRange(TimeRange);
        curve.ValueRange = ValidRange(ValueRange);
        return curve;

        static Vector2 ValidRange(Vector2 range)
        {
            if (!float.IsFinite(range.X) || !float.IsFinite(range.Y) || !float.IsFinite(range.Y - range.X) || range.X == range.Y)
            {
                return new Vector2(0, 1);
            }

            return range.X > range.Y ? new Vector2(range.Y, range.X) : range;
        }
    }

    internal readonly bool SameAs(Curve other) =>
        TimeRange == other.TimeRange && ValueRange == other.ValueRange && Length == other.Length && (Length == 0 || Frames.SequenceEqual(other.Frames));

    /// <summary>A copy of this curve with <paramref name="frames"/> for its key frames.</summary>
    public readonly Curve WithFrames(IEnumerable<Frame> frames)
    {
        var curve = this;
        curve.Frames = frames.ToImmutableArray();
        return curve;
    }

    /// <summary>
    /// A copy of this curve going the other way in time: a curve that eases from 0 to 1 comes back eased from 1 to 0.
    /// A stepped part still holds the value of the key frame it starts at, so it steps at the other end.
    /// </summary>
    public readonly Curve Reverse()
    {
        if (Frames.IsDefaultOrEmpty)
        {
            return this;
        }

        var reversed = new Frame[Frames.Length];
        for (int i = 0; i < Frames.Length; i++)
        {
            var frame = Frames[i];
            var mode = frame.Mode is HandleMode.Linear or HandleMode.Stepped ? HandleMode.Split : frame.Mode;
            if (i > 0 && Frames[i - 1].Mode is HandleMode.Linear or HandleMode.Stepped)
            {
                mode = Frames[i - 1].Mode;
            }

            var flat = frame.Mode == HandleMode.Flat;
            reversed[Frames.Length - 1 - i] = frame with { Time = 1 - frame.Time, In = flat ? 0 : frame.Out, Out = flat ? 0 : frame.In, Mode = mode };
        }

        return WithFrames(reversed);
    }

    /// <summary>Adds a key frame at <paramref name="x"/>, <paramref name="y"/> and returns where it is in <see cref="Frames"/>.</summary>
    public int AddPoint(float x, float y) => AddPoint(new Frame(x, y));

    /// <summary>Adds <paramref name="keyframe"/> and returns where it is in <see cref="Frames"/>.</summary>
    public int AddPoint(in Frame keyframe)
    {
        if (Frames.IsDefaultOrEmpty)
        {
            Frames = [];
        }

        Frames = Frames.Add(keyframe);
        return Length - 1;
    }

    /// <summary>Removes every key frame within <paramref name="within"/> of <paramref name="time"/>.</summary>
    public void RemoveAtTime(float time, float within) => Frames = Frames.RemoveAll(x => MathF.Abs(x.Time - time) <= within);

    /// <summary>Puts the key frames in time order.</summary>
    public void Sort() => Frames = [.. Frames.Order()];

    /// <summary>Replaces the key frame at the same time as <paramref name="keyframe"/>, or adds it. Returns true if it was added.</summary>
    public bool AddOrReplacePoint(in Frame keyframe)
    {
        if (Frames.IsDefaultOrEmpty)
        {
            Frames = [];
        }

        for (int i = 0; i < Frames.Length; i++)
        {
            if (Frames[i].Time == keyframe.Time)
            {
                Frames = Frames.RemoveAt(i).Insert(i, keyframe);
                return false;
            }
        }

        RemoveAtTime(keyframe.Time, 0.0001f);
        Frames = Frames.Add(keyframe);
        Sort();
        return true;
    }

    /// <summary>The curve's value at <paramref name="time"/>, both in your own units from <see cref="TimeRange"/> and <see cref="ValueRange"/>.</summary>
    public readonly float Evaluate(float time)
    {
        time = MathX.LerpInverse(time, TimeRange.X, TimeRange.Y, false);
        return MathX.Remap(EvaluateDelta(time), 0, 1, ValueRange.X, ValueRange.Y, false);
    }

    /// <summary>The curve's value at <paramref name="time"/>, both from 0 to 1 regardless of the curve's ranges.</summary>
    public readonly float EvaluateDelta(float time)
    {
        if (Length == 0)
        {
            return 0;
        }

        if (Length == 1)
        {
            return Frames[0].Value;
        }

        int index = Frames.BinarySearch(new Frame { Time = time }, null);
        if (index >= 0)
        {
            return Frames[index].Value;
        }

        index = ~index;
        if (index == 0)
        {
            return Frames[0].Value;
        }

        return index >= Frames.Length ? Frames[^1].Value : Interpolate(Frames[index - 1], Frames[index], time);
    }

    /// <summary>Repairs a broken curve: zero ranges become 0 to 1, and an empty curve gets a key frame in the middle.</summary>
    public void Fix()
    {
        if (ValueRange == Vector2.Zero)
        {
            ValueRange = new Vector2(0, 1);
        }

        if (TimeRange == Vector2.Zero)
        {
            TimeRange = new Vector2(0, 1);
        }

        if (Length == 0)
        {
            AddPoint(0.5f, 0.5f);
        }
    }

    /// <summary>Changes <see cref="ValueRange"/>. Pass <paramref name="retainValues"/> to keep the key frames at the same values in your own units.</summary>
    public void UpdateValueRange(Vector2 newRange, bool retainValues)
    {
        if (retainValues)
        {
            var oldRange = ValueRange;
            Frames = [.. Frames.Select(x => x.WithValue(RemapDelta(x.Value, oldRange, newRange)))];
        }

        ValueRange = newRange;
    }

    /// <summary>Changes <see cref="TimeRange"/>. Pass <paramref name="retainTimes"/> to keep the key frames at the same times in your own units.</summary>
    public void UpdateTimeRange(Vector2 newRange, bool retainTimes)
    {
        if (retainTimes)
        {
            var oldRange = TimeRange;
            Frames = [.. Frames.Select(x => x.WithTime(RemapDelta(x.Time, oldRange, newRange)))];
        }

        TimeRange = newRange;
    }

    private static float RemapDelta(float delta, Vector2 oldRange, Vector2 range)
    {
        var value = MathX.Remap(delta, 0, 1, oldRange.X, oldRange.Y, false);
        return MathX.Remap(value, range.X, range.Y, 0, 1, false);
    }

    private static float Interpolate(in Frame a, in Frame b, float time)
    {
        switch (a.Mode)
        {
            case HandleMode.Stepped:
                return a.Value;
            case HandleMode.Linear:
                return a.Value + ((b.Value - a.Value) * (time - a.Time) / (b.Time - a.Time));
        }

        float t = (time - a.Time) / (b.Time - a.Time);
        float incoming = b.Mode == HandleMode.Flat ? 0 : -b.In;
        float outgoing = a.Mode == HandleMode.Flat ? 0 : a.Out;
        float dx = b.Time - a.Time;
        float dy = b.Value - a.Value;
        return a.Value + (t * ((t * ((t * (((incoming + outgoing) * dx) - (2 * dy))) + ((-incoming - (2 * outgoing)) * dx) + (3 * dy))) + (outgoing * dx)));
    }

    /// <summary>A key frame on a <see cref="Curve"/>. Its time and value go from 0 to 1.</summary>
    public struct Frame : IComparable<Frame>
    {
        /// <summary>Makes a smooth key frame at <paramref name="timedelta"/>, <paramref name="valuedelta"/> with level tangents.</summary>
        public Frame(float timedelta, float valuedelta)
        {
            Time = timedelta;
            Value = valuedelta;
        }

        /// <summary>Makes a smooth key frame at <paramref name="timedelta"/>, <paramref name="valuedelta"/> with the slopes <paramref name="inTangent"/> and <paramref name="outTangent"/>.</summary>
        public Frame(float timedelta, float valuedelta, float inTangent, float outTangent)
        {
            Time = timedelta;
            Value = valuedelta;
            In = inTangent;
            Out = outTangent;
        }

        /// <summary>Where the key frame is in time, from 0 to 1.</summary>
        [JsonPropertyName("x")]
        public float Time { readonly get; set; }

        /// <summary>The key frame's value, from 0 to 1.</summary>
        [JsonPropertyName("y")]
        public float Value { readonly get; set; }

        /// <summary>The slope coming into the key frame, negated: 1 means the curve arrives going down one unit per unit of time.</summary>
        [JsonPropertyName("in")]
        public float In { readonly get; set; }

        /// <summary>The slope leaving the key frame: 1 means the curve leaves going up one unit per unit of time.</summary>
        [JsonPropertyName("out")]
        public float Out { readonly get; set; }

        /// <summary>How the curve leaves this key frame and reaches the next.</summary>
        [JsonPropertyName("mode")]
        public HandleMode Mode { readonly get; set; }

        /// <summary>A copy of this key frame at <paramref name="time"/>.</summary>
        public readonly Frame WithTime(float time) => this with { Time = time };

        /// <summary>A copy of this key frame with <paramref name="value"/>.</summary>
        public readonly Frame WithValue(float value) => this with { Value = value };

        /// <summary>Orders key frames by time.</summary>
        public readonly int CompareTo(Frame other) => Time.CompareTo(other.Time);
    }

    private sealed class JsonConverter : JsonConverter<Curve>
    {
        public override Curve Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.Number:
                    return reader.GetSingle();
                case JsonTokenType.StartArray:
                    {
                        var curve = new Curve().WithFrames(JsonSerializer.Deserialize<List<Frame>>(ref reader, options) ?? []);
                        curve.Fix();
                        return curve;
                    }

                case JsonTokenType.StartObject:
                    {
                        var curve = new Curve();
                        reader.Read();
                        while (reader.TokenType != JsonTokenType.EndObject)
                        {
                            if (reader.TokenType != JsonTokenType.PropertyName)
                            {
                                reader.Read();
                                continue;
                            }

                            var name = reader.GetString();
                            reader.Read();
                            switch (name)
                            {
                                case "rangex":
                                    curve.TimeRange = ReadVector(ref reader);
                                    break;
                                case "rangey":
                                    curve.ValueRange = ReadVector(ref reader);
                                    break;
                                case "frames" when reader.TokenType == JsonTokenType.StartArray:
                                    curve = curve.WithFrames(JsonSerializer.Deserialize<List<Frame>>(ref reader, options) ?? []);
                                    break;
                                default:
                                    reader.Skip();
                                    break;
                            }

                            reader.Read();
                        }

                        curve.Fix();
                        return curve;
                    }
            }

            throw new JsonException($"A curve can't be read from {reader.TokenType}.");
        }

        public override void Write(Utf8JsonWriter writer, Curve value, JsonSerializerOptions options)
        {
            var unit = new Vector2(0, 1);
            if (value.TimeRange == unit && value.ValueRange == unit)
            {
                JsonSerializer.Serialize(writer, value.Frames.IsDefault ? [] : value.Frames, options);
                return;
            }

            writer.WriteStartObject();
            if (value.TimeRange != unit)
            {
                writer.WriteString("rangex", WriteVector(value.TimeRange));
            }

            if (value.ValueRange != unit)
            {
                writer.WriteString("rangey", WriteVector(value.ValueRange));
            }

            if (!value.Frames.IsDefaultOrEmpty)
            {
                writer.WritePropertyName("frames");
                JsonSerializer.Serialize(writer, value.Frames, options);
            }

            writer.WriteEndObject();
        }

        private static string WriteVector(Vector2 value) => string.Create(CultureInfo.InvariantCulture, $"{value.X:G9},{value.Y:G9}");

        private static Vector2 ReadVector(ref Utf8JsonReader reader)
        {
            var parts = reader.TokenType == JsonTokenType.String ? reader.GetString()!.Split(',') : [];
            if (parts.Length == 2
                && Translation.TryParseFloat(parts[0], out var x)
                && Translation.TryParseFloat(parts[1], out var y))
            {
                return new Vector2(x, y);
            }

            throw new JsonException("A curve range must be written as \"x,y\".");
        }
    }
}

/// <summary>Two curves, for a value that can land anywhere between them, like a random size that changes over time.</summary>
public struct CurveRange
{
    /// <summary>Makes a range between two curves with no key frames.</summary>
    public CurveRange()
    {
        A = new Curve();
        B = new Curve();
    }

    /// <summary>Makes a range between <paramref name="a"/> and <paramref name="b"/>.</summary>
    public CurveRange(in Curve a, in Curve b)
    {
        A = a;
        B = b;
    }

    /// <summary>One side of the range.</summary>
    [JsonPropertyName("a")]
    public Curve A { readonly get; set; }

    /// <summary>The other side of the range.</summary>
    [JsonPropertyName("b")]
    public Curve B { readonly get; set; }

    /// <summary>The value at time <paramref name="x"/>, in your own units, <paramref name="y"/> of the way from <see cref="A"/> to <see cref="B"/>.</summary>
    public readonly float Evaluate(float x, float y) => MathX.Lerp(A.Evaluate(x), B.Evaluate(x), y);

    /// <summary>The value at time <paramref name="x"/>, both from 0 to 1, <paramref name="y"/> of the way from <see cref="A"/> to <see cref="B"/>.</summary>
    public readonly float EvaluateDelta(float x, float y) => MathX.Lerp(A.EvaluateDelta(x), B.EvaluateDelta(x), y);
}
