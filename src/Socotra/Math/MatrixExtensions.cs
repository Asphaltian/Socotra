namespace Socotra;

internal static class MatrixExtensions
{
    extension(Matrix4x4 matrix)
    {
        public Matrix4x4 Inverted
        {
            get
            {
                Matrix4x4.Invert(matrix, out var inverse);
                return inverse;
            }
        }

        public Vector2 Transform(Vector2 point) => Vector2.Transform(point, matrix);

        public Rect Transform(Rect rect)
        {
            var a = Vector2.Transform(rect.TopLeft, matrix);
            var b = Vector2.Transform(rect.TopRight, matrix);
            var c = Vector2.Transform(rect.BottomLeft, matrix);
            var d = Vector2.Transform(rect.BottomRight, matrix);
            var min = Vector2.Min(Vector2.Min(a, b), Vector2.Min(c, d));
            var max = Vector2.Max(Vector2.Max(a, b), Vector2.Max(c, d));
            return new Rect(min, max - min);
        }
    }

    extension(ref Matrix4x4 matrix)
    {
        public void TranslateLocal2D(float x, float y)
        {
            matrix.M41 = float.MultiplyAddEstimate(y, matrix.M21, x * matrix.M11) + matrix.M41;
            matrix.M42 = float.MultiplyAddEstimate(y, matrix.M22, x * matrix.M12) + matrix.M42;
        }

        public void RotateLocal2D(float degrees)
        {
            var (sin, cos) = MathF.SinCos(float.DegreesToRadians(degrees));
            float m11 = matrix.M11;
            float m12 = matrix.M12;
            matrix.M11 = float.MultiplyAddEstimate(sin, matrix.M21, cos * m11);
            matrix.M12 = float.MultiplyAddEstimate(sin, matrix.M22, cos * m12);
            matrix.M21 = float.MultiplyAddEstimate(cos, matrix.M21, -sin * m11);
            matrix.M22 = float.MultiplyAddEstimate(cos, matrix.M22, -sin * m12);
        }

        public void ScaleLocal2D(float x, float y)
        {
            matrix.M11 *= x;
            matrix.M12 *= x;
            matrix.M21 *= y;
            matrix.M22 *= y;
        }
    }
}
