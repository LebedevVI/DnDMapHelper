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
        MapImage.Source = _session.MapImage;
        RedrawOverlay();
    }

    private void UnsubscribeSession() => _session.PropertyChanged -= OnSessionChanged;

    private void OnSessionChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(GameSession.MapImage) or null)
            MapImage.Source = _session.MapImage;

        if (e.PropertyName is nameof(GameSession.MapImage)
            or nameof(GameSession.PartyPosition)
            or nameof(GameSession.PartyDisplayPosition)
            or nameof(GameSession.Targets)
            or nameof(GameSession.Regions)
            or nameof(GameSession.MovementPath)
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

            DrawPath();
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

    private void DrawPath()
    {
        if (_session.MovementPath.Count < 2)
            return;

        var canvasPoints = _session.MovementPath.Select(_viewport.ImageToCanvas).ToList();
        var geometry = PathGeometryHelper.CreateSmoothPath(canvasPoints);

        var path = new Path
        {
            Data = geometry,
            Stroke = new SolidColorBrush(Color.FromRgb(139, 37, 0)),
            StrokeThickness = 3,
            StrokeDashArray = [6, 4],
            Fill = Brushes.Transparent,
            Opacity = 0.85
        };
        OverlayCanvas.Children.Add(path);
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
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.Gold,
                    Background = new SolidColorBrush(Color.FromArgb(160, 30, 20, 10))
                };
                Canvas.SetLeft(label, center.X + size / 2 + 2);
                Canvas.SetTop(label, center.Y - 8);
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
