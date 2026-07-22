using System.Windows;
using System.Windows.Media;

namespace DnDMapHelper.Helpers;

public static class RegionGeometryHelper
{
    private const int MinimumOutlinePoints = 3;
    private const double MinimumBoundsSize = 12;

    /// <summary>Предпросмотр контура при рисовании: прореживание и сглаживание без замыкания.</summary>
    public static List<Point> PrepareDraftOutline(IReadOnlyList<Point> rawPoints)
    {
        if (rawPoints.Count < 2)
            return rawPoints.ToList();

        return PathGeometryHelper.PrepareOpenPolyline(rawPoints, captureMinDistance: 8, simplifyTolerance: 22);
    }

    /// <summary>Собирает контур из сырых точек мыши: прореживание, упрощение, замыкание.</summary>
    public static List<Point>? PrepareOutline(IReadOnlyList<Point> rawPoints)
    {
        if (rawPoints.Count < 2)
            return null;

        var simplified = PathGeometryHelper.PrepareOpenPolyline(rawPoints);
        if (simplified.Count < MinimumOutlinePoints)
            simplified = simplified.Count >= MinimumOutlinePoints
                ? simplified
                : EnsureMinimumPoints(simplified);

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

        var bounds = GetBounds(outline);
        if (!bounds.Contains(testPoint))
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
}
