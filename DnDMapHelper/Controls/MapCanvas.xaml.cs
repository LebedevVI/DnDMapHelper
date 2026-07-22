using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
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
    private bool _redrawScheduled;
    private bool _staticLayerDirty = true;
    private bool _dynamicLayerDirty = true;
    private double _zoom = 1;
    private bool _isPanning;
    private Point _panStartMouse;
    private double _panStartScrollX;
    private double _panStartScrollY;
    private bool _isApplyingScroll;
    private QuestMapVisualState? _questVisualState;
    private double _routeGeometryScale = double.NaN;
    private readonly Dictionary<Guid, RouteGeometryCache> _routeGeometryCache = [];
    private readonly List<Point> _scratchCanvasPoints = [];

    private const double MinZoom = 1;
    private const double MaxZoom = 6;
    private const double ZoomStep = 1.15;
    private const double KeyboardPanStep = 48;

    private static readonly FontFamily LabelFont = new("Georgia");
    private static readonly DoubleCollection RouteDashPattern = CreateFrozenDash([10, 6]);
    private static readonly DoubleCollection DraftDashPattern = CreateFrozenDash([6, 3]);
    private static readonly DoubleCollection RegionDashPattern = CreateFrozenDash([4, 3]);

    private static readonly SolidColorBrush RouteDraftStroke = CreateFrozenBrush(255, 200, 60);
    private static readonly SolidColorBrush RouteActiveStroke = CreateFrozenBrush(210, 70, 20);
    private static readonly SolidColorBrush RouteSelectedStroke = CreateFrozenBrush(150, 95, 25);
    private static readonly SolidColorBrush RouteNormalStroke = CreateFrozenBrush(120, 80, 35);
    private static readonly SolidColorBrush RouteBadgeActiveBackground = CreateFrozenBrush(230, 139, 37, 0);
    private static readonly SolidColorBrush RouteBadgeNormalBackground = CreateFrozenBrush(200, 80, 55, 20);
    private static readonly SolidColorBrush LabelForeground = CreateFrozenBrush(255, 236, 179);
    private static readonly SolidColorBrush LabelBackground = CreateFrozenBrush(200, 45, 28, 12);
    private static readonly SolidColorBrush EncounterLabelBackground = CreateFrozenBrush(210, 48, 27, 12);
    private static readonly SolidColorBrush RegionLabelForeground = CreateFrozenBrush(61, 41, 20);
    private static readonly SolidColorBrush RegionLabelBackground = CreateFrozenBrush(180, 244, 232, 200);

    private sealed class RouteGeometryCache
    {
        public required IReadOnlyList<Point> SourcePoints;
        public required PathGeometry Geometry;
    }

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

    public Canvas OverlayCanvasElement => StaticOverlayCanvas;

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

    public void Refresh() => ScheduleRedraw();

    public void ResetView() => _zoom = 1;

    public void ZoomIn() => ZoomAt(GetZoomCenter(), ZoomStep);

    public void ZoomOut() => ZoomAt(GetZoomCenter(), 1 / ZoomStep);

    public void ResetZoom()
    {
        ResetView();
        ApplyContentLayout();
        CenterScrollAtCurrentZoom();
        InvalidateRouteGeometryCache();
        ScheduleRedraw();
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
            canvas.ScheduleRedraw(staticLayer: true, dynamicLayer: true);
    }

    private void SubscribeSession()
    {
        _session.PropertyChanged += OnSessionChanged;
        _session.Routes.CollectionChanged += OnRoutesCollectionChanged;
        MapImage.Source = _session.MapImage;
        ScheduleRedraw(staticLayer: true, dynamicLayer: true);
    }

    private void UnsubscribeSession()
    {
        _session.PropertyChanged -= OnSessionChanged;
        _session.Routes.CollectionChanged -= OnRoutesCollectionChanged;
    }

    private void OnRoutesCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        InvalidateRouteGeometryCache();
        ScheduleRedraw(staticLayer: true, dynamicLayer: false);
    }

    private void OnSessionChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(GameSession.MapImage) or null)
        {
            MapImage.Source = _session.MapImage;
            if (e.PropertyName is nameof(GameSession.MapImage))
            {
                ResetView();
                InvalidateRouteGeometryCache();
                Dispatcher.BeginInvoke(ResetZoom, DispatcherPriority.Loaded);
                return;
            }
        }

        if (e.PropertyName is nameof(GameSession.PartyDisplayPosition) or nameof(GameSession.PartyPosition))
        {
            ScheduleRedraw(staticLayer: false, dynamicLayer: true);
            return;
        }

        if (e.PropertyName is nameof(GameSession.DraftPath) or nameof(GameSession.DraftRegionOutline))
        {
            ScheduleRedraw(staticLayer: false, dynamicLayer: true);
            return;
        }

        if (e.PropertyName is nameof(GameSession.MapImage)
            or nameof(GameSession.Targets)
            or nameof(GameSession.Regions)
            or nameof(GameSession.Encounters)
            or nameof(GameSession.Routes)
            or nameof(GameSession.SelectedRouteIndex)
            or nameof(GameSession.SelectedTargetId)
            or nameof(GameSession.SelectedRegionId)
            or nameof(GameSession.SelectedEncounterId)
            or nameof(GameSession.Quests)
            or nameof(GameSession.SelectedQuestId))
        {
            if (e.PropertyName is nameof(GameSession.Quests) or nameof(GameSession.SelectedQuestId))
                InvalidateRouteGeometryCache();

            ScheduleRedraw(staticLayer: true, dynamicLayer: true);
        }
    }

    private void MapScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e) =>
        ScheduleRedraw(staticLayer: true, dynamicLayer: true);

    private void MapScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_isApplyingScroll)
            return;

        RecalculateViewport();
    }

    private void ScheduleRedraw(bool staticLayer = true, bool dynamicLayer = true)
    {
        if (staticLayer)
            _staticLayerDirty = true;
        if (dynamicLayer)
            _dynamicLayerDirty = true;

        if (_redrawScheduled)
            return;

        _redrawScheduled = true;
        Dispatcher.BeginInvoke(ProcessScheduledRedraw, DispatcherPriority.Render);
    }

    private void ProcessScheduledRedraw()
    {
        _redrawScheduled = false;
        RedrawOverlay();
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
            StaticOverlayCanvas.Width = 0;
            StaticOverlayCanvas.Height = 0;
            DynamicOverlayCanvas.Width = 0;
            DynamicOverlayCanvas.Height = 0;
            return;
        }

        var content = GetContentSize();
        MapContentCanvas.Width = content.Width;
        MapContentCanvas.Height = content.Height;
        StaticOverlayCanvas.Width = content.Width;
        StaticOverlayCanvas.Height = content.Height;
        DynamicOverlayCanvas.Width = content.Width;
        DynamicOverlayCanvas.Height = content.Height;
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
            // Keep dirty flags; SizeChanged will schedule a redraw. Do not spin the dispatcher.
            return;
        }

        if (!_staticLayerDirty && !_dynamicLayerDirty)
            return;

        _isRedrawing = true;
        try
        {
            ApplyContentLayout();
            RecalculateViewport();

            var scale = ContentScale;
            if (Math.Abs(scale - _routeGeometryScale) > 0.0001)
                InvalidateRouteGeometryCache();

            if (_session.MapImage is null)
            {
                StaticOverlayCanvas.Children.Clear();
                DynamicOverlayCanvas.Children.Clear();
                _questVisualState = null;
                return;
            }

            if (_staticLayerDirty)
            {
                _questVisualState = QuestMapVisualState.Build(_session, IsPlayerMode);
                StaticOverlayCanvas.Children.Clear();
                DrawStaticOverlay(_questVisualState, scale);
                _staticLayerDirty = false;
            }

            if (_dynamicLayerDirty)
            {
                DynamicOverlayCanvas.Children.Clear();
                DrawDynamicOverlay(scale);
                _dynamicLayerDirty = false;
            }
        }
        finally
        {
            _isRedrawing = false;
        }
    }

    private void DrawStaticOverlay(QuestMapVisualState questState, double scale)
    {
        foreach (var region in _session.Regions)
        {
            if (ShouldDrawRegion(region, questState))
                DrawRegion(region, questState, scale);
        }

        DrawCommittedRoutes(scale);

        foreach (var target in _session.Targets)
        {
            if (!questState.IsTargetVisible(target.Id))
                continue;

            DrawTarget(target, questState, scale);
        }

        if (!IsPlayerMode)
            DrawEncounters(scale);
    }

    private void DrawDynamicOverlay(double scale)
    {
        if (_session.DraftPath.Count >= 2)
            DrawRoutePath(_session.DraftPath, scale, isHighlighted: true, isActive: false, order: null, isDraft: true);

        if (!IsPlayerMode && _session.DraftRegionOutline.Count >= 2)
            DrawRegionOutline(_session.DraftRegionOutline, scale, isDraft: true);

        DrawParty(scale);
    }

    private Point ImageToContent(Point imagePoint, double scale) =>
        new(imagePoint.X * scale, imagePoint.Y * scale);

    private void TransformPointsToCanvas(IReadOnlyList<Point> imagePoints, double scale, List<Point> destination)
    {
        destination.Clear();
        destination.Capacity = Math.Max(destination.Capacity, imagePoints.Count);
        for (var i = 0; i < imagePoints.Count; i++)
        {
            var point = imagePoints[i];
            destination.Add(new Point(point.X * scale, point.Y * scale));
        }
    }

    private bool ShouldDrawRegion(MapRegion region, QuestMapVisualState? questState = null)
    {
        questState ??= _questVisualState ?? QuestMapVisualState.Build(_session, IsPlayerMode);
        if (!questState.IsRegionVisible(region.Id))
            return false;

        if (GetValue(IsPlayerModeProperty) is true)
            return region.VisibleToPlayers;

        return ShowRegions;
    }

    private void DrawRegion(MapRegion region, QuestMapVisualState questState, double scale)
    {
        if (region.Outline.Count < 3)
            return;

        var forPlayerDisplay = IsPlayerMode && region.VisibleToPlayers;
        var questHighlight = questState.IsRegionHighlighted(region.Id);
        var isSelected = !IsPlayerMode && _session.SelectedRegionId == region.Id;

        DrawRegionOutline(
            region.Outline,
            scale,
            isSelected: isSelected,
            isDraft: false,
            title: HighlightRegions || forPlayerDisplay ? region.Title : null,
            forPlayerDisplay: forPlayerDisplay,
            questHighlight: questHighlight);
    }

    private void DrawRegionOutline(
        IReadOnlyList<Point> imageOutline,
        double scale,
        bool isSelected = false,
        bool isDraft = false,
        string? title = null,
        bool forPlayerDisplay = false,
        bool questHighlight = false)
    {
        if (imageOutline.Count < 2)
            return;

        var outlinePoints = isDraft
            ? RegionGeometryHelper.PrepareDraftOutline(imageOutline)
            : imageOutline;
        TransformPointsToCanvas(outlinePoints, scale, _scratchCanvasPoints);
        var geometry = isDraft
            ? _scratchCanvasPoints.Count >= 3
                ? RegionGeometryHelper.CreateClosedSmoothPath(_scratchCanvasPoints)
                : PathGeometryHelper.CreateSmoothPath(_scratchCanvasPoints)
            : _scratchCanvasPoints.Count >= 3
                ? RegionGeometryHelper.CreateClosedSmoothPath(_scratchCanvasPoints)
                : PathGeometryHelper.CreateSmoothPath(_scratchCanvasPoints);

        const double draftFade = 0.75;
        const byte regionFillAlpha = 60;
        const byte regionStrokeAlpha = 255;
        var regionStrokeRgb = Color.FromRgb(139, 105, 20);

        var fill = isDraft
            ? new SolidColorBrush(Color.FromArgb((byte)(regionFillAlpha * draftFade), 201, 168, 108))
            : questHighlight
                ? new SolidColorBrush(Color.FromArgb(95, 255, 220, 120))
                : forPlayerDisplay || HighlightRegions
                    ? new SolidColorBrush(Color.FromArgb((byte)(isSelected ? 90 : forPlayerDisplay ? 75 : 60), 201, 168, 108))
                    : new SolidColorBrush(Color.FromArgb(25, 201, 168, 108));

        var shape = new Path
        {
            Data = geometry,
            Fill = fill,
            Stroke = isDraft
                ? new SolidColorBrush(Color.FromArgb((byte)(regionStrokeAlpha * draftFade), regionStrokeRgb.R, regionStrokeRgb.G, regionStrokeRgb.B))
                : new SolidColorBrush(isSelected
                    ? Color.FromRgb(139, 37, 0)
                    : questHighlight
                        ? Color.FromRgb(210, 160, 40)
                        : forPlayerDisplay
                            ? Color.FromRgb(160, 120, 35)
                            : regionStrokeRgb),
            StrokeThickness = isSelected ? 3 : isDraft ? 2.5 * draftFade : questHighlight || forPlayerDisplay || HighlightRegions ? 2.5 : 1.5,
            StrokeDashArray = isDraft
                ? RegionDashPattern
                : isSelected || HighlightRegions || forPlayerDisplay || questHighlight
                    ? null
                    : RegionDashPattern
        };
        var targetCanvas = isDraft ? DynamicOverlayCanvas : StaticOverlayCanvas;
        targetCanvas.Children.Add(shape);

        if (!string.IsNullOrWhiteSpace(title) && imageOutline.Count >= 1)
        {
            var bounds = RegionGeometryHelper.GetBounds(imageOutline);
            var topLeft = ImageToContent(bounds.TopLeft, scale);
            var label = new TextBlock
            {
                Text = title,
                FontFamily = LabelFont,
                FontSize = 11,
                Foreground = RegionLabelForeground,
                Background = RegionLabelBackground
            };
            Canvas.SetLeft(label, topLeft.X + 4);
            Canvas.SetTop(label, topLeft.Y + 4);
            StaticOverlayCanvas.Children.Add(label);
        }
    }

    private void DrawCommittedRoutes(double scale)
    {
        if (IsPlayerMode)
        {
            var active = _session.ActiveRoute;
            if (active is not null && active.Points.Count >= 2)
                DrawRoutePath(active.Points, scale, isHighlighted: true, isActive: true, active.Order, routeId: active.Id);
            return;
        }

        for (var i = 0; i < _session.Routes.Count; i++)
        {
            var route = _session.Routes[i];
            if (route.Points.Count < 2)
                continue;

            var isActive = i == 0;
            var isSelected = i == _session.SelectedRouteIndex;
            DrawRoutePath(route.Points, scale, isHighlighted: isSelected, isActive: isActive, route.Order, routeId: route.Id);
        }
    }

    private void DrawRoutePath(
        IReadOnlyList<Point> imagePoints,
        double scale,
        bool isHighlighted,
        bool isActive,
        int? order,
        bool isDraft = false,
        Guid? routeId = null)
    {
        PathGeometry geometry;
        if (!isDraft && routeId is { } id && TryGetCachedRouteGeometry(id, imagePoints, scale, out var cached))
        {
            geometry = cached;
        }
        else
        {
            TransformPointsToCanvas(imagePoints, scale, _scratchCanvasPoints);
            geometry = PathGeometryHelper.CreateSmoothPath(_scratchCanvasPoints);
            geometry.Freeze();
            if (!isDraft && routeId is { } cacheId)
                StoreRouteGeometry(cacheId, imagePoints, geometry);
        }

        double thickness;
        double opacity;
        SolidColorBrush stroke;

        if (isDraft)
        {
            stroke = RouteDraftStroke;
            thickness = 4;
            opacity = 1;
        }
        else if (isActive)
        {
            stroke = RouteActiveStroke;
            thickness = 3.5;
            opacity = 0.95;
        }
        else if (isHighlighted)
        {
            stroke = RouteSelectedStroke;
            thickness = 3;
            opacity = 0.9;
        }
        else
        {
            stroke = RouteNormalStroke;
            thickness = 2.5;
            opacity = 0.65;
        }

        var path = new Path
        {
            Data = geometry,
            Stroke = stroke,
            StrokeThickness = thickness,
            StrokeDashArray = isDraft ? DraftDashPattern : RouteDashPattern,
            Fill = Brushes.Transparent,
            Opacity = opacity
        };

        var targetCanvas = isDraft ? DynamicOverlayCanvas : StaticOverlayCanvas;
        targetCanvas.Children.Add(path);

        if (order.HasValue && !isDraft)
        {
            TransformPointsToCanvas(imagePoints, scale, _scratchCanvasPoints);
            var badgePoint = _scratchCanvasPoints[0];
            var badge = new Border
            {
                Background = isActive ? RouteBadgeActiveBackground : RouteBadgeNormalBackground,
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
            StaticOverlayCanvas.Children.Add(badge);
        }
    }

    private bool TryGetCachedRouteGeometry(Guid routeId, IReadOnlyList<Point> sourcePoints, double scale, out PathGeometry geometry)
    {
        if (_routeGeometryCache.TryGetValue(routeId, out var cache)
            && ReferenceEquals(cache.SourcePoints, sourcePoints))
        {
            geometry = cache.Geometry;
            return true;
        }

        geometry = null!;
        return false;
    }

    private void StoreRouteGeometry(Guid routeId, IReadOnlyList<Point> sourcePoints, PathGeometry geometry)
    {
        _routeGeometryCache[routeId] = new RouteGeometryCache
        {
            SourcePoints = sourcePoints,
            Geometry = geometry
        };
        _routeGeometryScale = ContentScale;
    }

    private void InvalidateRouteGeometryCache()
    {
        _routeGeometryCache.Clear();
        _routeGeometryScale = double.NaN;
    }

    private void DrawTarget(TargetMarker target, QuestMapVisualState questState, double scale)
    {
        var center = ImageToContent(target.Position, scale);
        var isSelected = !IsPlayerMode && _session.SelectedTargetId == target.Id;
        var questHighlight = questState.IsTargetHighlighted(target.Id);
        var size = isSelected || questHighlight ? 28 : 22;

        var marker = HandDrawnMarkerHelper.CreateTargetMarker(center, isSelected, target.Id, questHighlight);
        marker.Tag = target;
        StaticOverlayCanvas.Children.Add(marker);

        if (!string.IsNullOrWhiteSpace(target.Label))
        {
            var label = new TextBlock
            {
                Text = target.Label,
                FontFamily = LabelFont,
                FontSize = isSelected ? 13 : 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = LabelForeground,
                Background = LabelBackground,
                Padding = new Thickness(4, 2, 4, 2)
            };
            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(label, center.X - label.DesiredSize.Width / 2);
            Canvas.SetTop(label, center.Y + size / 2 + 4);
            StaticOverlayCanvas.Children.Add(label);
        }
    }

    private void DrawParty(double scale)
    {
        var pos = _session.PartyDisplayPosition;
        if (!pos.HasValue)
            return;

        var center = ImageToContent(pos.Value, scale);
        DynamicOverlayCanvas.Children.Add(HandDrawnMarkerHelper.CreatePartyShield(center, isSelected: false));
    }

    private void DrawEncounters(double scale)
    {
        foreach (var encounter in _session.Encounters)
        {
            var center = ImageToContent(encounter.Position, scale);
            var isSelected = _session.SelectedEncounterId == encounter.Id;
            StaticOverlayCanvas.Children.Add(HandDrawnMarkerHelper.CreateEncounterSwords(center, isSelected));

            if (!string.IsNullOrWhiteSpace(encounter.Title))
            {
                var label = new TextBlock
                {
                    Text = encounter.Title,
                    FontFamily = LabelFont,
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = LabelForeground,
                    Background = EncounterLabelBackground,
                    Padding = new Thickness(4, 2, 4, 2)
                };
                label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(label, center.X - label.DesiredSize.Width / 2);
                Canvas.SetTop(label, center.Y + 12);
                StaticOverlayCanvas.Children.Add(label);
            }
        }
    }

    public MapRegion? HitTestRegion(Point viewPoint)
    {
        var imagePoint = CanvasToImage(viewPoint);
        for (var i = _session.Regions.Count - 1; i >= 0; i--)
        {
            var region = _session.Regions[i];
            if (!ShouldDrawRegion(region))
                continue;

            if (region.Outline.Count >= 3 &&
                RegionGeometryHelper.ContainsPoint(region.Outline, imagePoint))
                return region;
        }

        return null;
    }

    public TargetMarker? HitTestTarget(Point viewPoint, double tolerance = 18)
    {
        var imagePoint = CanvasToImage(viewPoint);
        var questState = _questVisualState ?? QuestMapVisualState.Build(_session, IsPlayerMode);
        TargetMarker? closest = null;
        var closestDistance = double.MaxValue;

        foreach (var target in _session.Targets)
        {
            if (!questState.IsTargetVisible(target.Id))
                continue;

            var distance = Distance(target.Position, imagePoint);
            if (distance >= closestDistance)
                continue;

            closest = target;
            closestDistance = distance;
        }

        return closest is not null &&
               closestDistance <= tolerance / Math.Max(_viewport.Scale, 0.01)
            ? closest
            : null;
    }

    public EncounterPoint? HitTestEncounter(Point viewPoint, double tolerance = 20)
    {
        var imagePoint = CanvasToImage(viewPoint);
        EncounterPoint? closest = null;
        var closestDistance = double.MaxValue;

        foreach (var encounter in _session.Encounters)
        {
            var distance = Distance(encounter.Position, imagePoint);
            if (distance >= closestDistance)
                continue;

            closest = encounter;
            closestDistance = distance;
        }

        return closest is not null &&
               closestDistance <= tolerance / Math.Max(_viewport.Scale, 0.01)
            ? closest
            : null;
    }

    private static double Distance(Point a, Point b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static SolidColorBrush CreateFrozenBrush(byte a, byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
        brush.Freeze();
        return brush;
    }

    private static SolidColorBrush CreateFrozenBrush(byte r, byte g, byte b) =>
        CreateFrozenBrush(255, r, g, b);

    private static DoubleCollection CreateFrozenDash(double[] values)
    {
        var collection = new DoubleCollection(values);
        collection.Freeze();
        return collection;
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
        InvalidateRouteGeometryCache();

        var scale = ContentScale;
        var contentX = imagePoint.X * scale;
        var contentY = imagePoint.Y * scale;
        var (letterboxX, letterboxY) = GetLetterboxOffset();

        SetScrollOffsets(contentX + letterboxX - viewPoint.X, contentY + letterboxY - viewPoint.Y);
        ScheduleRedraw(staticLayer: true, dynamicLayer: true);
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
