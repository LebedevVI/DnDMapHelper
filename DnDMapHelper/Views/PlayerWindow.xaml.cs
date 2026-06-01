using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using DnDMapHelper.Helpers;
using DnDMapHelper.Models;
using DnDMapHelper.Services;

namespace DnDMapHelper.Views;

public partial class PlayerWindow : Window
{
    private readonly GameSession _session = GameSession.Current;
    private EventHandler? _renderHandler;
    private DateTime _moveStartTime;
    private double _moveDurationSeconds = 3;

    private const double MinMoveDurationSeconds = 2.5;
    private const double MaxMoveDurationSeconds = 7;
    private const double PixelsPerSecond = 90;

    public PlayerWindow()
    {
        InitializeComponent();

        _session.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(GameSession.MapImage) or nameof(GameSession.HasMap))
                UpdateNoMapHint();
            if (e.PropertyName is nameof(GameSession.IsPartyMoving)
                or nameof(GameSession.CanStartMovement)
                or nameof(GameSession.Routes)
                or nameof(GameSession.HasRoutes)
                or nameof(GameSession.ActiveRoute))
                UpdateMoveButton();
        };

        UpdateNoMapHint();
        UpdateMoveButton();
    }

    private void UpdateNoMapHint() =>
        NoMapHint.Visibility = _session.HasMap ? Visibility.Collapsed : Visibility.Visible;

    private void UpdateMoveButton()
    {
        MoveButton.IsEnabled = _session.CanStartMovement();
        var count = _session.Routes.Count;
        MoveButton.Content = count switch
        {
            0 => "⚔ Движение",
            1 => "⚔ Движение",
            _ => $"⚔ Движение (1 из {count})"
        };
    }

    private void MoveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_session.CanStartMovement())
            return;

        var pathLength = PathGeometryHelper.GetSmoothPathLength(_session.ActiveMovementPath);
        _moveDurationSeconds = Math.Clamp(
            pathLength / PixelsPerSecond,
            MinMoveDurationSeconds,
            MaxMoveDurationSeconds);

        _session.BeginPartyMovement();
        _moveStartTime = DateTime.UtcNow;
        MoveButton.IsEnabled = false;
        StartRenderLoop();
    }

    private void StartRenderLoop()
    {
        StopRenderLoop();
        _renderHandler = OnRendering;
        CompositionTarget.Rendering += _renderHandler;
    }

    private void StopRenderLoop()
    {
        if (_renderHandler is null)
            return;

        CompositionTarget.Rendering -= _renderHandler;
        _renderHandler = null;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (!_session.IsPartyMoving)
        {
            StopRenderLoop();
            MapView.Refresh();
            UpdateMoveButton();
            return;
        }

        var elapsed = (DateTime.UtcNow - _moveStartTime).TotalSeconds;
        var linearProgress = Math.Min(1.0, elapsed / _moveDurationSeconds);
        var easedProgress = PathGeometryHelper.EaseInOutCubic(linearProgress);

        _session.UpdatePartyMovement(easedProgress);
        MapView.Refresh();
    }

    private void MapView_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_session.IsPartyMoving)
            return;

        var canvasPoint = e.GetPosition(MapView);
        var region = MapView.HitTestRegion(canvasPoint);
        if (region is null)
            return;

        e.Handled = true;
        ShowRegionScroll(region);
    }

    private void ShowRegionScroll(MapRegion region)
    {
        var popup = new ScrollPopupWindow(region.Title, region.Description, this);
        popup.ShowDialog();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            Close();
        if (e.Key == Key.F11)
            WindowStyle = WindowStyle == WindowStyle.None ? WindowStyle.SingleBorderWindow : WindowStyle.None;
    }

    protected override void OnClosed(EventArgs e)
    {
        StopRenderLoop();
        _session.ResetPartyMovement();
        base.OnClosed(e);
    }
}
