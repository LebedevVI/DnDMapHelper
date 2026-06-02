using System.Windows;
using System.Windows.Input;
using DnDMapHelper.Models;
using DnDMapHelper.Services;

namespace DnDMapHelper.Views;

public partial class PlayerWindow : Window
{
    private readonly GameSession _session = GameSession.Current;
    private readonly PartyMovementController _movement = PartyMovementController.Current;
    private bool _isFullscreen = true;

    private const double CompactWidth = 720;
    private const double CompactHeight = 480;

    public PlayerWindow()
    {
        InitializeComponent();

        _movement.MovementFrame += OnMovementFrame;
        _session.PropertyChanged += OnSessionPropertyChanged;

        UpdateNoMapHint();
        UpdateDisplayModeButtons();
    }

    public bool IsFullscreen => _isFullscreen;

    public void SetFullscreen(bool fullscreen)
    {
        _isFullscreen = fullscreen;

        if (fullscreen)
        {
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            WindowState = WindowState.Maximized;
        }
        else
        {
            WindowStyle = WindowStyle.SingleBorderWindow;
            ResizeMode = ResizeMode.CanResize;
            WindowState = WindowState.Normal;
            Width = CompactWidth;
            Height = CompactHeight;
            PositionBesideOwner();
        }

        UpdateDisplayModeButtons();
    }

    public void ToggleDisplayMode() => SetFullscreen(!_isFullscreen);

    private void PositionBesideOwner()
    {
        if (Owner is not Window owner)
            return;

        Left = owner.Left + owner.Width + 8;
        Top = owner.Top;

        if (Left + Width > SystemParameters.VirtualScreenWidth)
            Left = Math.Max(0, owner.Left - Width - 8);
        if (Top + Height > SystemParameters.VirtualScreenHeight)
            Top = Math.Max(0, SystemParameters.VirtualScreenHeight - Height);
    }

    private void OnSessionPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(GameSession.MapImage) or nameof(GameSession.HasMap))
            UpdateNoMapHint();
        if (e.PropertyName is nameof(GameSession.Regions) or null)
            RefreshMap();
    }

    public void RefreshMap() => MapView.Refresh();

    private void OnMovementFrame() => RefreshMap();

    private void UpdateNoMapHint() =>
        NoMapHint.Visibility = _session.HasMap ? Visibility.Collapsed : Visibility.Visible;

    private void UpdateDisplayModeButtons()
    {
        CompactModeButton.Visibility = _isFullscreen ? Visibility.Visible : Visibility.Collapsed;
        FullscreenModeButton.Visibility = _isFullscreen ? Visibility.Collapsed : Visibility.Visible;
    }

    private void CompactModeButton_Click(object sender, RoutedEventArgs e) => SetFullscreen(false);

    private void FullscreenModeButton_Click(object sender, RoutedEventArgs e) => SetFullscreen(true);

    private void MapView_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_session.IsPartyMoving)
            return;

        var canvasPoint = MapView.GetViewPoint(e);
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

    public void ShowEncounterPopup(string title, string description)
    {
        if (!IsLoaded)
            return;

        var popup = new ScrollPopupWindow(title, description, this);
        popup.ShowDialog();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (MapView.TryHandlePanKey(e.Key))
        {
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
            Close();
        if (e.Key == Key.F11)
            ToggleDisplayMode();
    }

    protected override void OnClosed(EventArgs e)
    {
        _movement.MovementFrame -= OnMovementFrame;
        _movement.StopRenderLoop();
        _session.ResetPartyMovement();
        base.OnClosed(e);
    }
}
