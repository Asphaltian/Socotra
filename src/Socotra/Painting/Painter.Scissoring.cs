namespace Socotra;

public readonly ref partial struct Painter
{
    internal struct Scissoring : IEquatable<Scissoring>
    {
        public const int MaxClips = 4;

        public struct Clip
        {
            public Rect Rect;
            public BorderRadii Radii;
            public Matrix4x4 Transform;

            internal readonly Clip ForShader()
            {
                var m = Transform;
                if (m.M33 == 0 || !float.IsFinite(m.M33))
                {
                    return new Clip { Transform = Matrix4x4.Identity };
                }

                var z = new Vector4(m.M31, m.M32, 0, m.M34);
                var x = new Vector4(m.M11, m.M12, 0, m.M14) - z * (m.M13 / m.M33);
                var y = new Vector4(m.M21, m.M22, 0, m.M24) - z * (m.M23 / m.M33);
                var w = new Vector4(m.M41, m.M42, 0, m.M44) - z * (m.M43 / m.M33);
                if (!IsFinite(x) || !IsFinite(y) || !IsFinite(w))
                {
                    return new Clip { Transform = Matrix4x4.Identity };
                }

                return new Clip
                {
                    Rect = Rect,
                    Radii = Radii,
                    Transform = new Matrix4x4(
                        x.X, x.Y, 0, x.W,
                        y.X, y.Y, 0, y.W,
                        0, 0, m.M33, 0,
                        w.X, w.Y, 0, w.W)
                };
            }
        }

        [System.Runtime.CompilerServices.InlineArray(MaxClips)]
        public struct ClipList
        {
            Clip _element;
        }

        public ClipList Clips;
        public int Count;

        public bool Invert;

        public readonly bool IsEmpty => Count == 0;

        public static Scissoring Single(in Rect rect, in BorderRadii radii, in Matrix4x4 matrix, bool invert = false)
        {
            var s = new Scissoring { Invert = invert };
            s.Push(rect, radii, matrix);
            return s;
        }

        public void Push(in Rect rect, in BorderRadii radii, in Matrix4x4 matrix)
        {
            if (Count > 0)
            {
                ref var top = ref Clips[Count - 1];

                var mergeable = top.Radii.IsZero && radii.IsZero && top.Transform == matrix;
                if (mergeable || Count == MaxClips)
                {
                    top.Rect = Socotra.Rect.Intersect(top.Rect, rect);
                    return;
                }
            }

            Clips[Count++] = new Clip { Rect = rect, Radii = radii, Transform = matrix };
        }

        public readonly bool Equals(in Scissoring other)
        {
            if (Count != other.Count || Invert != other.Invert)
            {
                return false;
            }

            for (int i = 0; i < Count; i++)
            {
                ref readonly var a = ref Clips[i];
                ref readonly var b = ref other.Clips[i];
                if (a.Rect != b.Rect || a.Radii != b.Radii || a.Transform != b.Transform)
                {
                    return false;
                }
            }

            return true;
        }

        readonly bool IEquatable<Scissoring>.Equals(Scissoring other) => Equals(in other);

        public readonly override bool Equals(object? obj) => obj is Scissoring other && Equals(in other);

        public readonly override int GetHashCode()
        {
            var hash = HashCode.Combine(Count, Invert);
            for (int i = 0; i < Count; i++)
            {
                ref readonly var c = ref Clips[i];
                hash = HashCode.Combine(hash, c.Rect, c.Radii.TopLeft, c.Radii.TopRight, c.Radii.BottomLeft, c.Radii.BottomRight, c.Transform);
            }
            return hash;
        }
    }
}
