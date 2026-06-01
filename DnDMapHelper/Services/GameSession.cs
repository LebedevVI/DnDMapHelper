using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media.Imaging;
using DnDMapHelper.Models;

namespace DnDMapHelper.Services;

public sealed class GameSession : INotifyPropertyChanged
{
    private static readonly Lazy<GameSession> Instance = new(() => new GameSession());
    public static GameSession Current => Instance.Value;

    private BitmapImage? _mapImage;
    private Point? _partyPosition;
    private Guid? _selectedTargetId;
    private List<Point> _movementPath = [];
    private bool _isPartyMoving;
    private double _partyPathProgress;
    private Point? _partyDisplayPosition;

    public event PropertyChangedEventHandler? PropertyChanged;

    public BitmapImage? MapImage
    {
        get => _mapImage;
        set { _mapImage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasMap)); }
    }

    public bool HasMap => MapImage is not null;

    public Point? PartyPosition
    {
        get => _partyPosition;
        set
        {
            _partyPosition = value;
            if (!_isPartyMoving)
                PartyDisplayPosition = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasPartyMarker));
        }
    }

    public Point? PartyDisplayPosition
    {
        get => _partyDisplayPosition ?? _partyPosition;
        private set { _partyDisplayPosition = value; OnPropertyChanged(); }
    }

    public bool HasPartyMarker => PartyPosition.HasValue;

    public ObservableCollection<TargetMarker> Targets { get; } = [];

    public ObservableCollection<MapRegion> Regions { get; } = [];

    public Guid? SelectedTargetId
    {
        get => _selectedTargetId;
        set { _selectedTargetId = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasSelectedTarget)); }
    }

    public bool HasSelectedTarget =>
        SelectedTargetId.HasValue && Targets.Any(t => t.Id == SelectedTargetId.Value);

    public TargetMarker? SelectedTarget =>
        SelectedTargetId.HasValue
            ? Targets.FirstOrDefault(t => t.Id == SelectedTargetId.Value)
            : null;

    public IReadOnlyList<Point> MovementPath
    {
        get => _movementPath;
        set
        {
            _movementPath = value.ToList();
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasMovementPath));
        }
    }

    public bool HasMovementPath => MovementPath.Count >= 2;

    public bool IsPartyMoving
    {
        get => _isPartyMoving;
        private set { _isPartyMoving = value; OnPropertyChanged(); }
    }

    public void SelectTarget(Guid id) => SelectedTargetId = id;

    public void ClearPath()
    {
        MovementPath = [];
        ResetPartyMovement();
    }

    public void ResetPartyMovement()
    {
        IsPartyMoving = false;
        _partyPathProgress = 0;
        PartyDisplayPosition = PartyPosition;
    }

    public bool CanStartMovement() =>
        HasPartyMarker && HasSelectedTarget && HasMovementPath && !IsPartyMoving;

    public void BeginPartyMovement()
    {
        if (!CanStartMovement())
            return;

        IsPartyMoving = true;
        _partyPathProgress = 0;
        PartyDisplayPosition = MovementPath[0];
    }

    public void UpdatePartyMovement(double progress)
    {
        if (!IsPartyMoving || MovementPath.Count < 2)
            return;

        _partyPathProgress = Math.Clamp(progress, 0, 1);
        PartyDisplayPosition = Helpers.PathGeometryHelper.GetPointOnSmoothPath(
            MovementPath, _partyPathProgress);

        if (_partyPathProgress >= 1)
        {
            var target = SelectedTarget;
            if (target is not null)
                PartyPosition = target.Position;
            IsPartyMoving = false;
            _partyPathProgress = 0;
        }
    }

    public void NotifyTargetsChanged() => OnPropertyChanged(nameof(Targets));

    public void NotifyRegionsChanged() => OnPropertyChanged(nameof(Regions));

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
