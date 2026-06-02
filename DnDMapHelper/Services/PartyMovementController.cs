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
    private double _moveDurationSeconds = 12;
    private double _progressBase;
    private double _progressSpan = 1;

    public event Action? MovementFrame;
    public event Action? MovementStateChanged;

    public bool CanStart => _session.CanStartMovement();

    public string GetMoveButtonLabel()
    {
        if (_session.HasPendingEncounter)
            return "⚔ Продолжить движение";

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

        if (_session.HasPendingEncounter)
        {
            var startProgress = _session.GetCurrentMovementProgress();
            _progressBase = startProgress;
            _progressSpan = Math.Max(0.001, 1 - startProgress);
            _moveDurationSeconds = Math.Max(0.4, PathGeometryHelper.CalculateMovementDurationSeconds(
                PathGeometryHelper.GetSmoothPathLength(_session.ActiveMovementPath)) * _progressSpan);
            if (!_session.TryResumeAfterEncounter())
                return;
        }
        else
        {
            _progressBase = 0;
            _progressSpan = 1;
            var pathLength = PathGeometryHelper.GetSmoothPathLength(_session.ActiveMovementPath);
            _moveDurationSeconds = PathGeometryHelper.CalculateMovementDurationSeconds(pathLength);
            _session.BeginPartyMovement();
        }

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
        var routeProgress = _progressBase + easedProgress * _progressSpan;
        _session.UpdatePartyMovement(routeProgress);
        MovementFrame?.Invoke();
    }
}
