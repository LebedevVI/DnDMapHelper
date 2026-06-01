using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using DnDMapHelper.Models;
using DnDMapHelper.Services;

namespace DnDMapHelper.Views;

public partial class PlayerWindow : Window
{
    private readonly GameSession _session = GameSession.Current;
    private readonly DispatcherTimer _moveTimer;
    private const double MoveDurationSeconds = 2.5;
    private DateTime _moveStartTime;

    public PlayerWindow()
    {
        InitializeComponent();
        _moveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _moveTimer.Tick += MoveTimer_Tick;

        _session.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(GameSession.MapImage) or nameof(GameSession.HasMap))
                UpdateNoMapHint();
            if (e.PropertyName is nameof(GameSession.IsPartyMoving) or nameof(GameSession.CanStartMovement))
                UpdateMoveButton();
        };

        UpdateNoMapHint();
        UpdateMoveButton();
    }

    private void UpdateNoMapHint() =>
        NoMapHint.Visibility = _session.HasMap ? Visibility.Collapsed : Visibility.Visible;

    private void UpdateMoveButton() =>
        MoveButton.IsEnabled = _session.CanStartMovement();

    private void MoveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_session.CanStartMovement())
            return;

        _session.BeginPartyMovement();
        _moveStartTime = DateTime.UtcNow;
        MoveButton.IsEnabled = false;
        _moveTimer.Start();
    }

    private void MoveTimer_Tick(object? sender, EventArgs e)
    {
        var elapsed = (DateTime.UtcNow - _moveStartTime).TotalSeconds;
        var progress = elapsed / MoveDurationSeconds;
        _session.UpdatePartyMovement(progress);
        MapView.Refresh();

        if (!_session.IsPartyMoving)
        {
            _moveTimer.Stop();
            MapView.Refresh();
            UpdateMoveButton();
        }
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
        _moveTimer.Stop();
        _session.ResetPartyMovement();
        base.OnClosed(e);
    }
}
