using System.Windows.Media;
using DnDMapHelper.Helpers;

namespace DnDMapHelper.Services;

public sealed class PartyMovementController
{
    private static readonly Lazy<PartyMovementController> Instance = new(() => new());
    public static PartyMovementController Current => Instance.Value;

    private readonly GameSession _session = GameSession.Current;
    private EventHandler? _renderHandler;
    private DateTime _moveStartTime;
    private double _moveDurationSeconds = 3;

    private const double MinMoveDurationSeconds = 2.5;
    private const double MaxMoveDurationSeconds = 7;
    private const double PixelsPerSecond = 90;

    public event Action? MovementFrame;
    public event Action? MovementStateChanged;

    public bool CanStart => _session.CanStartMovement();

    public string GetMoveButtonLabel()
    {
        var count = _session.Routes.Count;
        return count switch
        {
            0 => "⚔ Движение",
            1 => "⚔ Движение",
            _ => $"⚔ Движение (1 из {count})"
        };
    }

    public void TryStartMovement()
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
        MovementStateChanged?.Invoke();

        StopRenderLoop();
        _renderHandler = OnRendering;
        CompositionTarget.Rendering += _renderHandler;
    }

    public void StopRenderLoop()
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
            MovementStateChanged?.Invoke();
            return;
        }

        var elapsed = (DateTime.UtcNow - _moveStartTime).TotalSeconds;
        var linearProgress = Math.Min(1.0, elapsed / _moveDurationSeconds);
        var easedProgress = PathGeometryHelper.EaseInOutCubic(linearProgress);

        _session.UpdatePartyMovement(easedProgress);
        MovementFrame?.Invoke();
    }
}
