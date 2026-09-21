namespace Socotra;

public partial class CurveEditor
{
    internal static Vector2[] BuildRangeArea(CurveRange range, float min, float max, int samples)
    {
        var times = new SortedSet<float>();
        samples = Math.Clamp(samples, 2, 2048);
        for (int i = 0; i <= samples; i++)
        {
            times.Add(min + ((max - min) * i / samples));
        }

        foreach (var curve in (Curve[])[range.A, range.B])
        {
            for (int i = 0; i < curve.Length; i++)
            {
                float t = DisplayTime(curve, curve.Frames[i].Time);
                if (t < min || t > max)
                {
                    continue;
                }

                times.Add(t);
                if (i > 0 && curve.Frames[i - 1].Mode == Curve.HandleMode.Stepped)
                {
                    times.Add(Math.Max(min, t - Math.Max(MathF.Abs(t) * 0.000001f, 0.000001f)));
                }
            }
        }

        var xs = times.ToArray();
        var result = new Vector2[xs.Length * 2];
        for (int i = 0; i < xs.Length; i++)
        {
            float time = range.A.TimeRange.X + (xs[i] * (range.A.TimeRange.Y - range.A.TimeRange.X));
            float a = (range.A.Evaluate(time) - range.A.ValueRange.X) / (range.A.ValueRange.Y - range.A.ValueRange.X);
            float b = (range.B.Evaluate(time) - range.A.ValueRange.X) / (range.A.ValueRange.Y - range.A.ValueRange.X);
            result[i] = new Vector2(xs[i], a);
            result[^(i + 1)] = new Vector2(xs[i], b);
        }

        return result;

        float DisplayTime(Curve curve, float x) => Remap(curve, range.A, new Vector2(x, 0)).X;
    }

    private sealed partial class CurveCanvas
    {
        private Curve[]? _areaCurves;
        private Vector2 _areaMin;
        private Vector2 _areaMax;
        private Rect _areaPlot;
        private Vector2[] _area = [];

        public override void OnDraw(Painter painter)
        {
            base.OnDraw(painter);
            using var scope = painter.Scope();
            painter.Clip(Plot);
            DrawNormalRange(painter);
            if (_editor.IsRange)
            {
                DrawArea(painter);
            }

            foreach (int c in Enumerable.Range(0, _editor._curves.Length).OrderBy(c => c == _editor.ActiveCurve))
            {
                bool active = c == _editor.ActiveCurve;
                var color = CurveColor(c);
                DrawCurve(painter, _editor._curves[c], active ? color : color.WithAlpha(color.A * 0.5f), active ? 1.5f : 1);
            }

            if (!HasTangents)
            {
                return;
            }

            var tangentColor = CurveColor(_editor.ActiveCurve);
            painter.Stroke = Stroke.Solid(tangentColor.WithAlpha(0.5f), 1);
            painter.Line(KeyPosition(PrimaryKey), TangentPosition(true));
            painter.Line(KeyPosition(PrimaryKey), TangentPosition(false));
        }

        private Color CurveColor(int index) => _editor.ChannelColor(index);

        private void DrawNormalRange(Painter painter)
        {
            using var scope = painter.Scope();
            float top = CanvasToScreen(Vector2.One).Y;
            float bottom = CanvasToScreen(Vector2.Zero).Y;
            painter.Stroke = Stroke.None;
            painter.Fill = AxisColor.WithAlpha(0.045f);
            if (top > Plot.Top)
            {
                painter.Rect(new Rect(Plot.Left, Plot.Top, Plot.Width, Math.Min(top, Plot.Bottom) - Plot.Top));
            }

            if (bottom < Plot.Bottom)
            {
                painter.Rect(new Rect(Plot.Left, Math.Max(bottom, Plot.Top), Plot.Width, Plot.Bottom - Math.Max(bottom, Plot.Top)));
            }
        }

        private void DrawCurve(Painter painter, Curve curve, Color color, float width)
        {
            if (curve.Length == 0)
            {
                return;
            }

            painter.Stroke = Stroke.Solid(color, width);
            var first = curve.Frames[0];
            var last = curve.Frames[^1];
            painter.Line(CanvasToScreen(new Vector2(ViewMin.X, _editor.ToDisplay(curve, new Vector2(first.Time, first.Value)).Y)), Point(first.Time, first.Value));
            for (int i = 0; i < curve.Length - 1; i++)
            {
                var a = curve.Frames[i];
                var b = curve.Frames[i + 1];
                var start = Point(a.Time, a.Value);
                var end = Point(b.Time, b.Value);
                if (a.Mode == Curve.HandleMode.Stepped)
                {
                    var corner = Point(b.Time, a.Value);
                    painter.Line(start, corner);
                    painter.Line(corner, end);
                }
                else if (a.Mode == Curve.HandleMode.Linear)
                {
                    painter.Line(start, end);
                }
                else
                {
                    float dx = (b.Time - a.Time) / 3;
                    float outgoing = a.Mode == Curve.HandleMode.Flat ? 0 : a.Out;
                    float incoming = b.Mode == Curve.HandleMode.Flat ? 0 : b.In;
                    painter.Bezier(start, Point(a.Time + dx, a.Value + (outgoing * dx)), Point(b.Time - dx, b.Value + (incoming * dx)), end);
                }
            }

            painter.Line(Point(last.Time, last.Value), CanvasToScreen(new Vector2(ViewMax.X, _editor.ToDisplay(curve, new Vector2(last.Time, last.Value)).Y)));

            Vector2 Point(float x, float y) => CanvasToScreen(_editor.ToDisplay(curve, new Vector2(x, y)));
        }

        private void DrawArea(Painter painter)
        {
            if (_areaCurves != _editor._curves || _areaMin != ViewMin || _areaMax != ViewMax || _areaPlot != Plot)
            {
                _areaCurves = _editor._curves;
                _areaMin = ViewMin;
                _areaMax = ViewMax;
                _areaPlot = Plot;
                _area = [.. BuildRangeArea(_editor.RangeValue, ViewMin.X, ViewMax.X, Math.Clamp((int)(Plot.Width / 3), 32, 2048)).Select(CanvasToScreen)];
            }

            painter.Stroke = Stroke.None;
            painter.Fill = CurveColor(0).WithAlpha(0.12f);
            painter.Polygon(_area);
        }
    }
}
