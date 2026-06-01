using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using DnDMapHelper.Models;
using DnDMapHelper.Services;
using Microsoft.Win32;

namespace DnDMapHelper.Views;

public partial class MasterWindow : Window
{
    private readonly GameSession _session = GameSession.Current;
    private MasterTool _currentTool = MasterTool.Navigate;
    private bool _isDrawingPath;
    private bool _isDrawingRegion;
    private Point _regionStartCanvas;
    private readonly List<Point> _pathPointsImage = [];
    private Rectangle? _regionPreview;
    private PlayerWindow? _playerWindow;

    public MasterWindow()
    {
        InitializeComponent();
        ToolNavigate.IsChecked = true;
        UpdateStatus();
        _session.PropertyChanged += (_, _) => UpdateStatus();
    }

    private void LoadMap_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Изображения|*.png;*.jpg;*.jpeg;*.bmp;*.gif|Все файлы|*.*",
            Title = "Выберите карту"
        };

        if (dialog.ShowDialog() != true)
            return;

        var image = new BitmapImage();
        image.BeginInit();
        image.UriSource = new Uri(dialog.FileName);
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.EndInit();
        image.Freeze();

        _session.MapImage = image;
        _session.Targets.Clear();
        _session.Regions.Clear();
        _session.PartyPosition = null;
        _session.ClearPath();
        _session.SelectedTargetId = null;
        MapView.Refresh();
        UpdateStatus("Карта загружена. Разместите метку партии и цели.");
    }

    private void ClearPath_Click(object sender, RoutedEventArgs e)
    {
        _session.ClearPath();
        _pathPointsImage.Clear();
        MapView.Refresh();
    }

    private void OpenPlayer_Click(object sender, RoutedEventArgs e)
    {
        if (_playerWindow is { IsLoaded: true })
        {
            _playerWindow.Activate();
            if (_playerWindow.WindowState == WindowState.Minimized)
                _playerWindow.WindowState = WindowState.Normal;
            return;
        }

        _playerWindow = new PlayerWindow { Owner = this };
        _playerWindow.Closed += (_, _) => _playerWindow = null;
        _playerWindow.Show();
    }

    private void Tool_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton button || button.Tag is not string tag)
            return;

        UncheckOtherTools(button);
        _currentTool = Enum.Parse<MasterTool>(tag);
        FinishTransientDrawing();
        UpdateStatus();
    }

    private void UncheckOtherTools(ToggleButton active)
    {
        foreach (var tool in new[] { ToolNavigate, ToolParty, ToolTarget, ToolPath, ToolRegion })
        {
            if (tool != active)
                tool.IsChecked = false;
        }

        if (!ToolNavigate.IsChecked && !ToolParty.IsChecked && !ToolTarget.IsChecked &&
            !ToolPath.IsChecked && !ToolRegion.IsChecked)
            active.IsChecked = true;
    }

    private void MapView_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_session.MapImage is null)
        {
            UpdateStatus("Сначала загрузите карту.");
            return;
        }

        var canvasPoint = e.GetPosition(MapView);
        var imagePoint = MapView.CanvasToImage(canvasPoint);

        if (!IsPointOnMap(imagePoint))
            return;

        switch (_currentTool)
        {
            case MasterTool.PartyMarker:
                _session.PartyPosition = imagePoint;
                _session.ResetPartyMovement();
                MapView.Refresh();
                break;

            case MasterTool.TargetMarker:
                var target = new TargetMarker { Position = imagePoint, Label = $"Цель {_session.Targets.Count + 1}" };
                _session.Targets.Add(target);
                _session.SelectTarget(target.Id);
                MapView.Refresh();
                break;

            case MasterTool.DrawPath:
                StartPathDrawing(imagePoint);
                MapView.CaptureMouse();
                e.Handled = true;
                break;

            case MasterTool.DrawRegion:
                StartRegionDrawing(canvasPoint);
                MapView.CaptureMouse();
                e.Handled = true;
                break;

            case MasterTool.Navigate:
                var hitTarget = MapView.HitTestTarget(canvasPoint);
                if (hitTarget is not null)
                {
                    _session.SelectTarget(hitTarget.Id);
                    MapView.Refresh();
                    UpdateStatus($"Выбрана цель: {hitTarget.Label}");
                }
                break;
        }
    }

    private void MapView_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDrawingPath && e.LeftButton == MouseButtonState.Pressed)
        {
            var imagePoint = MapView.CanvasToImage(e.GetPosition(MapView));
            if (!IsPointOnMap(imagePoint))
                return;

            if (_pathPointsImage.Count == 0 ||
                Distance(_pathPointsImage[^1], imagePoint) > 4)
            {
                _pathPointsImage.Add(imagePoint);
                _session.MovementPath = BuildPathWithEndpoints(_pathPointsImage);
                MapView.Refresh();
            }
        }
        else if (_isDrawingRegion && e.LeftButton == MouseButtonState.Pressed && _regionPreview is not null)
        {
            var current = e.GetPosition(MapView.OverlayCanvasElement);
            UpdateRegionPreview(_regionStartCanvas, current);
        }
    }

    private void MapView_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDrawingPath)
        {
            FinishPathDrawing();
            MapView.ReleaseMouseCapture();
            e.Handled = true;
        }
        else if (_isDrawingRegion)
        {
            FinishRegionDrawing(e.GetPosition(MapView));
            MapView.ReleaseMouseCapture();
            e.Handled = true;
        }
    }

    private void StartPathDrawing(Point imagePoint)
    {
        if (!_session.HasPartyMarker)
        {
            UpdateStatus("Сначала установите метку партии.");
            return;
        }

        if (!_session.HasSelectedTarget)
        {
            UpdateStatus("Выберите или добавьте метку цели (клик в режиме «Обзор»).");
            return;
        }

        _isDrawingPath = true;
        _pathPointsImage.Clear();
        _pathPointsImage.Add(_session.PartyPosition!.Value);
        _pathPointsImage.Add(imagePoint);
        _session.MovementPath = BuildPathWithEndpoints(_pathPointsImage);
        MapView.Refresh();
    }

    private void FinishPathDrawing()
    {
        _isDrawingPath = false;
        if (_session.HasSelectedTarget && _pathPointsImage.Count > 0)
        {
            _pathPointsImage[^1] = _session.SelectedTarget!.Position;
            _session.MovementPath = BuildPathWithEndpoints(_pathPointsImage);
        }

        MapView.Refresh();
        UpdateStatus("Маршрут сохранён. На экране игры нажмите «Движение».");
    }

    private List<Point> BuildPathWithEndpoints(List<Point> stroke)
    {
        if (stroke.Count == 0)
            return [];

        var result = new List<Point>(stroke);
        if (_session.PartyPosition is { } party)
            result[0] = party;
        if (_session.SelectedTarget is { } target)
            result[^1] = target.Position;
        return result;
    }

    private void StartRegionDrawing(Point canvasStart)
    {
        _isDrawingRegion = true;
        _regionStartCanvas = canvasStart;
        _regionPreview = new Rectangle
        {
            Stroke = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(139, 105, 20)),
            StrokeThickness = 2,
            StrokeDashArray = [4, 2],
            Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(50, 201, 168, 108))
        };
        Canvas.SetLeft(_regionPreview, canvasStart.X);
        Canvas.SetTop(_regionPreview, canvasStart.Y);
        MapView.OverlayCanvasElement.Children.Add(_regionPreview);
    }

    private void UpdateRegionPreview(Point start, Point end)
    {
        if (_regionPreview is null)
            return;

        var x = Math.Min(start.X, end.X);
        var y = Math.Min(start.Y, end.Y);
        var w = Math.Abs(end.X - start.X);
        var h = Math.Abs(end.Y - start.Y);
        Canvas.SetLeft(_regionPreview, x);
        Canvas.SetTop(_regionPreview, y);
        _regionPreview.Width = Math.Max(1, w);
        _regionPreview.Height = Math.Max(1, h);
    }

    private void FinishRegionDrawing(Point canvasEnd)
    {
        _isDrawingRegion = false;
        if (_regionPreview is not null)
        {
            MapView.OverlayCanvasElement.Children.Remove(_regionPreview);
            _regionPreview = null;
        }

        var x = Math.Min(_regionStartCanvas.X, canvasEnd.X);
        var y = Math.Min(_regionStartCanvas.Y, canvasEnd.Y);
        var w = Math.Abs(canvasEnd.X - _regionStartCanvas.X);
        var h = Math.Abs(canvasEnd.Y - _regionStartCanvas.Y);

        if (w < 10 || h < 10)
        {
            UpdateStatus("Область слишком мала — выделите больший прямоугольник.");
            MapView.Refresh();
            return;
        }

        var canvasRect = new Rect(x, y, w, h);
        var imageRect = MapView.Viewport.CanvasToImage(canvasRect);

        var dialog = new RegionTextDialog("Описание земель", string.Empty) { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            MapView.Refresh();
            return;
        }

        _session.Regions.Add(new MapRegion
        {
            Bounds = imageRect,
            Title = dialog.RegionTitle,
            Description = dialog.RegionDescription
        });
        MapView.Refresh();
        UpdateStatus("Область с описанием добавлена.");
    }

    private void FinishTransientDrawing()
    {
        _isDrawingPath = false;
        _isDrawingRegion = false;
        if (_regionPreview is not null)
        {
            MapView.OverlayCanvasElement.Children.Remove(_regionPreview);
            _regionPreview = null;
        }
    }

    private bool IsPointOnMap(Point imagePoint)
    {
        if (_session.MapImage is null)
            return false;
        return imagePoint.X >= 0 && imagePoint.Y >= 0 &&
               imagePoint.X <= _session.MapImage.PixelWidth &&
               imagePoint.Y <= _session.MapImage.PixelHeight;
    }

    private static double Distance(Point a, Point b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private void UpdateStatus(string? message = null)
    {
        if (message is not null)
        {
            StatusText.Text = message;
            return;
        }

        StatusText.Text = _currentTool switch
        {
            MasterTool.Navigate => "Обзор: клик по крестику цели — выбрать активную цель для маршрута.",
            MasterTool.PartyMarker => "Кликните на карте, чтобы поставить метку партии (синий щит).",
            MasterTool.TargetMarker => "Кликните, чтобы добавить метку цели (красный крестик).",
            MasterTool.DrawPath => "Зажмите ЛКМ и ведите кривую от партии к выбранной цели.",
            MasterTool.DrawRegion => "Выделите прямоугольник — откроется окно для текста справки.",
            _ => string.Empty
        };
    }
}
