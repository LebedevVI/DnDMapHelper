using System.Collections.ObjectModel;
using System.Collections.Specialized;
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
    private List<Point> _draftPath = [];
    private bool _isPartyMoving;
    private double _partyPathProgress;
    private Point? _partyDisplayPosition;
    private int _selectedRouteIndex = -1;
    private IReadOnlyList<Point>? _activeMovementPath;

    public GameSession()
    {
        Routes.CollectionChanged += OnRoutesCollectionChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<MovementRoute> Routes { get; } = [];

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

    private Guid? _selectedRegionId;

    public Guid? SelectedRegionId
    {
        get => _selectedRegionId;
        set
        {
            _selectedRegionId = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedRegion));
            OnPropertyChanged(nameof(HasSelectedRegion));
            NotifyRegionsChanged();
        }
    }

    public bool HasSelectedRegion =>
        SelectedRegionId.HasValue && Regions.Any(r => r.Id == SelectedRegionId.Value);

    public MapRegion? SelectedRegion =>
        SelectedRegionId.HasValue
            ? Regions.FirstOrDefault(r => r.Id == SelectedRegionId.Value)
            : null;

    /// <summary>Черновик маршрута при рисовании на экране мастера.</summary>
    public IReadOnlyList<Point> DraftPath
    {
        get => _draftPath;
        set
        {
            _draftPath = value.ToList();
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasDraftPath));
        }
    }

    public bool HasDraftPath => DraftPath.Count >= 2;

    public int SelectedRouteIndex
    {
        get => _selectedRouteIndex;
        set
        {
            if (Routes.Count == 0)
            {
                if (_selectedRouteIndex == -1)
                    return;
                _selectedRouteIndex = -1;
            }
            else
            {
                var clamped = Math.Clamp(value, 0, Routes.Count - 1);
                if (_selectedRouteIndex == clamped)
                    return;
                _selectedRouteIndex = clamped;
            }

            OnPropertyChanged();
            NotifyRoutesChanged();
        }
    }

    public MovementRoute? SelectedRoute =>
        SelectedRouteIndex >= 0 && SelectedRouteIndex < Routes.Count
            ? Routes[SelectedRouteIndex]
            : null;

    /// <summary>Следующий маршрут для движения на экране игры (первый в очереди).</summary>
    public MovementRoute? ActiveRoute => Routes.Count > 0 ? Routes[0] : null;

    public bool HasRoutes => Routes.Count > 0;

    public IReadOnlyList<Point> ActiveMovementPath =>
        _activeMovementPath ?? ActiveRoute?.Points ?? [];

    public void SelectTarget(Guid id)
    {
        SelectedTargetId = id;
        if (SelectedRegionId.HasValue)
            SelectedRegionId = null;
    }

    public void SelectRegion(Guid id)
    {
        SelectedRegionId = id;
        SelectedTargetId = null;
    }

    public bool RemoveRegion(Guid regionId)
    {
        var region = Regions.FirstOrDefault(r => r.Id == regionId);
        if (region is null)
            return false;

        Regions.Remove(region);
        if (SelectedRegionId == regionId)
            SelectedRegionId = null;

        NotifyRegionsChanged();
        return true;
    }

    public bool RemoveTarget(Guid targetId)
    {
        var target = Targets.FirstOrDefault(t => t.Id == targetId);
        if (target is null)
            return false;

        Targets.Remove(target);

        for (var i = Routes.Count - 1; i >= 0; i--)
        {
            if (Routes[i].TargetId == targetId)
                Routes.RemoveAt(i);
        }

        RenumberRoutes();

        if (SelectedTargetId == targetId)
            SelectedTargetId = Targets.Count > 0 ? Targets[0].Id : null;

        if (Routes.Count == 0)
            SelectedRouteIndex = -1;
        else if (SelectedRouteIndex >= Routes.Count)
            SelectedRouteIndex = Routes.Count - 1;

        NotifyTargetsChanged();
        NotifyRoutesChanged();
        return true;
    }

    public Point GetNextRouteStartPoint()
    {
        if (Routes.Count > 0)
            return Routes[^1].EndPoint;
        return PartyPosition ?? default;
    }

    public void AddRoute(MovementRoute route)
    {
        route.Order = Routes.Count + 1;
        Routes.Add(route);
        SelectedRouteIndex = Routes.Count - 1;
        DraftPath = [];
        NotifyRoutesChanged();
    }

    public void RemoveRouteAt(int index)
    {
        if (index < 0 || index >= Routes.Count)
            return;

        Routes.RemoveAt(index);
        RenumberRoutes();
        if (Routes.Count == 0)
            SelectedRouteIndex = -1;
        else if (SelectedRouteIndex >= Routes.Count)
            SelectedRouteIndex = Routes.Count - 1;
        NotifyRoutesChanged();
    }

    public void ClearAllRoutes()
    {
        Routes.Clear();
        DraftPath = [];
        SelectedRouteIndex = -1;
        _activeMovementPath = null;
        ResetPartyMovement();
        NotifyRoutesChanged();
    }

    public void ResetPartyMovement()
    {
        IsPartyMoving = false;
        _partyPathProgress = 0;
        _activeMovementPath = null;
        PartyDisplayPosition = PartyPosition;
    }

    public bool CanStartMovement() =>
        HasPartyMarker && ActiveRoute is not null && ActiveRoute.Points.Count >= 2 && !IsPartyMoving;

    public bool IsPartyMoving
    {
        get => _isPartyMoving;
        private set { _isPartyMoving = value; OnPropertyChanged(); }
    }

    public void BeginPartyMovement()
    {
        if (!CanStartMovement())
            return;

        _activeMovementPath = ActiveRoute!.Points.ToList();
        IsPartyMoving = true;
        _partyPathProgress = 0;
        PartyDisplayPosition = _activeMovementPath[0];
    }

    public void UpdatePartyMovement(double progress)
    {
        var path = _activeMovementPath;
        if (!IsPartyMoving || path is null || path.Count < 2)
            return;

        _partyPathProgress = Math.Clamp(progress, 0, 1);
        PartyDisplayPosition = Helpers.PathGeometryHelper.GetPointOnSmoothPath(path, _partyPathProgress);

        if (_partyPathProgress >= 1)
        {
            PartyPosition = Helpers.PathGeometryHelper.GetPointOnSmoothPath(path, 1);
            IsPartyMoving = false;
            _partyPathProgress = 0;
            _activeMovementPath = null;
            CompleteActiveRoute();
        }
    }

    private void CompleteActiveRoute()
    {
        if (Routes.Count == 0)
            return;

        Routes.RemoveAt(0);
        RenumberRoutes();

        if (Routes.Count == 0)
            SelectedRouteIndex = -1;
        else if (SelectedRouteIndex < 0)
            SelectedRouteIndex = 0;

        NotifyRoutesChanged();
    }

    private void RenumberRoutes()
    {
        for (var i = 0; i < Routes.Count; i++)
            Routes[i].Order = i + 1;
    }

    private void OnRoutesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        NotifyRoutesChanged();

    public void NotifyRoutesChanged()
    {
        OnPropertyChanged(nameof(Routes));
        OnPropertyChanged(nameof(HasRoutes));
        OnPropertyChanged(nameof(ActiveRoute));
        OnPropertyChanged(nameof(SelectedRoute));
        OnPropertyChanged(nameof(SelectedRouteIndex));
    }

    public void NotifyTargetsChanged() => OnPropertyChanged(nameof(Targets));

    public void NotifyRegionsChanged() => OnPropertyChanged(nameof(Regions));

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
