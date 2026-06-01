using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using DnDMapHelper.Helpers;
using DnDMapHelper.Models;
using DnDMapHelper.Services;

namespace DnDMapHelper.Controls;

public partial class MapCanvas : UserControl
{
    public static readonly DependencyProperty IsPlayerModeProperty =
        DependencyProperty.Register(nameof(IsPlayerMode), typeof(bool), typeof(MapCanvas),
            new PropertyMetadata(false, OnVisualPropertyChanged));

    public static readonly DependencyProperty ShowRegionsProperty =
        DependencyProperty.Register(nameof(ShowRegions), typeof(bool), typeof(MapCanvas),
            new PropertyMetadata(true, OnVisualPropertyChanged));

    public static readonly DependencyProperty HighlightRegionsProperty =
        DependencyProperty.Register(nameof(HighlightRegions), typeof(bool), typeof(MapCanvas),
            new PropertyMetadata(false, OnVisualPropertyChanged));

    private readonly GameSession _session = GameSession.Current;
    private MapViewport _viewport;
    private bool _isRedrawing;

    public MapCanvas()
    {
        InitializeComponent();
        Loaded += (_, _) => SubscribeSession();
        Unloaded += (_, _) => UnsubscribeSession();
    }

    public bool IsPlayerMode
    {
        get => (bool)GetValue(IsPlayerModeProperty);
        set => SetValue(IsPlayerModeProperty, value);
    }

    public bool ShowRegions
    {
        get => (bool)GetValue(ShowRegionsProperty);
        set => SetValue(ShowRegionsProperty, value);
    }

    public bool HighlightRegions
    {
        get => (bool)GetValue(HighlightRegionsProperty);
        set => SetValue(HighlightRegionsProperty, value);
    }

    public MapViewport Viewport => _viewport;

    public Canvas OverlayCanvasElement => OverlayCanvas;

    public Point CanvasToImage(Point canvasPoint) => _viewport.CanvasToImage(canvasPoint);

    public Point ImageToCanvas(Point imagePoint) => _viewport.ImageToCanvas(imagePoint);

    public void Refresh() => RedrawOverlay();

    private static void OnVisualPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MapCanvas canvas)
            canvas.RedrawOverlay();
    }

    private void SubscribeSession()
    {
        _session.PropertyChanged += OnSessionChanged;
        _session.Routes.CollectionChanged += OnRoutesCollectionChanged;
        MapImage.Source = _session.MapImage;
        RedrawOverlay();
    }

    private void UnsubscribeSession()
    {
        _session.PropertyChanged -= OnSessionChanged;
        _session.Routes.CollectionChanged -= OnRoutesCollectionChanged;
    }

    private void OnRoutesCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) =>
        Dispatcher.BeginInvoke(RedrawOverlay);

    private void OnSessionChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(GameSession.MapImage) or null)
            MapImage.Source = _session.MapImage;

        if (e.PropertyName is nameof(GameSession.MapImage)
            or nameof(GameSession.PartyPosition)
            or nameof(GameSession.PartyDisplayPosition)
            or nameof(GameSession.Targets)
            or nameof(GameSession.Regions)
            or nameof(GameSession.DraftPath)
            or nameof(GameSession.Routes)
            or nameof(GameSession.SelectedRouteIndex)
            or nameof(GameSession.SelectedTargetId)
            or null)
        {
            Dispatcher.BeginInvoke(RedrawOverlay);
        }
    }

    private void RootGrid_SizeChanged(object sender, SizeChangedEventArgs e) => RedrawOverlay();

    private void RecalculateViewport()
    {
        if (_session.MapImage is null || RootGrid.ActualWidth <= 0 || RootGrid.ActualHeight <= 0)
        {
            _viewport = new MapViewport(0, 0, 1, 0, 0);
            return;
        }

        _viewport = MapCoordinateHelper.Calculate(
            new Size(RootGrid.ActualWidth, RootGrid.ActualHeight),
            _session.MapImage.PixelWidth,
            _session.MapImage.PixelHeight);
    }

    private void RedrawOverlay()
    {
        if (_isRedrawing)
            return;

        _isRedrawing = true;
        try
        {
            RecalculateViewport();
            OverlayCanvas.Children.Clear();
            if (_session.MapImage is null)
                return;

            if (ShowRegions)
            {
                foreach (var region in _session.Regions)
                    DrawRegion(region);
            }

            DrawRoutes();
            DrawTargets();
            DrawParty();
        }
        finally
        {
            _isRedrawing = false;
        }
    }

    private void DrawRegion(MapRegion region)
    {
        var rect = _viewport.ImageToCanvas(region.Bounds);
        var fill = HighlightRegions
            ? new SolidColorBrush(Color.FromArgb(60, 201, 168, 108))
            : new SolidColorBrush(Color.FromArgb(25, 201, 168, 108));

        var shape = new Rectangle
        {
            Width = Math.Max(1, rect.Width),
            Height = Math.Max(1, rect.Height),
            Fill = fill,
            Stroke = new SolidColorBrush(Color.FromRgb(139, 105, 20)),
            StrokeThickness = HighlightRegions ? 2.5 : 1.5,
            StrokeDashArray = HighlightRegions ? null : [4, 3],
            Tag = region
        };

        Canvas.SetLeft(shape, rect.X);
        Canvas.SetTop(shape, rect.Y);
        OverlayCanvas.Children.Add(shape);

        if (HighlightRegions && !string.IsNullOrWhiteSpace(region.Title))
        {
            var label = new TextBlock
            {
                Text = region.Title,
                FontFamily = new FontFamily("Georgia"),
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(61, 41, 20)),
                Background = new SolidColorBrush(Color.FromArgb(180, 244, 232, 200))
            };
            Canvas.SetLeft(label, rect.X + 4);
            Canvas.SetTop(label, rect.Y + 4);
            OverlayCanvas.Children.Add(label);
        }
    }

    private void DrawRoutes()
    {
        if (IsPlayerMode)
        {
            var active = _session.ActiveRoute;
            if (active is not null && active.Points.Count >= 2)
                DrawRoutePath(active.Points, isHighlighted: true, isActive: true, active.Order);
            return;
        }

        for (var i = 0; i < _session.Routes.Count; i++)
        {
            var route = _session.Routes[i];
            if (route.Points.Count < 2)
                continue;

            var isActive = i == 0;
            var isSelected = i == _session.SelectedRouteIndex;
            DrawRoutePath(route.Points, isHighlighted: isSelected, isActive: isActive, route.Order);
        }

        if (_session.DraftPath.Count >= 2)
            DrawRoutePath(_session.DraftPath, isHighlighted: true, isActive: false, order: null, isDraft: true);
    }

    private void DrawRoutePath(
        IReadOnlyList<Point> imagePoints,
        bool isHighlighted,
        bool isActive,
        int? order,
        bool isDraft = false)
    {
        var canvasPoints = imagePoints.Select(_viewport.ImageToCanvas).ToList();
        var geometry = PathGeometryHelper.CreateSmoothPath(canvasPoints);

        Color strokeColor;
        double thickness;
        double opacity;

        if (isDraft)
        {
            strokeColor = Color.FromRgb(255, 200, 60);
            thickness = 4;
            opacity = 1;
        }
        else if (isActive)
        {
            strokeColor = Color.FromRgb(210, 70, 20);
            thickness = 3.5;
            opacity = 0.95;
        }
        else if (isHighlighted)
        {
            strokeColor = Color.FromRgb(150, 95, 25);
            thickness = 3;
            opacity = 0.9;
        }
        else
        {
            strokeColor = Color.FromRgb(120, 80, 35);
            thickness = 2.5;
            opacity = 0.65;
        }

        var dashPattern = isDraft
            ? new DoubleCollection([6, 3])
            : new DoubleCollection([10, 6]);

        var path = new Path
        {
            Data = geometry,
            Stroke = new SolidColorBrush(strokeColor),
            StrokeThickness = thickness,
            StrokeDashArray = dashPattern,
            Fill = Brushes.Transparent,
            Opacity = opacity
        };
        OverlayCanvas.Children.Add(path);

        if (order.HasValue && !isDraft)
        {
            var badgePoint = canvasPoints[0];
            var badge = new Border
            {
                Background = new SolidColorBrush(isActive
                    ? Color.FromArgb(230, 139, 37, 0)
                    : Color.FromArgb(200, 80, 55, 20)),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(6, 2, 6, 2),
                Child = new TextBlock
                {
                    Text = order.Value.ToString(),
                    FontWeight = FontWeights.Bold,
                    FontSize = 11,
                    Foreground = Brushes.White
                }
            };
            Canvas.SetLeft(badge, badgePoint.X - 10);
            Canvas.SetTop(badge, badgePoint.Y - 22);
            OverlayCanvas.Children.Add(badge);
        }
    }

    private void DrawTargets()
    {
        foreach (var target in _session.Targets)
        {
            var center = _viewport.ImageToCanvas(target.Position);
            var isSelected = _session.SelectedTargetId == target.Id;
            var size = isSelected ? 28 : 22;

            var cross = HandDrawnMarkerHelper.CreateTargetCross(center, size, isSelected, target.Id);
            cross.Tag = target;
            OverlayCanvas.Children.Add(cross);

            if (!string.IsNullOrWhiteSpace(target.Label))
            {
                var label = new TextBlock
                {
                    Text = target.Label,
                    FontFamily = new FontFamily("Georgia"),
                    FontSize = isSelected ? 13 : 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(255, 236, 179)),
                    Background = new SolidColorBrush(Color.FromArgb(200, 45, 28, 12)),
                    Padding = new Thickness(4, 2, 4, 2)
                };
                label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(label, center.X - label.DesiredSize.Width / 2);
                Canvas.SetTop(label, center.Y + size / 2 + 4);
                OverlayCanvas.Children.Add(label);
            }
        }
    }

    private void DrawParty()
    {
        var pos = _session.PartyDisplayPosition;
        if (!pos.HasValue)
            return;

        var center = _viewport.ImageToCanvas(pos.Value);
        const double radius = 14;

        var outer = new Ellipse
        {
            Width = radius * 2 + 6,
            Height = radius * 2 + 6,
            Fill = new SolidColorBrush(Color.FromArgb(80, 255, 215, 0)),
            Stroke = new SolidColorBrush(Color.FromRgb(218, 165, 32)),
            StrokeThickness = 2
        };
        Canvas.SetLeft(outer, center.X - radius - 3);
        Canvas.SetTop(outer, center.Y - radius - 3);
        OverlayCanvas.Children.Add(outer);

        var marker = new Ellipse
        {
            Width = radius * 2,
            Height = radius * 2,
            Fill = new RadialGradientBrush(
                Color.FromRgb(65, 105, 225),
                Color.FromRgb(25, 25, 112))
            {
                GradientOrigin = new Point(0.3, 0.3),
                Center = new Point(0.3, 0.3)
            },
            Stroke = Brushes.White,
            StrokeThickness = 2
        };
        Canvas.SetLeft(marker, center.X - radius);
        Canvas.SetTop(marker, center.Y - radius);
        OverlayCanvas.Children.Add(marker);

        var pin = new Polygon
        {
            Points = new PointCollection
            {
                new(center.X, center.Y - radius - 8),
                new(center.X - 6, center.Y - radius + 2),
                new(center.X + 6, center.Y - radius + 2)
            },
            Fill = new SolidColorBrush(Color.FromRgb(218, 165, 32)),
            Stroke = Brushes.White,
            StrokeThickness = 1
        };
        OverlayCanvas.Children.Add(pin);
    }

    public MapRegion? HitTestRegion(Point canvasPoint)
    {
        var imagePoint = CanvasToImage(canvasPoint);
        for (var i = _session.Regions.Count - 1; i >= 0; i--)
        {
            if (_session.Regions[i].Bounds.Contains(imagePoint))
                return _session.Regions[i];
        }

        return null;
    }

    public TargetMarker? HitTestTarget(Point canvasPoint, double tolerance = 18)
    {
        var imagePoint = CanvasToImage(canvasPoint);
        return _session.Targets
            .OrderBy(t => Distance(t.Position, imagePoint))
            .FirstOrDefault(t => Distance(t.Position, imagePoint) <= tolerance / Math.Max(_viewport.Scale, 0.01));
    }

    private static double Distance(Point a, Point b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
