namespace Socotra;

internal static class VectorExtensions
{
    extension(Vector2 vector)
    {
        public Vector2 Normal => vector.LengthSquared() <= 1e-8f ? Vector2.Zero : Vector2.Normalize(vector);

        public Vector2 Perpendicular => new(-vector.Y, vector.X);
    }
}
