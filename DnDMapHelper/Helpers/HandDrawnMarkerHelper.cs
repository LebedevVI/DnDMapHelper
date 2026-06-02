using System.Windows;
using System.Windows.Controls;
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

    public static Canvas CreateEncounterSwords(Point center, bool isSelected)
    {
        var bladeFill = new SolidColorBrush(Color.FromRgb(206, 192, 170));
        var bladeStroke = new SolidColorBrush(Color.FromRgb(80, 66, 49));
        var gripFill = new SolidColorBrush(isSelected ? Color.FromRgb(170, 48, 20) : Color.FromRgb(123, 83, 40));
        var guardFill = new SolidColorBrush(Color.FromRgb(192, 145, 55));

        var root = new Canvas { IsHitTestVisible = false };
        root.Children.Add(CreateSword(center, -36, bladeFill, bladeStroke, gripFill, guardFill));
        root.Children.Add(CreateSword(center, 36, bladeFill, bladeStroke, gripFill, guardFill));

        var jewel = new Ellipse
        {
            Width = isSelected ? 7 : 6,
            Height = isSelected ? 7 : 6,
            Fill = new SolidColorBrush(isSelected ? Color.FromRgb(255, 220, 110) : Color.FromRgb(220, 170, 75)),
            Stroke = bladeStroke,
            StrokeThickness = 1.1
        };
        Canvas.SetLeft(jewel, center.X - jewel.Width / 2);
        Canvas.SetTop(jewel, center.Y - jewel.Height / 2);
        root.Children.Add(jewel);

        return root;
    }

    private static Canvas CreateSword(
        Point center,
        double angle,
        Brush bladeFill,
        Brush bladeStroke,
        Brush gripFill,
        Brush guardFill)
    {
        const double bladeLength = 24;
        const double bladeWidth = 5.5;

        var sword = new Canvas();

        var blade = new Polygon
        {
            Points = new PointCollection
            {
                new(-bladeWidth / 2, -bladeLength),
                new(bladeWidth / 2, -bladeLength),
                new(bladeWidth * 0.35, -4),
                new(0, 0),
                new(-bladeWidth * 0.35, -4)
            },
            Fill = bladeFill,
            Stroke = bladeStroke,
            StrokeThickness = 1
        };
        sword.Children.Add(blade);

        var guard = new Rectangle
        {
            Width = 14,
            Height = 3.5,
            RadiusX = 1.2,
            RadiusY = 1.2,
            Fill = guardFill,
            Stroke = bladeStroke,
            StrokeThickness = 1
        };
        Canvas.SetLeft(guard, -guard.Width / 2);
        Canvas.SetTop(guard, 0.2);
        sword.Children.Add(guard);

        var grip = new Rectangle
        {
            Width = 3.8,
            Height = 11,
            RadiusX = 1,
            RadiusY = 1,
            Fill = gripFill,
            Stroke = bladeStroke,
            StrokeThickness = 1
        };
        Canvas.SetLeft(grip, -grip.Width / 2);
        Canvas.SetTop(grip, 3.2);
        sword.Children.Add(grip);

        var pommel = new Ellipse
        {
            Width = 4.2,
            Height = 4.2,
            Fill = guardFill,
            Stroke = bladeStroke,
            StrokeThickness = 1
        };
        Canvas.SetLeft(pommel, -pommel.Width / 2);
        Canvas.SetTop(pommel, 12.8);
        sword.Children.Add(pommel);

        sword.RenderTransform = new RotateTransform(angle, 0, 0);
        Canvas.SetLeft(sword, center.X);
        Canvas.SetTop(sword, center.Y);
        return sword;
    }
}
