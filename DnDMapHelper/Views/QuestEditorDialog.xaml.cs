using System.Windows;
using System.Windows.Controls;
using DnDMapHelper.Models;
using DnDMapHelper.Services;

namespace DnDMapHelper.Views;

public partial class QuestEditorDialog : Window
{
    private readonly GameSession _session = GameSession.Current;
    private readonly Quest _quest;

    public QuestEditorDialog(Quest quest)
    {
        InitializeComponent();
        _quest = quest;

        TitleBox.Text = quest.Title;
        ConditionsBox.Text = quest.Conditions;
        DescriptionBox.Text = quest.Description;
        RewardBox.Text = quest.Reward;

        var targets = _session.Targets.ToList();
        var noneTarget = new TargetMarker { Label = "— не выбрано —" };

        TurnInCombo.ItemsSource = new[] { noneTarget }.Concat(targets).ToList();
        ObjectiveList.ItemsSource = targets;
        RegionList.ItemsSource = _session.Regions.ToList();

        SelectComboTarget(TurnInCombo, quest.TurnInTargetId);
        SelectListItems(ObjectiveList, quest.ObjectiveTargetIds);
        SelectListItems(RegionList, quest.RegionIds);

        Loaded += (_, _) => TitleBox.Focus();
    }

    public Quest Quest => _quest;

    private static void SelectComboTarget(ComboBox combo, Guid? targetId)
    {
        if (!targetId.HasValue)
        {
            combo.SelectedIndex = 0;
            return;
        }

        for (var i = 0; i < combo.Items.Count; i++)
        {
            if (combo.Items[i] is TargetMarker marker && marker.Id == targetId.Value)
            {
                combo.SelectedIndex = i;
                return;
            }
        }

        combo.SelectedIndex = 0;
    }

    private static void SelectListItems(ListBox list, IEnumerable<Guid> ids)
    {
        var idSet = ids.ToHashSet();
        list.SelectedItems.Clear();
        foreach (var item in list.Items)
        {
            var id = item switch
            {
                TargetMarker target => target.Id,
                MapRegion region => region.Id,
                _ => Guid.Empty
            };
            if (idSet.Contains(id))
                list.SelectedItems.Add(item);
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TitleBox.Text))
        {
            MessageBox.Show(this, "Введите название квеста.", "Квест",
                MessageBoxButton.OK, MessageBoxImage.Information);
            TitleBox.Focus();
            return;
        }

        _quest.Title = TitleBox.Text.Trim();
        _quest.Conditions = ConditionsBox.Text.Trim();
        _quest.Description = DescriptionBox.Text.Trim();
        _quest.Reward = RewardBox.Text.Trim();
        _quest.TurnInTargetId = GetSelectedTargetId(TurnInCombo);
        _quest.ObjectiveTargetIds = ObjectiveList.SelectedItems.Cast<TargetMarker>().Select(t => t.Id).ToList();
        _quest.RegionIds = RegionList.SelectedItems.Cast<MapRegion>().Select(r => r.Id).ToList();

        DialogResult = true;
        Close();
    }

    private static Guid? GetSelectedTargetId(ComboBox combo)
    {
        if (combo.SelectedItem is not TargetMarker marker || marker.Label == "— не выбрано —")
            return null;

        return marker.Id;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
