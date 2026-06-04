using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DnDMapHelper.Helpers;

public static class HandDrawnMarkerHelper
{
    private static readonly SolidColorBrush OutlineStroke = new(Color.FromRgb(80, 66, 49));
    private static readonly SolidColorBrush GoldStroke = new(Color.FromRgb(192, 145, 55));
    private static readonly SolidColorBrush GoldStrokeBright = new(Color.FromRgb(255, 210, 90));
    private static readonly SolidColorBrush ParchmentFill = new(Color.FromRgb(244, 228, 198));

    public static Canvas CreateTargetMarker(Point center, bool isSelected, Guid targetId)
    {
        var hash = targetId.GetHashCode();
        var wobble = (PseudoRandom(hash, 1) - 0.5) * 6;

        var root = new Canvas { IsHitTestVisible = false };
        root.RenderTransform = new RotateTransform(wobble, center.X, center.Y);

        var ringSize = isSelected ? 30 : 26;
        var outerRing = new Ellipse
        {
            Width = ringSize,
            Height = ringSize,
            Fill = new SolidColorBrush(Color.FromArgb(225, 248, 236, 210)),
            Stroke = isSelected ? GoldStrokeBright : GoldStroke,
            StrokeThickness = isSelected ? 2.6 : 2.1
        };
        PlaceCentered(root, outerRing, center, ringSize, ringSize);

        var innerRingSize = ringSize - 7;
        var innerRing = new Ellipse
        {
            Width = innerRingSize,
            Height = innerRingSize,
            Fill = Brushes.Transparent,
            Stroke = OutlineStroke,
            StrokeThickness = 1.1,
            Opacity = 0.65
        };
        PlaceCentered(root, innerRing, center, innerRingSize, innerRingSize);

        var barFill = new SolidColorBrush(isSelected ? Color.FromRgb(186, 42, 18) : Color.FromRgb(148, 36, 16));
        AddCrossBar(root, center, 45, barFill, OutlineStroke, isSelected);
        AddCrossBar(root, center, -45, barFill, OutlineStroke, isSelected);

        var jewelSize = isSelected ? 7.5 : 6.5;
        var jewel = new Ellipse
        {
            Width = jewelSize,
            Height = jewelSize,
            Fill = new SolidColorBrush(isSelected ? Color.FromRgb(255, 220, 110) : Color.FromRgb(220, 170, 75)),
            Stroke = OutlineStroke,
            StrokeThickness = 1.1
        };
        PlaceCentered(root, jewel, center, jewelSize, jewelSize);

        AddCardinalTicks(root, center, ringSize * 0.5 + 1, OutlineStroke);

        return root;
    }

    public static Canvas CreatePartyShield(Point center, bool isSelected)
    {
        var root = new Canvas { IsHitTestVisible = false };

        var glowSize = isSelected ? 38 : 34;
        var glow = new Ellipse
        {
            Width = glowSize,
            Height = glowSize,
            Fill = new SolidColorBrush(Color.FromArgb(isSelected ? (byte)95 : (byte)70, 255, 215, 130)),
            Stroke = isSelected ? GoldStrokeBright : GoldStroke,
            StrokeThickness = isSelected ? 2.2 : 1.8
        };
        PlaceCentered(root, glow, center, glowSize, glowSize);

        var shield = new Polygon
        {
            Points = new PointCollection
            {
                new(center.X, center.Y - 16),
                new(center.X + 12, center.Y - 10),
                new(center.X + 14, center.Y + 1),
                new(center.X + 10, center.Y + 14),
                new(center.X, center.Y + 18),
                new(center.X - 10, center.Y + 14),
                new(center.X - 14, center.Y + 1),
                new(center.X - 12, center.Y - 10)
            },
            Fill = new LinearGradientBrush(
                isSelected ? Color.FromRgb(92, 138, 228) : Color.FromRgb(58, 98, 196),
                isSelected ? Color.FromRgb(32, 52, 128) : Color.FromRgb(22, 38, 98),
                new Point(center.X, center.Y - 16),
                new Point(center.X, center.Y + 18)),
            Stroke = isSelected ? GoldStrokeBright : GoldStroke,
            StrokeThickness = isSelected ? 2.6 : 2.1,
            StrokeLineJoin = PenLineJoin.Round
        };
        root.Children.Add(shield);

        var inset = new Polygon
        {
            Points = new PointCollection
            {
                new(center.X, center.Y - 11),
                new(center.X + 8, center.Y - 7),
                new(center.X + 9, center.Y + 1),
                new(center.X + 6, center.Y + 10),
                new(center.X, center.Y + 12),
                new(center.X - 6, center.Y + 10),
                new(center.X - 9, center.Y + 1),
                new(center.X - 8, center.Y - 7)
            },
            Fill = new SolidColorBrush(Color.FromArgb(55, 180, 205, 255)),
            Stroke = OutlineStroke,
            StrokeThickness = 1,
            StrokeLineJoin = PenLineJoin.Round
        };
        root.Children.Add(inset);

        var chevron = new Polygon
        {
            Points = new PointCollection
            {
                new(center.X, center.Y - 7),
                new(center.X + 5, center.Y + 1),
                new(center.X, center.Y - 1),
                new(center.X - 5, center.Y + 1)
            },
            Fill = new SolidColorBrush(Color.FromArgb(210, 210, 228, 255)),
            Stroke = OutlineStroke,
            StrokeThickness = 1
        };
        root.Children.Add(chevron);

        var bossSize = isSelected ? 6.5 : 5.5;
        var boss = new Ellipse
        {
            Width = bossSize,
            Height = bossSize,
            Fill = GoldStroke,
            Stroke = OutlineStroke,
            StrokeThickness = 1
        };
        PlaceCentered(root, boss, new Point(center.X, center.Y + 8), bossSize, bossSize);

        return root;
    }

    public static Canvas CreateEncounterSwords(Point center, bool isSelected)
    {
        var bladeFill = new SolidColorBrush(Color.FromRgb(206, 192, 170));
        var bladeStroke = OutlineStroke;
        var gripFill = new SolidColorBrush(isSelected ? Color.FromRgb(170, 48, 20) : Color.FromRgb(123, 83, 40));
        var guardFill = GoldStroke;

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
        PlaceCentered(root, jewel, center, jewel.Width, jewel.Height);

        return root;
    }

    private static void AddCrossBar(
        Canvas parent,
        Point center,
        double angle,
        Brush fill,
        Brush stroke,
        bool isSelected)
    {
        var bar = new Rectangle
        {
            Width = isSelected ? 5.2 : 4.4,
            Height = isSelected ? 17 : 14.5,
            RadiusX = 1.6,
            RadiusY = 1.6,
            Fill = fill,
            Stroke = stroke,
            StrokeThickness = 1
        };
        PlaceCentered(parent, bar, center, bar.Width, bar.Height);
        bar.RenderTransform = new RotateTransform(angle, center.X, center.Y);
    }

    private static void AddCardinalTicks(Canvas parent, Point center, double radius, Brush stroke)
    {
        for (var i = 0; i < 4; i++)
        {
            var angle = i * 90;
            var rad = angle * Math.PI / 180;
            var outer = radius + 2.5;
            var inner = radius - 1.5;
            var line = new Line
            {
                X1 = center.X + Math.Cos(rad) * inner,
                Y1 = center.Y + Math.Sin(rad) * inner,
                X2 = center.X + Math.Cos(rad) * outer,
                Y2 = center.Y + Math.Sin(rad) * outer,
                Stroke = stroke,
                StrokeThickness = 1.2,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Opacity = 0.75
            };
            parent.Children.Add(line);
        }
    }

    private static void PlaceCentered(Canvas parent, UIElement element, Point center, double width, double height)
    {
        Canvas.SetLeft(element, center.X - width / 2);
        Canvas.SetTop(element, center.Y - height / 2);
        parent.Children.Add(element);
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

    private static double PseudoRandom(int seed, int channel) =>
        ((seed * 1103515245 + channel * 12345) & 0x7FFFFFFF) / (double)0x7FFFFFFF;
}
