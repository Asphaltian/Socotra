namespace Socotra;

public readonly ref partial struct Painter
{
    /// <summary>
    /// Moves, turns and scales everything you draw from now on. Usually you'll call <see cref="Translate(float, float)"/>,
    /// <see cref="Rotate"/> and <see cref="Scale(float)"/> instead. Throws if the matrix isn't a flat 2D transform.
    /// </summary>
    public Matrix4x4 Transform
    {
        get => ActiveContext.State.Transform;
        set
        {
            if (!float.IsFinite(value.M11) || !float.IsFinite(value.M12) || !float.IsFinite(value.M21)
                || !float.IsFinite(value.M22) || !float.IsFinite(value.M41) || !float.IsFinite(value.M42)
                || value.M13 != 0 || value.M14 != 0 || value.M23 != 0 || value.M24 != 0
                || value.M31 != 0 || value.M32 != 0 || value.M33 != 1 || value.M34 != 0 || value.M43 != 0 || value.M44 != 1)
            {
                throw new ArgumentException("Expected a finite 2D affine matrix.", nameof(value));
            }

            ActiveContext.State.Transform = value;
        }
    }

    /// <summary>
    /// Shifts everything you draw from now on by <paramref name="offset"/>.
    /// </summary>
    public void Translate(Vector2 offset) => Translate(offset.X, offset.Y);

    /// <summary>
    /// Shifts everything you draw from now on by <paramref name="x"/> and <paramref name="y"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// using (painter.Scope())
    /// {
    ///     // spin a square around its middle at (50, 50)
    ///     painter.Translate(50, 50);
    ///     painter.Rotate(angle);
    ///     painter.Rect(new Rect(-10, -10, 20, 20));
    /// }
    /// </code>
    /// </example>
    public void Translate(float x, float y) => Transform = Matrix4x4.CreateTranslation(new Vector3(x, y, 0)) * Transform;

    /// <summary>
    /// Turns everything you draw from now on clockwise by <paramref name="degrees"/>, around the current 0, 0.
    /// Call <see cref="Translate(float, float)"/> first to turn around another point.
    /// </summary>
    public void Rotate(float degrees) => Transform = Matrix4x4.CreateRotationZ(float.DegreesToRadians(degrees)) * Transform;

    /// <summary>
    /// Makes everything you draw from now on bigger or smaller, strokes and text included. 2 is twice the size.
    /// </summary>
    public void Scale(float scale) => Scale(scale, scale);

    /// <summary>
    /// Stretches everything you draw from now on by different amounts across and down.
    /// </summary>
    public void Scale(Vector2 scale) => Scale(scale.X, scale.Y);

    /// <summary>
    /// Stretches everything you draw from now on by <paramref name="x"/> across and <paramref name="y"/> down.
    /// </summary>
    public void Scale(float x, float y) => Transform = Matrix4x4.CreateScale(new Vector3(x, y, 1)) * Transform;
}
