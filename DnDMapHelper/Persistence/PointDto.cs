using System.Windows;

namespace DnDMapHelper.Persistence;

public sealed class PointDto
{
    public double X { get; set; }
    public double Y { get; set; }

    public static PointDto FromPoint(Point point) => new() { X = point.X, Y = point.Y };

    public Point ToPoint() => new(X, Y);
}
