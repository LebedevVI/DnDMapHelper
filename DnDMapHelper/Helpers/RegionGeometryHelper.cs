using System.Windows;
using System.Windows.Media;

namespace DnDMapHelper.Helpers;

public static class RegionGeometryHelper
{
    private const double CaptureMinDistance = 10;
    private const double SimplifyTolerance = 16;
    private const int MinimumOutlinePoints = 3;
    private const double MinimumBoundsSize = 12;

    /// <summary>Собирает контур из сырых точек мыши: прореживание, упрощение, замыкание.</summary>
    public static List<Point>? PrepareOutline(IReadOnlyList<Point> rawPoints)
    {
        if (rawPoints.Count < 2)
            return null;

        var thinned = ThinByDistance(rawPoints, CaptureMinDistance);
        if (thinned.Count < 2)
            thinned = [rawPoints[0], rawPoints[^1]];

        var simplified = SimplifyPolyline(thinned, SimplifyTolerance);
        if (simplified.Count < MinimumOutlinePoints)
            simplified = thinned.Count >= MinimumOutlinePoints
                ? thinned
                : EnsureMinimumPoints(thinned);

        if (simplified.Count < MinimumOutlinePoints)
            return null;

        var bounds = GetBounds(simplified);
        if (bounds.Width < MinimumBoundsSize || bounds.Height < MinimumBoundsSize)
            return null;

        return simplified;
    }

    public static PathGeometry CreateClosedSmoothPath(IReadOnlyList<Point> points)
    {
        var geometry = new PathGeometry();
        if (points.Count < 3)
            return geometry;

        var count = points.Count;
        var figure = new PathFigure
        {
            StartPoint = points[0],
            IsClosed = true,
            IsFilled = true
        };

        for (var i = 0; i < count; i++)
        {
            var p0 = points[(i - 1 + count) % count];
            var p1 = points[i];
            var p2 = points[(i + 1) % count];
            var p3 = points[(i + 2) % count];

            var cp1 = new Point(p1.X + (p2.X - p0.X) / 6, p1.Y + (p2.Y - p0.Y) / 6);
            var cp2 = new Point(p2.X - (p3.X - p1.X) / 6, p2.Y - (p3.Y - p1.Y) / 6);
            figure.Segments.Add(new BezierSegment(cp1, cp2, p2, true));
        }

        geometry.Figures.Add(figure);
        return geometry;
    }

    public static bool ContainsPoint(IReadOnlyList<Point> outline, Point testPoint)
    {
        if (outline.Count < 3)
            return false;

        var geometry = CreateClosedSmoothPath(outline);
        return geometry.FillContains(testPoint);
    }

    public static Rect GetBounds(IReadOnlyList<Point> points)
    {
        if (points.Count == 0)
            return Rect.Empty;

        var minX = points[0].X;
        var minY = points[0].Y;
        var maxX = minX;
        var maxY = minY;

        for (var i = 1; i < points.Count; i++)
        {
            minX = Math.Min(minX, points[i].X);
            minY = Math.Min(minY, points[i].Y);
            maxX = Math.Max(maxX, points[i].X);
            maxY = Math.Max(maxY, points[i].Y);
        }

        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }

    private static List<Point> ThinByDistance(IReadOnlyList<Point> points, double minDistance)
    {
        var result = new List<Point> { points[0] };
        var minDistSq = minDistance * minDistance;

        for (var i = 1; i < points.Count; i++)
        {
            var last = result[^1];
            var dx = points[i].X - last.X;
            var dy = points[i].Y - last.Y;
            if (dx * dx + dy * dy >= minDistSq)
                result.Add(points[i]);
        }

        var end = points[^1];
        if (result.Count == 1 || Distance(result[^1], end) > 1)
            result.Add(end);

        return result;
    }

    private static List<Point> EnsureMinimumPoints(List<Point> points)
    {
        if (points.Count >= MinimumOutlinePoints)
            return points;

        if (points.Count == 2)
        {
            var mid = new Point(
                (points[0].X + points[1].X) / 2,
                (points[0].Y + points[1].Y) / 2);
            return [points[0], mid, points[1]];
        }

        return points;
    }

    private static List<Point> SimplifyPolyline(IReadOnlyList<Point> points, double tolerance)
    {
        if (points.Count <= 2)
            return points.ToList();

        var keep = new bool[points.Count];
        keep[0] = true;
        keep[^1] = true;
        SimplifyRange(points, 0, points.Count - 1, tolerance, keep);

        var result = new List<Point>();
        for (var i = 0; i < points.Count; i++)
        {
            if (keep[i])
                result.Add(points[i]);
        }

        return result;
    }

    private static void SimplifyRange(IReadOnlyList<Point> points, int start, int end, double tolerance, bool[] keep)
    {
        if (end <= start + 1)
            return;

        var lineStart = points[start];
        var lineEnd = points[end];
        var maxDistance = 0.0;
        var index = start;

        for (var i = start + 1; i < end; i++)
        {
            var distance = PerpendicularDistance(points[i], lineStart, lineEnd);
            if (distance <= maxDistance)
                continue;

            maxDistance = distance;
            index = i;
        }

        if (maxDistance <= tolerance)
            return;

        keep[index] = true;
        SimplifyRange(points, start, index, tolerance, keep);
        SimplifyRange(points, index, end, tolerance, keep);
    }

    private static double PerpendicularDistance(Point point, Point lineStart, Point lineEnd)
    {
        var dx = lineEnd.X - lineStart.X;
        var dy = lineEnd.Y - lineStart.Y;
        if (Math.Abs(dx) < 0.001 && Math.Abs(dy) < 0.001)
            return Distance(point, lineStart);

        var t = ((point.X - lineStart.X) * dx + (point.Y - lineStart.Y) * dy) / (dx * dx + dy * dy);
        t = Math.Clamp(t, 0, 1);
        var projX = lineStart.X + t * dx;
        var projY = lineStart.Y + t * dy;
        return Distance(point, new Point(projX, projY));
    }

    private static double Distance(Point a, Point b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
