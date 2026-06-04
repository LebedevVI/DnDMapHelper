using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
    private double _zoom = 1;
    private bool _isPanning;
    private Point _panStartMouse;
    private double _panStartScrollX;
    private double _panStartScrollY;
    private bool _isApplyingScroll;

    private const double MinZoom = 1;
    private const double MaxZoom = 6;
    private const double ZoomStep = 1.15;
    private const double KeyboardPanStep = 48;

    public MapCanvas()
    {
        InitializeComponent();
        Loaded += (_, _) => SubscribeSession();
        Unloaded += (_, _) => UnsubscribeSession();
        PreviewMouseDown += OnPreviewMouseDown;
        PreviewMouseMove += OnPreviewMouseMove;
        PreviewMouseUp += OnPreviewMouseUp;
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

    public Point CanvasToImage(Point viewPoint)
    {
        RecalculateViewport();
        return _viewport.CanvasToImage(viewPoint);
    }

    public Point ImageToCanvas(Point imagePoint)
    {
        RecalculateViewport();
        return _viewport.ImageToCanvas(imagePoint);
    }

    public Point GetViewPoint(MouseEventArgs e) => e.GetPosition(MapScrollViewer);

    public Point GetViewPoint(MouseButtonEventArgs e) => e.GetPosition(MapScrollViewer);

    public Point ViewToContent(Point viewPoint)
    {
        var (letterboxX, letterboxY) = GetLetterboxOffset();
        return new Point(
            viewPoint.X + MapScrollViewer.HorizontalOffset - letterboxX,
            viewPoint.Y + MapScrollViewer.VerticalOffset - letterboxY);
    }

    public Rect ContentToImage(Rect contentRect)
    {
        var scale = ContentScale;
        if (scale <= 0)
            return Rect.Empty;

        return new Rect(
            contentRect.X / scale,
            contentRect.Y / scale,
            contentRect.Width / scale,
            contentRect.Height / scale);
    }

    public void Refresh() => RedrawOverlay();

    public void ResetView() => _zoom = 1;

    public void ZoomIn() => ZoomAt(GetZoomCenter(), ZoomStep);

    public void ZoomOut() => ZoomAt(GetZoomCenter(), 1 / ZoomStep);

    public void ResetZoom()
    {
        ResetView();
        ApplyContentLayout();
        CenterScrollAtCurrentZoom();
        RedrawOverlay();
    }

    public double ZoomFactor => _zoom;

    private double ContentScale
    {
        get
        {
            if (_session.MapImage is null)
                return 1;

            return GetBaseScale() * _zoom;
        }
    }

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
        {
            MapImage.Source = _session.MapImage;
            if (e.PropertyName is nameof(GameSession.MapImage))
            {
                ResetView();
                Dispatcher.BeginInvoke(ResetZoom, System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        if (e.PropertyName is nameof(GameSession.MapImage)
            or nameof(GameSession.PartyPosition)
            or nameof(GameSession.PartyDisplayPosition)
            or nameof(GameSession.Targets)
            or nameof(GameSession.Regions)
            or nameof(GameSession.Encounters)
            or nameof(GameSession.DraftPath)
            or nameof(GameSession.DraftRegionOutline)
            or nameof(GameSession.Routes)
            or nameof(GameSession.SelectedRouteIndex)
            or nameof(GameSession.SelectedTargetId)
            or nameof(GameSession.SelectedRegionId)
            or nameof(GameSession.SelectedEncounterId)
            or null)
        {
            Dispatcher.BeginInvoke(RedrawOverlay);
        }
    }

    private void MapScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e) => RedrawOverlay();

    private void MapScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_isApplyingScroll)
            return;

        RecalculateViewport();
    }

    private double GetBaseScale()
    {
        if (_session.MapImage is null)
            return 1;

        var viewportWidth = MapScrollViewer.ViewportWidth;
        var viewportHeight = MapScrollViewer.ViewportHeight;
        if (viewportWidth <= 0 || viewportHeight <= 0)
            return 1;

        return Math.Min(
            viewportWidth / _session.MapImage.PixelWidth,
            viewportHeight / _session.MapImage.PixelHeight);
    }

    private Size GetContentSize()
    {
        if (_session.MapImage is null)
            return Size.Empty;

        var scale = ContentScale;
        return new Size(
            _session.MapImage.PixelWidth * scale,
            _session.MapImage.PixelHeight * scale);
    }

    private bool IsScrollable()
    {
        var content = GetContentSize();
        return content.Width > MapScrollViewer.ViewportWidth + 0.5
            || content.Height > MapScrollViewer.ViewportHeight + 0.5;
    }

    private (double X, double Y) GetLetterboxOffset()
    {
        var content = GetContentSize();
        var viewportWidth = MapScrollViewer.ViewportWidth;
        var viewportHeight = MapScrollViewer.ViewportHeight;
        if (!IsScrollable())
            return ((viewportWidth - content.Width) / 2, (viewportHeight - content.Height) / 2);

        return (0, 0);
    }

    private void ApplyContentLayout()
    {
        if (_session.MapImage is null)
        {
            MapContentCanvas.Width = 0;
            MapContentCanvas.Height = 0;
            OverlayCanvas.Width = 0;
            OverlayCanvas.Height = 0;
            return;
        }

        var content = GetContentSize();
        MapContentCanvas.Width = content.Width;
        MapContentCanvas.Height = content.Height;
        OverlayCanvas.Width = content.Width;
        OverlayCanvas.Height = content.Height;
        MapImage.Width = content.Width;
        MapImage.Height = content.Height;
    }

    private void RecalculateViewport()
    {
        if (_session.MapImage is null || MapScrollViewer.ViewportWidth <= 0 || MapScrollViewer.ViewportHeight <= 0)
        {
            _viewport = new MapViewport(0, 0, 1, 0, 0);
            return;
        }

        var (letterboxX, letterboxY) = GetLetterboxOffset();
        _viewport = new MapViewport(
            letterboxX - MapScrollViewer.HorizontalOffset,
            letterboxY - MapScrollViewer.VerticalOffset,
            ContentScale,
            _session.MapImage.PixelWidth,
            _session.MapImage.PixelHeight);
    }

    private void RedrawOverlay()
    {
        if (_isRedrawing)
            return;

        if (MapScrollViewer.ViewportWidth <= 0 || MapScrollViewer.ViewportHeight <= 0)
        {
            Dispatcher.BeginInvoke(RedrawOverlay, System.Windows.Threading.DispatcherPriority.Loaded);
            return;
        }

        _isRedrawing = true;
        try
        {
            ApplyContentLayout();
            RecalculateViewport();
            OverlayCanvas.Children.Clear();
            if (_session.MapImage is null)
                return;

            foreach (var region in _session.Regions)
            {
                if (ShouldDrawRegion(region))
                    DrawRegion(region);
            }

            DrawRoutes();
            DrawTargets();
            if (!IsPlayerMode)
                DrawEncounters();
            DrawParty();
        }
        finally
        {
            _isRedrawing = false;
        }
    }

    private Point ImageToContent(Point imagePoint)
    {
        var scale = ContentScale;
        return new Point(imagePoint.X * scale, imagePoint.Y * scale);
    }

    private bool ShouldDrawRegion(MapRegion region)
    {
        if (GetValue(IsPlayerModeProperty) is true)
            return region.VisibleToPlayers;

        return ShowRegions;
    }

    private void DrawRegion(MapRegion region)
    {
        if (region.Outline.Count < 3)
            return;

        var forPlayerDisplay = IsPlayerMode && region.VisibleToPlayers;
        DrawRegionOutline(
            region.Outline,
            isSelected: !IsPlayerMode && _session.SelectedRegionId == region.Id,
            isDraft: false,
            title: HighlightRegions || forPlayerDisplay ? region.Title : null,
            forPlayerDisplay: forPlayerDisplay);
    }

    private void DrawRegionOutline(
        IReadOnlyList<Point> imageOutline,
        bool isSelected = false,
        bool isDraft = false,
        string? title = null,
        bool forPlayerDisplay = false)
    {
        if (imageOutline.Count < 2)
            return;

        var canvasPoints = imageOutline.Select(ImageToContent).ToList();
        var geometry = canvasPoints.Count >= 3
            ? RegionGeometryHelper.CreateClosedSmoothPath(canvasPoints)
            : PathGeometryHelper.CreateSmoothPath(canvasPoints);

        var fill = isDraft
            ? new SolidColorBrush(Color.FromArgb(45, 201, 168, 108))
            : forPlayerDisplay || HighlightRegions
                ? new SolidColorBrush(Color.FromArgb((byte)(isSelected ? 90 : forPlayerDisplay ? 75 : 60), 201, 168, 108))
                : new SolidColorBrush(Color.FromArgb(25, 201, 168, 108));

        var shape = new Path
        {
            Data = geometry,
            Fill = fill,
            Stroke = new SolidColorBrush(isSelected
                ? Color.FromRgb(139, 37, 0)
                : forPlayerDisplay
                    ? Color.FromRgb(160, 120, 35)
                    : Color.FromRgb(139, 105, 20)),
            StrokeThickness = isSelected ? 3 : forPlayerDisplay || HighlightRegions || isDraft ? 2.5 : 1.5,
            StrokeDashArray = isSelected || HighlightRegions || isDraft || forPlayerDisplay
                ? null
                : new DoubleCollection([4, 3])
        };
        OverlayCanvas.Children.Add(shape);

        if (!string.IsNullOrWhiteSpace(title) && imageOutline.Count >= 1)
        {
            var bounds = RegionGeometryHelper.GetBounds(imageOutline);
            var topLeft = ImageToContent(bounds.TopLeft);
            var label = new TextBlock
            {
                Text = title,
                FontFamily = new FontFamily("Georgia"),
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(61, 41, 20)),
                Background = new SolidColorBrush(Color.FromArgb(180, 244, 232, 200))
            };
            Canvas.SetLeft(label, topLeft.X + 4);
            Canvas.SetTop(label, topLeft.Y + 4);
            OverlayCanvas.Children.Add(label);
        }
    }

    private Rect ContentRectFromImage(Rect imageRect)
    {
        var topLeft = ImageToContent(imageRect.TopLeft);
        var bottomRight = ImageToContent(imageRect.BottomRight);
        return new Rect(topLeft, bottomRight);
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

        if (!IsPlayerMode && _session.DraftRegionOutline.Count >= 2)
            DrawRegionOutline(_session.DraftRegionOutline, isDraft: true);
    }

    private void DrawRoutePath(
        IReadOnlyList<Point> imagePoints,
        bool isHighlighted,
        bool isActive,
        int? order,
        bool isDraft = false)
    {
        var canvasPoints = imagePoints.Select(ImageToContent).ToList();
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
            var center = ImageToContent(target.Position);
            var isSelected = !IsPlayerMode && _session.SelectedTargetId == target.Id;
            var size = isSelected ? 28 : 22;

            var marker = HandDrawnMarkerHelper.CreateTargetMarker(center, isSelected, target.Id);
            marker.Tag = target;
            OverlayCanvas.Children.Add(marker);

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

        var center = ImageToContent(pos.Value);
        OverlayCanvas.Children.Add(HandDrawnMarkerHelper.CreatePartyShield(center, isSelected: false));
    }

    private void DrawEncounters()
    {
        foreach (var encounter in _session.Encounters)
        {
            var center = ImageToContent(encounter.Position);
            var isSelected = _session.SelectedEncounterId == encounter.Id;
            OverlayCanvas.Children.Add(HandDrawnMarkerHelper.CreateEncounterSwords(center, isSelected));

            if (!string.IsNullOrWhiteSpace(encounter.Title))
            {
                var label = new TextBlock
                {
                    Text = encounter.Title,
                    FontFamily = new FontFamily("Georgia"),
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(255, 236, 179)),
                    Background = new SolidColorBrush(Color.FromArgb(210, 48, 27, 12)),
                    Padding = new Thickness(4, 2, 4, 2)
                };
                label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(label, center.X - label.DesiredSize.Width / 2);
                Canvas.SetTop(label, center.Y + 12);
                OverlayCanvas.Children.Add(label);
            }
        }
    }

    public MapRegion? HitTestRegion(Point viewPoint)
    {
        var imagePoint = CanvasToImage(viewPoint);
        for (var i = _session.Regions.Count - 1; i >= 0; i--)
        {
            var region = _session.Regions[i];
            if (region.Outline.Count >= 3 &&
                RegionGeometryHelper.ContainsPoint(region.Outline, imagePoint))
                return region;
        }

        return null;
    }

    public TargetMarker? HitTestTarget(Point viewPoint, double tolerance = 18)
    {
        var imagePoint = CanvasToImage(viewPoint);
        return _session.Targets
            .OrderBy(t => Distance(t.Position, imagePoint))
            .FirstOrDefault(t => Distance(t.Position, imagePoint) <= tolerance / Math.Max(_viewport.Scale, 0.01));
    }

    public EncounterPoint? HitTestEncounter(Point viewPoint, double tolerance = 20)
    {
        var imagePoint = CanvasToImage(viewPoint);
        return _session.Encounters
            .OrderBy(encounter => Distance(encounter.Position, imagePoint))
            .FirstOrDefault(encounter =>
                Distance(encounter.Position, imagePoint) <= tolerance / Math.Max(_viewport.Scale, 0.01));
    }

    private static double Distance(Point a, Point b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private Point GetZoomCenter()
    {
        if (MapScrollViewer.ViewportWidth > 0 && MapScrollViewer.ViewportHeight > 0)
            return new Point(MapScrollViewer.ViewportWidth / 2, MapScrollViewer.ViewportHeight / 2);

        return new Point(ActualWidth / 2, ActualHeight / 2);
    }

    private void ZoomAt(Point viewPoint, double factor)
    {
        if (_session.MapImage is null)
            return;

        var newZoom = Math.Clamp(_zoom * factor, MinZoom, MaxZoom);
        if (Math.Abs(newZoom - _zoom) < 0.0001)
            return;

        var imagePoint = CanvasToImage(viewPoint);
        _zoom = newZoom;

        ApplyContentLayout();

        var scale = ContentScale;
        var contentX = imagePoint.X * scale;
        var contentY = imagePoint.Y * scale;
        var (letterboxX, letterboxY) = GetLetterboxOffset();

        SetScrollOffsets(contentX + letterboxX - viewPoint.X, contentY + letterboxY - viewPoint.Y);
        RedrawOverlay();
    }

    private void CenterScrollAtCurrentZoom() => SetScrollOffsets(0, 0);

    private void SetScrollOffsets(double horizontal, double vertical)
    {
        _isApplyingScroll = true;
        try
        {
            MapScrollViewer.ScrollToHorizontalOffset(Math.Max(0, horizontal));
            MapScrollViewer.ScrollToVerticalOffset(Math.Max(0, vertical));
        }
        finally
        {
            _isApplyingScroll = false;
        }

        RecalculateViewport();
    }

    private void PanBy(double deltaX, double deltaY)
    {
        if (!IsScrollable())
            return;

        SetScrollOffsets(
            MapScrollViewer.HorizontalOffset + deltaX,
            MapScrollViewer.VerticalOffset + deltaY);
    }

    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_session.MapImage is null)
            return;

        Focus();
        var factor = e.Delta > 0 ? ZoomStep : 1 / ZoomStep;
        ZoomAt(e.GetPosition(MapScrollViewer), factor);
        e.Handled = true;
    }

    private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_session.MapImage is not null && e.ChangedButton == MouseButton.Left)
            Focus();

        if (e.ChangedButton != MouseButton.Middle || _session.MapImage is null)
            return;

        if (!IsScrollable())
            return;

        _isPanning = true;
        _panStartMouse = e.GetPosition(MapScrollViewer);
        _panStartScrollX = MapScrollViewer.HorizontalOffset;
        _panStartScrollY = MapScrollViewer.VerticalOffset;
        CaptureMouse();
        e.Handled = true;
    }

    private void OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isPanning)
            return;

        var current = e.GetPosition(MapScrollViewer);
        SetScrollOffsets(
            _panStartScrollX + (_panStartMouse.X - current.X),
            _panStartScrollY + (_panStartMouse.Y - current.Y));
        e.Handled = true;
    }

    private void OnPreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle || !_isPanning)
            return;

        _isPanning = false;
        ReleaseMouseCapture();
        e.Handled = true;
    }

    public bool TryHandlePanKey(Key key)
    {
        if (_session.MapImage is null || !IsScrollable())
            return false;

        switch (key)
        {
            case Key.Left:
            case Key.A:
                PanBy(-KeyboardPanStep, 0);
                return true;
            case Key.Right:
            case Key.D:
                PanBy(KeyboardPanStep, 0);
                return true;
            case Key.Up:
            case Key.W:
                PanBy(0, -KeyboardPanStep);
                return true;
            case Key.Down:
            case Key.S:
                PanBy(0, KeyboardPanStep);
                return true;
            default:
                return false;
        }
    }
}
