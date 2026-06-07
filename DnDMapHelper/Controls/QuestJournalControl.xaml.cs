using System.Windows;
using System.Windows.Controls;
using DnDMapHelper.Models;
using DnDMapHelper.Services;
using DnDMapHelper.Views;

namespace DnDMapHelper.Controls;

public partial class QuestJournalControl : UserControl
{
    public static readonly DependencyProperty IsMasterModeProperty =
        DependencyProperty.Register(nameof(IsMasterMode), typeof(bool), typeof(QuestJournalControl),
            new PropertyMetadata(true, OnIsMasterModeChanged));

    private readonly GameSession _session = GameSession.Current;
    private bool _isSyncingQuestList;

    public QuestJournalControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        _session.PropertyChanged += OnSessionChanged;
    }

    public bool IsMasterMode
    {
        get => (bool)GetValue(IsMasterModeProperty);
        set => SetValue(IsMasterModeProperty, value);
    }

    public event Action<string>? StatusMessageRequested;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        QuestList.ItemsSource = _session.Quests;
        ApplyModeVisibility();
        SyncQuestList();
    }

    private static void OnIsMasterModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is QuestJournalControl control)
            control.ApplyModeVisibility();
    }

    private void ApplyModeVisibility()
    {
        var isMaster = IsMasterMode;
        MasterEditPanel.Visibility = isMaster ? Visibility.Visible : Visibility.Collapsed;
        MasterStatusPanel.Visibility = isMaster ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnSessionChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(GameSession.Quests))
            Dispatcher.BeginInvoke(SyncQuestList);
    }

    private void QuestList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSyncingQuestList)
            return;

        if (QuestList.SelectedItem is Quest quest)
        {
            if (_session.SelectedQuestId != quest.Id)
                _session.SelectQuest(quest.Id);
            UpdateQuestDetails(quest);
        }
        else
        {
            if (_session.SelectedQuestId.HasValue)
                _session.SelectQuest(null);
            UpdateQuestDetails(null);
        }
    }

    private void UpdateQuestDetails(Quest? quest)
    {
        if (quest is null)
        {
            QuestDetailsPlaceholder.Visibility = Visibility.Visible;
            QuestConditionsText.Visibility = Visibility.Collapsed;
            QuestDescriptionText.Visibility = Visibility.Collapsed;
            QuestRewardText.Visibility = Visibility.Collapsed;
            return;
        }

        QuestDetailsPlaceholder.Visibility = Visibility.Collapsed;

        SetDetailBlock(QuestConditionsText, "Условия", quest.Conditions);
        SetDetailBlock(QuestDescriptionText, "Задача", quest.Description);
        SetDetailBlock(QuestRewardText, "Награда", quest.Reward);
        QuestDetailsPanel.Opacity = quest.Status == QuestStatus.Completed ? 0.55 : 1;
    }

    private static void SetDetailBlock(TextBlock block, string label, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            block.Visibility = Visibility.Collapsed;
            return;
        }

        block.Text = $"{label}: {value}";
        block.Visibility = Visibility.Visible;
    }

    public void SyncQuestList()
    {
        if (_isSyncingQuestList)
            return;

        _isSyncingQuestList = true;
        try
        {
            if (_session.Quests.Count == 0)
            {
                QuestList.SelectedIndex = -1;
                UpdateQuestDetails(null);
                return;
            }

            if (_session.SelectedQuestId is { } selectedId)
            {
                for (var i = 0; i < _session.Quests.Count; i++)
                {
                    if (_session.Quests[i].Id != selectedId)
                        continue;

                    if (QuestList.SelectedIndex != i)
                        QuestList.SelectedIndex = i;
                    else
                        UpdateQuestDetails(_session.Quests[i]);

                    return;
                }
            }

            if (QuestList.SelectedIndex < 0 || QuestList.SelectedIndex >= _session.Quests.Count)
                QuestList.SelectedIndex = 0;
        }
        finally
        {
            _isSyncingQuestList = false;
        }
    }

    private void RefreshQuestListDisplay() => QuestList.Items.Refresh();

    private Window? GetOwnerWindow() => Window.GetWindow(this);

    private void NotifyMapChanged()
    {
        _session.NotifyQuestsChanged();
    }

    private void NewQuest_Click(object sender, RoutedEventArgs e)
    {
        var quest = new Quest();
        var dialog = new QuestEditorDialog(quest) { Owner = GetOwnerWindow() };
        if (dialog.ShowDialog() != true)
            return;

        _session.Quests.Add(quest);
        _session.SelectQuest(quest.Id);
        SyncQuestList();
        NotifyMapChanged();
        StatusMessageRequested?.Invoke($"Квест «{quest.Title}» добавлен.");
    }

    private void EditQuest_Click(object sender, RoutedEventArgs e)
    {
        if (QuestList.SelectedItem is not Quest quest)
        {
            StatusMessageRequested?.Invoke("Выберите квест в журнале.");
            return;
        }

        var dialog = new QuestEditorDialog(quest) { Owner = GetOwnerWindow() };
        if (dialog.ShowDialog() != true)
            return;

        NotifyMapChanged();
        RefreshQuestListDisplay();
        UpdateQuestDetails(quest);
        StatusMessageRequested?.Invoke($"Квест «{quest.Title}» обновлён.");
    }

    private void DeleteQuest_Click(object sender, RoutedEventArgs e)
    {
        if (QuestList.SelectedItem is not Quest quest)
            return;

        var owner = GetOwnerWindow();
        if (MessageBox.Show(owner, $"Удалить квест «{quest.Title}»?", "Журнал квестов",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        _session.Quests.Remove(quest);
        if (_session.SelectedQuestId == quest.Id)
            _session.SelectQuest(null);

        NotifyMapChanged();
        StatusMessageRequested?.Invoke($"Квест «{quest.Title}» удалён.");
    }

    private void MarkQuestActive_Click(object sender, RoutedEventArgs e)
    {
        if (QuestList.SelectedItem is not Quest quest)
        {
            StatusMessageRequested?.Invoke("Выберите квест в журнале.");
            return;
        }

        quest.ResetProgress();
        NotifyMapChanged();
        RefreshQuestListDisplay();
        UpdateQuestDetails(quest);
        StatusMessageRequested?.Invoke($"Квест «{quest.Title}» снова активен — все цели на карте.");
    }

    private void MarkQuestReady_Click(object sender, RoutedEventArgs e)
    {
        if (QuestList.SelectedItem is not Quest quest)
        {
            StatusMessageRequested?.Invoke("Выберите квест в журнале.");
            return;
        }

        if (quest.TurnInTargetId is null)
        {
            MessageBox.Show(GetOwnerWindow(), "Укажите метку сдачи квеста в редакторе.", "Журнал квестов",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        quest.Status = QuestStatus.ReadyToTurnIn;
        NotifyMapChanged();
        RefreshQuestListDisplay();
        UpdateQuestDetails(quest);
        StatusMessageRequested?.Invoke($"Квест «{quest.Title}» готов к сдаче.");
    }

    private void MarkQuestComplete_Click(object sender, RoutedEventArgs e)
    {
        if (QuestList.SelectedItem is not Quest quest)
        {
            StatusMessageRequested?.Invoke("Выберите квест в журнале.");
            return;
        }

        quest.Status = QuestStatus.Completed;
        NotifyMapChanged();
        RefreshQuestListDisplay();
        UpdateQuestDetails(quest);
        StatusMessageRequested?.Invoke($"Квест «{quest.Title}» выполнен.");
    }
}
