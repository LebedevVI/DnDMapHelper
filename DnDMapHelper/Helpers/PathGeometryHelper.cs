using System.Windows;
using System.Windows.Media;

namespace DnDMapHelper.Helpers;

public static class PathGeometryHelper
{
    public static double GetTotalLength(IList<Point> points)
    {
        if (points.Count < 2)
            return 0;

        double length = 0;
        for (var i = 1; i < points.Count; i++)
            length += Distance(points[i - 1], points[i]);
        return length;
    }

    public static Point GetPointAtDistance(IList<Point> points, double distance)
    {
        if (points.Count == 0)
            return default;
        if (points.Count == 1)
            return points[0];

        var remaining = Math.Max(0, distance);
        for (var i = 1; i < points.Count; i++)
        {
            var segment = Distance(points[i - 1], points[i]);
            if (remaining <= segment || i == points.Count - 1)
            {
                var t = segment <= 0 ? 1 : remaining / segment;
                return new Point(
                    points[i - 1].X + (points[i].X - points[i - 1].X) * t,
                    points[i - 1].Y + (points[i].Y - points[i - 1].Y) * t);
            }

            remaining -= segment;
        }

        return points[^1];
    }

    public static PathGeometry CreateSmoothPath(IReadOnlyList<Point> points)
    {
        var geometry = new PathGeometry();
        if (points.Count == 0)
            return geometry;

        var figure = new PathFigure { StartPoint = points[0], IsClosed = false, IsFilled = false };
        if (points.Count == 1)
        {
            geometry.Figures.Add(figure);
            return geometry;
        }

        if (points.Count == 2)
        {
            figure.Segments.Add(new LineSegment(points[1], true));
            geometry.Figures.Add(figure);
            return geometry;
        }

        for (var i = 0; i < points.Count - 1; i++)
        {
            var p0 = i == 0 ? points[0] : points[i - 1];
            var p1 = points[i];
            var p2 = points[i + 1];
            var p3 = i + 2 < points.Count ? points[i + 2] : p2;

            var cp1 = new Point(p1.X + (p2.X - p0.X) / 6, p1.Y + (p2.Y - p0.Y) / 6);
            var cp2 = new Point(p2.X - (p3.X - p1.X) / 6, p2.Y - (p3.Y - p1.Y) / 6);
            figure.Segments.Add(new BezierSegment(cp1, cp2, p2, true));
        }

        geometry.Figures.Add(figure);
        return geometry;
    }

    /// <summary>Прогресс 0..1 с плавным разгоном и торможением.</summary>
    public static double EaseInOutCubic(double t)
    {
        t = Math.Clamp(t, 0, 1);
        return t < 0.5
            ? 4 * t * t * t
            : 1 - Math.Pow(-2 * t + 2, 3) / 2;
    }

    public static IList<Point> FlattenSmoothPath(IReadOnlyList<Point> points, double tolerance = 0.75)
    {
        if (points.Count < 2)
            return points.Count == 1 ? [points[0]] : [];

        var geometry = CreateSmoothPath(points);
        var flattened = geometry.GetFlattenedPathGeometry(tolerance, ToleranceType.Absolute);
        var result = new List<Point>();
        foreach (var figure in flattened.Figures)
        {
            result.Add(figure.StartPoint);
            foreach (var segment in figure.Segments)
            {
                if (segment is LineSegment line)
                    result.Add(line.Point);
                else if (segment is PolyLineSegment poly)
                    result.AddRange(poly.Points);
            }
        }

        return result.Count >= 2 ? result : points.ToList();
    }

    public static double GetSmoothPathLength(IReadOnlyList<Point> points)
    {
        var flat = FlattenSmoothPath(points);
        return GetTotalLength(flat);
    }

    public static Point GetPointOnSmoothPath(IReadOnlyList<Point> points, double easedProgress)
    {
        var flat = FlattenSmoothPath(points);
        if (flat.Count == 0)
            return default;
        if (flat.Count == 1)
            return flat[0];

        var total = GetTotalLength(flat);
        var distance = total * Math.Clamp(easedProgress, 0, 1);
        return GetPointAtDistance(flat, distance);
    }

    private static double Distance(Point a, Point b) =>
        Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
}
