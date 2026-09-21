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
}
