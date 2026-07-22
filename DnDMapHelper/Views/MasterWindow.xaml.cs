using System.Windows;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using DnDMapHelper.Helpers;
using DnDMapHelper.Models;
using DnDMapHelper.Models.Persistence;
using DnDMapHelper.Services;
using Microsoft.Win32;

namespace DnDMapHelper.Views;

public partial class MasterWindow : Window
{
    private readonly GameSession _session = GameSession.Current;
    private MasterTool _currentTool = MasterTool.Navigate;
    private bool _isDrawingPath;
    private bool _isDrawingRegion;
    private readonly List<Point> _pathPointsImage = [];
    private readonly List<Point> _regionPointsImage = [];
    private PlayerWindow? _playerWindow;
    private readonly PartyMovementController _movement = PartyMovementController.Current;
    private string? _sessionFilePath;

    public MasterWindow()
    {
        InitializeComponent();
        ToolNavigate.IsChecked = true;
        RouteList.ItemsSource = _session.Routes;
        QuestJournal.StatusMessageRequested += msg => UpdateStatus(msg);
        _session.Routes.CollectionChanged += (_, _) => Dispatcher.BeginInvoke(SyncRouteList);

        _movement.MovementStateChanged += UpdateMoveButton;
        _session.EncounterTriggered += OnEncounterTriggered;

        UpdateStatus();
        UpdateRouteQueueHint();
        UpdateMoveButton();
        _session.PropertyChanged += (_, e) =>
        {
            // PartyDisplayPosition updates every animation frame — skip status rebuild.
            if (e.PropertyName is not nameof(GameSession.PartyDisplayPosition))
                UpdateStatus();

            if (e.PropertyName is nameof(GameSession.Routes) or nameof(GameSession.SelectedRouteIndex))
                UpdateRouteQueueHint();
            if (e.PropertyName is nameof(GameSession.Routes)
                or nameof(GameSession.HasRoutes)
                or nameof(GameSession.ActiveRoute)
                or nameof(GameSession.IsPartyMoving)
                or nameof(GameSession.HasPausedMovement)
                or nameof(GameSession.CanStartMovement))
                UpdateMoveButton();
            if (e.PropertyName is nameof(GameSession.Quests) or nameof(GameSession.SelectedQuestId))
            {
                MapView.Refresh();
                _playerWindow?.RefreshMap();
            }
        };
    }

    private void MoveButton_Click(object sender, RoutedEventArgs e) =>
        _movement.TryToggleMovement();

    private void UpdateMoveButton()
    {
        MoveButton.IsEnabled = _movement.CanUseMoveButton;
        MoveButton.Content = _movement.GetMoveButtonLabel();
    }

    private void ZoomIn_Click(object sender, RoutedEventArgs e) => MapView.ZoomIn();

    private void ZoomOut_Click(object sender, RoutedEventArgs e) => MapView.ZoomOut();

    private void ResetZoom_Click(object sender, RoutedEventArgs e) => MapView.ResetZoom();

    private void LoadMap_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmDiscardSession("Загрузить новое изображение карты? Текущие метки и квесты будут удалены."))
            return;

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

        ClearSessionState();
        _sessionFilePath = null;
        _session.MapImage = image;
        RefreshAfterSessionChange();
        UpdateStatus("Карта загружена. Поставьте «Партия» и «Цель» — или откройте «Квесты», когда будете готовы.");
    }

    private void OpenSession_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmDiscardSession("Открыть другой файл? Несохранённые изменения будут потеряны."))
            return;

        var dialog = new OpenFileDialog
        {
            Filter = SessionFileFormat.FileDialogFilter,
            Title = "Открыть карту приключений"
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            if (_session.IsPartyMoving)
                _movement.PauseMovement();

            SessionPersistenceService.Load(_session, dialog.FileName);
            _sessionFilePath = dialog.FileName;
            RefreshAfterSessionChange();
            UpdateStatus($"Открыто: {System.IO.Path.GetFileName(dialog.FileName)}");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Не удалось открыть файл",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveSession_Click(object sender, RoutedEventArgs e)
    {
        if (!_session.HasMap)
        {
            UpdateStatus("Сначала загрузите или откройте карту.");
            return;
        }

        if (_session.IsPartyMoving)
            _movement.PauseMovement();

        if (_sessionFilePath is not null)
        {
            try
            {
                SessionPersistenceService.Save(_session, _sessionFilePath);
                UpdateStatus($"Сохранено: {System.IO.Path.GetFileName(_sessionFilePath)}");
                return;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Не удалось сохранить",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
        }

        var dialog = new SaveFileDialog
        {
            Filter = SessionFileFormat.FileDialogFilter,
            Title = "Сохранить карту приключений",
            DefaultExt = SessionFileFormat.Extension.TrimStart('.'),
            FileName = "Карта приключений.dndmap"
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            SessionPersistenceService.Save(_session, dialog.FileName);
            _sessionFilePath = dialog.FileName;
            UpdateStatus($"Сохранено: {System.IO.Path.GetFileName(dialog.FileName)}");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Не удалось сохранить",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool ConfirmDiscardSession(string message)
    {
        if (!_session.HasMap && _session.Targets.Count == 0 && _session.Quests.Count == 0)
            return true;

        return MessageBox.Show(this, message, "Подтверждение",
                   MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
    }

    private void ClearSessionState()
    {
        _session.Targets.Clear();
        _session.Regions.Clear();
        _session.Encounters.Clear();
        _session.Quests.Clear();
        _session.PartyPosition = null;
        _session.ClearAllRoutes();
        _session.SelectedTargetId = null;
        _session.SelectedRegionId = null;
        _session.SelectedEncounterId = null;
        _session.SelectQuest(null);
    }

    private void RefreshAfterSessionChange()
    {
        SyncRouteList();
        MapView.ResetZoom();
        MapView.Refresh();
        _playerWindow?.RefreshMap();
        UpdateMoveButton();
        UpdateRouteQueueHint();
    }

    private void ClearRoutes_Click(object sender, RoutedEventArgs e)
    {
        _movement.StopRenderLoop();
        _session.ClearAllRoutes();
        ClearPathDraft();
        SyncRouteList();
        MapView.Refresh();
        UpdateMoveButton();
        UpdateStatus("Очередь маршрутов очищена.");
    }

    private void RemoveSelectedRoute_Click(object sender, RoutedEventArgs e)
    {
        if (RouteList.SelectedIndex < 0)
            return;

        var removingActive = RouteList.SelectedIndex == 0;
        if (removingActive)
            _movement.StopRenderLoop();

        _session.RemoveRouteAt(RouteList.SelectedIndex);
        SyncRouteList();
        MapView.Refresh();
        UpdateMoveButton();
        UpdateRouteQueueHint();
    }

    private void RouteList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RouteList.SelectedIndex >= 0)
            _session.SelectedRouteIndex = RouteList.SelectedIndex;

        MapView.Refresh();
        UpdateRouteQueueHint();
    }

    private void SyncRouteList()
    {
        RouteList.ItemsSource = null;
        RouteList.ItemsSource = _session.Routes;

        if (_session.Routes.Count == 0)
        {
            RouteList.SelectedIndex = -1;
        }
        else if (RouteList.SelectedIndex < 0 || RouteList.SelectedIndex >= _session.Routes.Count)
        {
            RouteList.SelectedIndex = _session.SelectedRouteIndex >= 0
                ? _session.SelectedRouteIndex
                : 0;
        }

        UpdateRouteQueueHint();
    }

    private void ToggleQuestJournal_Checked(object sender, RoutedEventArgs e)
    {
        QuestJournalPanel.Visibility = ToggleQuestJournal.IsChecked == true
            ? Visibility.Visible
            : Visibility.Collapsed;
        UpdateStatus();
    }

    private void ToggleRouteJournal_Checked(object sender, RoutedEventArgs e) =>
        RouteJournalPanel.Visibility = ToggleRouteJournal.IsChecked == true
            ? Visibility.Visible
            : Visibility.Collapsed;

    private void UpdateRouteQueueHint()
    {
        if (_session.Routes.Count == 0)
        {
            RouteQueueHint.Text = "Маршруты появятся после рисования.";
            return;
        }

        var active = _session.ActiveRoute;
        var selected = RouteList.SelectedIndex;
        if (selected == 0 && active is not null)
            RouteQueueHint.Text = $"Следующий для игроков: {active.DisplayName}";
        else if (selected >= 0 && selected < _session.Routes.Count)
            RouteQueueHint.Text = $"Просмотр #{_session.Routes[selected].Order} (в очереди)";
        else
            RouteQueueHint.Text = $"В очереди: {_session.Routes.Count}";
    }

    private void HelpButton_Click(object sender, RoutedEventArgs e)
    {
        var help = new HelpWindow { Owner = this };
        help.ShowDialog();
    }

    private void AboutLink_Click(object sender, RoutedEventArgs e)
    {
        var about = new AboutWindow { Owner = this };
        about.ShowDialog();
    }

    private void OpenPlayer_Click(object sender, RoutedEventArgs e) => EnsurePlayerWindow(activateIfOpen: true);

    private void EnsurePlayerWindow(bool activateIfOpen = false)
    {
        if (_playerWindow is { IsLoaded: true })
        {
            if (activateIfOpen)
            {
                _playerWindow.Activate();
                if (_playerWindow.WindowState == WindowState.Minimized)
                    _playerWindow.WindowState = WindowState.Normal;
            }

            return;
        }

        _playerWindow = new PlayerWindow { Owner = this };
        _playerWindow.Closed += (_, _) =>
        {
            _playerWindow = null;
            UpdatePlayerWindowToggleButton();
        };
        _playerWindow.Show();
        UpdatePlayerWindowToggleButton();
    }

    private void TogglePlayerWindow_Click(object sender, RoutedEventArgs e)
    {
        if (_playerWindow is not { IsLoaded: true })
            return;

        _playerWindow.ToggleDisplayMode();
        UpdatePlayerWindowToggleButton();
    }

    private void UpdatePlayerWindowToggleButton()
    {
        if (_playerWindow is not { IsLoaded: true })
        {
            TogglePlayerWindowButton.IsEnabled = false;
            TogglePlayerWindowButton.Visibility = Visibility.Collapsed;
            return;
        }

        TogglePlayerWindowButton.IsEnabled = true;
        TogglePlayerWindowButton.Visibility = Visibility.Visible;
        TogglePlayerWindowButton.Content = _playerWindow.IsFullscreen
            ? "Свернуть игроков"
            : "На весь экран";
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
        foreach (var tool in new[] { ToolNavigate, ToolParty, ToolTarget, ToolEncounter, ToolPath, ToolRegion })
        {
            if (tool != active)
                tool.IsChecked = false;
        }

        if (ToolNavigate.IsChecked != true && ToolParty.IsChecked != true &&
            ToolTarget.IsChecked != true && ToolEncounter.IsChecked != true &&
            ToolPath.IsChecked != true &&
            ToolRegion.IsChecked != true)
            active.IsChecked = true;
    }

    private void MapView_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_session.MapImage is null)
        {
            UpdateStatus("Сначала загрузите карту.");
            return;
        }

        var canvasPoint = MapView.GetViewPoint(e);
        var imagePoint = MapView.CanvasToImage(canvasPoint);

        if (!IsPointOnMap(imagePoint))
            return;

        switch (_currentTool)
        {
            case MasterTool.PartyMarker:
                _session.PartyPosition = imagePoint;
                _session.ClearAllRoutes();
                _pathPointsImage.Clear();
                SyncRouteList();
                MapView.Refresh();
                break;

            case MasterTool.TargetMarker:
                var target = new TargetMarker { Position = imagePoint, Label = $"Цель {_session.Targets.Count + 1}" };
                _session.Targets.Add(target);
                _session.SelectTarget(target.Id);
                MapView.Refresh();
                EditTargetLabel(target);
                break;

            case MasterTool.EncounterMarker:
                AddEncounter(imagePoint);
                break;

            case MasterTool.DrawPath:
                if (!TryStartPathDrawing(imagePoint))
                    break;
                MapView.CaptureMouse();
                e.Handled = true;
                break;

            case MasterTool.DrawRegion:
                StartRegionDrawing(imagePoint);
                MapView.CaptureMouse();
                e.Handled = true;
                break;

            case MasterTool.Navigate:
                var hitTarget = MapView.HitTestTarget(canvasPoint);
                if (hitTarget is not null)
                {
                    _session.SelectTarget(hitTarget.Id);
                    MapView.Refresh();
                    UpdateStatus($"Выбрана цель: {hitTarget.Label}. Двойной клик — подпись, Delete — удалить.");
                    break;
                }

                var hitRegion = MapView.HitTestRegion(canvasPoint);
                if (hitRegion is not null)
                {
                    _session.SelectRegion(hitRegion.Id);
                    MapView.Refresh();
                    UpdateStatus($"Область: «{hitRegion.Title}». Двойной клик — текст и «Показывать игрокам»; Delete — удалить.");
                    break;
                }

                var hitEncounter = MapView.HitTestEncounter(canvasPoint);
                if (hitEncounter is not null)
                {
                    _session.SelectEncounter(hitEncounter.Id);
                    MapView.Refresh();
                    UpdateStatus($"Столкновение: «{hitEncounter.Title}». Двойной клик — описание, Delete — удалить.");
                }
                break;
        }
    }

    private void MapView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_session.MapImage is null)
            return;

        var canvasPoint = MapView.GetViewPoint(e);

        var hitRegion = MapView.HitTestRegion(canvasPoint);
        if (hitRegion is not null)
        {
            EditRegion(hitRegion);
            e.Handled = true;
            return;
        }

        var hitTarget = MapView.HitTestTarget(canvasPoint);
        if (hitTarget is not null)
        {
            EditTargetLabel(hitTarget);
            e.Handled = true;
            return;
        }

        var hitEncounter = MapView.HitTestEncounter(canvasPoint);
        if (hitEncounter is null)
            return;

        EditEncounter(hitEncounter);
        e.Handled = true;
    }

    private void EditRegion(MapRegion region)
    {
        var dialog = new RegionTextDialog(region.Title, region.Description, region.VisibleToPlayers,
            showPlayerVisibilityOption: true) { Owner = this };
        if (dialog.ShowDialog() != true)
            return;

        region.Title = dialog.RegionTitle;
        region.Description = dialog.RegionDescription;
        region.VisibleToPlayers = dialog.VisibleToPlayers;
        _session.SelectRegion(region.Id);
        _session.NotifyRegionsChanged();
        MapView.Refresh();
        _playerWindow?.RefreshMap();
        UpdateStatus($"Область обновлена: «{region.Title}».");
    }

    private void EditTargetLabel(TargetMarker target)
    {
        var dialog = new TargetLabelDialog(target.Label) { Owner = this };
        if (dialog.ShowDialog() != true)
            return;

        target.Label = dialog.Label;
        foreach (var route in _session.Routes.Where(r => r.TargetId == target.Id))
            route.TargetLabel = target.Label;
        _session.SelectTarget(target.Id);
        _session.NotifyRoutesChanged();
        MapView.Refresh();
        SyncRouteList();
        UpdateStatus($"Подпись цели: «{target.Label}»");
    }

    private void AddEncounter(Point imagePoint)
    {
        var dialog = new RegionTextDialog("Новое столкновение", string.Empty) { Owner = this, Title = "Боевое столкновение" };
        if (dialog.ShowDialog() != true)
            return;

        var encounter = new EncounterPoint
        {
            Position = imagePoint,
            Title = dialog.RegionTitle,
            Description = dialog.RegionDescription
        };
        _session.Encounters.Add(encounter);
        _session.SelectEncounter(encounter.Id);
        _session.NotifyEncountersChanged();
        MapView.Refresh();
        UpdateStatus($"Добавлено столкновение: «{encounter.Title}».");
    }

    private void EditEncounter(EncounterPoint encounter)
    {
        var dialog = new RegionTextDialog(encounter.Title, encounter.Description) { Owner = this, Title = "Боевое столкновение" };
        if (dialog.ShowDialog() != true)
            return;

        encounter.Title = dialog.RegionTitle;
        encounter.Description = dialog.RegionDescription;
        _session.SelectEncounter(encounter.Id);
        _session.NotifyEncountersChanged();
        MapView.Refresh();
        UpdateStatus($"Столкновение обновлено: «{encounter.Title}».");
    }

    private void MapView_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_session.MapImage is null)
            return;

        var canvasPoint = MapView.GetViewPoint(e);
        var hitTarget = MapView.HitTestTarget(canvasPoint);
        if (hitTarget is null)
            return;

        _session.SelectTarget(hitTarget.Id);
        MapView.Refresh();
        UpdateStatus($"Выбрана цель: {hitTarget.Label}. Двойной клик — подпись, Delete — удалить.");
        e.Handled = true;
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (MapView.TryHandlePanKey(e.Key))
        {
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Delete)
            return;

        var region = _session.SelectedRegion;
        if (region is not null)
        {
            TryDeleteRegion(region);
            e.Handled = true;
            return;
        }

        var target = _session.SelectedTarget;
        if (target is not null)
        {
            TryDeleteTarget(target);
            e.Handled = true;
            return;
        }

        var encounter = _session.SelectedEncounter;
        if (encounter is null)
            return;

        TryDeleteEncounter(encounter);
        e.Handled = true;
    }

    private void TryDeleteRegion(MapRegion region)
    {
        var questHint = QuestMapHelper.IsRegionLinkedToQuests(_session, region.Id)
            ? "\n\nОбласть указана в журнале квестов — проверьте записи после удаления."
            : string.Empty;

        if (MessageBox.Show(this, $"Удалить область «{region.Title}»?{questHint}", "Удаление области",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        if (!_session.RemoveRegion(region.Id))
            return;

        MapView.Refresh();
        UpdateStatus($"Область «{region.Title}» удалена.");
    }

    private void TryDeleteTarget(TargetMarker target)
    {
        var routesCount = _session.Routes.Count(r => r.TargetId == target.Id);
        var questTitles = QuestMapHelper.GetQuestsForTarget(_session, target.Id)
            .Select(q => q.Title)
            .ToList();

        var details = new List<string>();
        if (routesCount > 0)
            details.Add($"связанные маршруты ({routesCount})");
        if (questTitles.Count > 0)
            details.Add($"квесты: {string.Join(", ", questTitles)}");

        var suffix = details.Count > 0
            ? $"\n\nТакже затронуты: {string.Join("; ", details)}."
            : string.Empty;

        if (MessageBox.Show(this, $"Удалить цель «{target.Label}»?{suffix}", "Удаление цели",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        if (!_session.RemoveTarget(target.Id))
            return;

        SyncRouteList();
        MapView.Refresh();
        UpdateStatus($"Цель «{target.Label}» удалена.");
    }

    private void TryDeleteEncounter(EncounterPoint encounter)
    {
        if (MessageBox.Show(this, $"Удалить столкновение «{encounter.Title}»?", "Удаление столкновения",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        if (!_session.RemoveEncounter(encounter.Id))
            return;

        MapView.Refresh();
        UpdateStatus($"Столкновение «{encounter.Title}» удалено.");
    }

    private void MapView_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDrawingPath && e.LeftButton == MouseButtonState.Pressed)
        {
            var imagePoint = MapView.CanvasToImage(MapView.GetViewPoint(e));
            if (!IsPointOnMap(imagePoint))
                return;

            if (_pathPointsImage.Count == 0 ||
                Distance(_pathPointsImage[^1], imagePoint) > PathGeometryHelper.DefaultCaptureMinDistance)
            {
                _pathPointsImage.Add(imagePoint);
                _session.DraftPath = BuildSmoothedPathWithEndpoints(_pathPointsImage);
            }
        }
        else if (_isDrawingRegion && e.LeftButton == MouseButtonState.Pressed)
        {
            var imagePoint = MapView.CanvasToImage(MapView.GetViewPoint(e));
            if (!IsPointOnMap(imagePoint))
                return;

            if (_regionPointsImage.Count == 0 ||
                Distance(_regionPointsImage[^1], imagePoint) > PathGeometryHelper.DefaultCaptureMinDistance)
            {
                _regionPointsImage.Add(imagePoint);
                _session.DraftRegionOutline = RegionGeometryHelper.PrepareDraftOutline(_regionPointsImage);
            }
        }
    }

    private void MapView_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDrawingPath)
        {
            FinishPathDrawing();
            ReleaseMapMouseCapture();
            e.Handled = true;
        }
        else if (_isDrawingRegion)
        {
            FinishRegionDrawing();
            ReleaseMapMouseCapture();
            e.Handled = true;
        }
        else if (MapView.IsMouseCaptured)
        {
            ReleaseMapMouseCapture();
        }
    }

    private bool TryStartPathDrawing(Point imagePoint)
    {
        if (!_session.HasPartyMarker)
        {
            UpdateStatus("Сначала установите метку партии.");
            return false;
        }

        if (!_session.HasSelectedTarget)
        {
            UpdateStatus("Выберите цель в режиме «Обзор», затем снова нажмите «Путь».");
            return false;
        }

        _isDrawingPath = true;
        _pathPointsImage.Clear();
        _pathPointsImage.Add(_session.GetNextRouteStartPoint());
        _pathPointsImage.Add(imagePoint);
        _session.DraftPath = BuildSmoothedPathWithEndpoints(_pathPointsImage);
        return true;
    }

    private void FinishPathDrawing()
    {
        _isDrawingPath = false;
        if (!_session.HasSelectedTarget || _pathPointsImage.Count < 2)
        {
            ClearPathDraft();
            return;
        }

        _pathPointsImage[^1] = _session.SelectedTarget!.Position;
        var points = BuildSmoothedPathWithEndpoints(_pathPointsImage);
        if (points.Count < 2)
        {
            ClearPathDraft();
            return;
        }

        var target = _session.SelectedTarget!;
        _session.AddRoute(new MovementRoute
        {
            TargetId = target.Id,
            TargetLabel = target.Label,
            Points = points
        });

        ClearPathDraft();
        SyncRouteList();
        SwitchToNavigateTool();
        SelectNextTargetInOrder(target);

        if (_session.Targets.Count > 1 && _session.SelectedTarget is { } next)
            UpdateStatus($"Маршрут #{_session.Routes.Count} → «{target.Label}». Следующая цель: «{next.Label}».");
        else
            UpdateStatus($"Маршрут #{_session.Routes.Count} добавлен в очередь → «{target.Label}».");
    }

    private void SwitchToNavigateTool()
    {
        if (ToolNavigate.IsChecked != true)
            ToolNavigate.IsChecked = true;
        else
        {
            _currentTool = MasterTool.Navigate;
            UpdateStatus();
        }
    }

    private void SelectNextTargetInOrder(TargetMarker current)
    {
        if (_session.Targets.Count <= 1)
            return;

        var index = -1;
        for (var i = 0; i < _session.Targets.Count; i++)
        {
            if (_session.Targets[i].Id != current.Id)
                continue;
            index = i;
            break;
        }

        if (index < 0)
            return;

        var next = _session.Targets[(index + 1) % _session.Targets.Count];
        _session.SelectTarget(next.Id);
    }

    private List<Point> BuildSmoothedPathWithEndpoints(List<Point> stroke) =>
        BuildPathWithEndpoints(PathGeometryHelper.PrepareOpenPolyline(stroke));

    private List<Point> BuildPathWithEndpoints(List<Point> stroke)
    {
        if (stroke.Count == 0)
            return [];

        var result = new List<Point>(stroke);
        result[0] = _session.GetNextRouteStartPoint();
        if (_session.SelectedTarget is { } target)
            result[^1] = target.Position;
        return result;
    }

    private void StartRegionDrawing(Point imagePoint)
    {
        _isDrawingRegion = true;
        _regionPointsImage.Clear();
        _regionPointsImage.Add(imagePoint);
        _session.DraftRegionOutline = _regionPointsImage;
    }

    private void FinishRegionDrawing()
    {
        _isDrawingRegion = false;
        _session.DraftRegionOutline = [];

        if (_regionPointsImage.Count < 2)
        {
            _regionPointsImage.Clear();
            return;
        }

        var outline = RegionGeometryHelper.PrepareOutline(_regionPointsImage);
        _regionPointsImage.Clear();

        if (outline is null)
        {
            UpdateStatus("Область слишком мала — обведите больший контур.");
            return;
        }

        var dialog = new RegionTextDialog("Описание земель", string.Empty, showPlayerVisibilityOption: true)
        {
            Owner = this
        };
        if (dialog.ShowDialog() != true)
            return;

        var newRegion = new MapRegion
        {
            Outline = outline,
            Title = dialog.RegionTitle,
            Description = dialog.RegionDescription,
            VisibleToPlayers = dialog.VisibleToPlayers
        };
        _session.Regions.Add(newRegion);
        _session.SelectRegion(newRegion.Id);
        _session.NotifyRegionsChanged();
        MapView.Refresh();
        _playerWindow?.RefreshMap();
        UpdateStatus("Область с описанием добавлена.");
    }

    private void FinishTransientDrawing()
    {
        _isDrawingPath = false;
        _isDrawingRegion = false;
        _pathPointsImage.Clear();
        _regionPointsImage.Clear();
        _session.DraftPath = [];
        _session.DraftRegionOutline = [];
        ReleaseMapMouseCapture();
    }

    private void ClearPathDraft()
    {
        _pathPointsImage.Clear();
        _session.DraftPath = [];
    }

    private void ReleaseMapMouseCapture()
    {
        if (MapView.IsMouseCaptured)
            MapView.ReleaseMouseCapture();
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

        if (!_session.HasMap)
        {
            StatusText.Text = "Сначала загрузите карту — кнопка «Карта» вверху слева.";
            return;
        }

        if (ToggleQuestJournal.IsChecked == true && _currentTool == MasterTool.Navigate)
        {
            StatusText.Text = "Журнал квестов: выберите запись — связанные метки подсветятся. «Новый» — создать; «Активен», «К сдаче», «Выполнен» — сменить статус.";
            return;
        }

        StatusText.Text = _currentTool switch
        {
            MasterTool.Navigate => "Обзор: клик — выбрать; двойной клик — править; Delete — удалить. «Квесты» — журнал заданий. Колёсико — масштаб; WASD — сдвиг.",
            MasterTool.PartyMarker => "Клик по карте — поставить или перенести метку партии (синий щит).",
            MasterTool.TargetMarker => "Клик по карте — медальон цели и окно для подписи (например, «Логово врага»).",
            MasterTool.EncounterMarker => "Клик по карте — боевое столкновение: название и описание для свитка.",
            MasterTool.DrawPath => _session.HasSelectedTarget
                ? "Зажмите кнопку и проведите маршрут к выбранной цели. Отпустите — маршрут в очередь (кнопка «Маршруты»)."
                : "Сначала выберите цель в режиме «Обзор», затем снова нажмите «Путь».",
            MasterTool.DrawRegion => "Зажмите кнопку и обведите область. Отпустите: заголовок, текст свитка и «Показывать игрокам» при желании.",
            _ => string.Empty
        };
    }

    private void OnEncounterTriggered(EncounterPoint encounter)
    {
        _movement.StopRenderLoop();
        UpdateMoveButton();
        MapView.Refresh();
        UpdateStatus($"Столкновение «{encounter.Title}»: движение приостановлено.");

        EnsurePlayerWindow(activateIfOpen: true);
        var player = _playerWindow;
        if (player is null)
            return;

        var title = encounter.Title;
        var description = encounter.Description;
        player.Dispatcher.BeginInvoke(() =>
        {
            player.Activate();
            player.ShowEncounterPopup(title, description);
        }, DispatcherPriority.ApplicationIdle);
    }

    protected override void OnClosed(EventArgs e)
    {
        _movement.MovementStateChanged -= UpdateMoveButton;
        _session.EncounterTriggered -= OnEncounterTriggered;
        _movement.StopRenderLoop();
        base.OnClosed(e);
    }
}
