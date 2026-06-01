using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DnDMapHelper.Helpers;

public static class HandDrawnMarkerHelper
{
    public static Path CreateTargetCross(Point center, double size, bool isSelected, Guid targetId)
    {
        var hash = targetId.GetHashCode();
        var r1 = PseudoRandom(hash, 1);
        var r2 = PseudoRandom(hash, 2);
        var r3 = PseudoRandom(hash, 3);
        var r4 = PseudoRandom(hash, 4);

        var half = size * 0.5;
        var wobble = size * 0.1;

        static double Off(double value, double amount, double t) => value + (t - 0.5) * 2 * amount;

        var tl = new Point(Off(center.X - half, wobble, r1), Off(center.Y - half, wobble, r2));
        var br = new Point(Off(center.X + half, wobble, r3), Off(center.Y + half, wobble, r4));
        var tr = new Point(Off(center.X + half, wobble, r2), Off(center.Y - half, wobble, r1));
        var bl = new Point(Off(center.X - half, wobble, r4), Off(center.Y + half, wobble, r3));

        var mid1 = new Point(
            Off((tl.X + br.X) / 2, wobble * 0.6, r3),
            Off((tl.Y + br.Y) / 2, wobble * 0.6, r1));
        var mid2 = new Point(
            Off((tr.X + bl.X) / 2, wobble * 0.6, r2),
            Off((tr.Y + bl.Y) / 2, wobble * 0.6, r4));

        var geometry = new PathGeometry();

        var slash1 = new PathFigure { StartPoint = tl, IsFilled = false };
        slash1.Segments.Add(new QuadraticBezierSegment(mid1, br, true));
        geometry.Figures.Add(slash1);

        var slash2 = new PathFigure { StartPoint = tr, IsFilled = false };
        slash2.Segments.Add(new QuadraticBezierSegment(mid2, bl, true));
        geometry.Figures.Add(slash2);

        var strokeColor = isSelected
            ? Color.FromRgb(190, 45, 15)
            : Color.FromRgb(110, 35, 12);

        return new Path
        {
            Data = geometry,
            Stroke = new SolidColorBrush(strokeColor),
            StrokeThickness = isSelected ? 3.2 : 2.6,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            SnapsToDevicePixels = false,
            RenderTransform = new RotateTransform(
                Off(0, 7, r1),
                center.X,
                center.Y)
        };
    }

    private static double PseudoRandom(int seed, int channel) =>
        ((seed * 1103515245 + channel * 12345) & 0x7FFFFFFF) / (double)0x7FFFFFFF;
}
