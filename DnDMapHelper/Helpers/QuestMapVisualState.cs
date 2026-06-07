using DnDMapHelper.Models;
using DnDMapHelper.Services;

namespace DnDMapHelper.Helpers;

/// <summary>Снимок видимости квестовых объектов — один проход на перерисовку статического слоя.</summary>
public sealed class QuestMapVisualState
{
    private readonly Dictionary<Guid, bool> _targetVisible = [];
    private readonly Dictionary<Guid, bool> _regionVisible = [];
    private readonly HashSet<Guid> _highlightedTargets = [];
    private readonly HashSet<Guid> _highlightedRegions = [];

    public static QuestMapVisualState Build(GameSession session, bool forPlayerDisplay)
    {
        var state = new QuestMapVisualState();

        foreach (var target in session.Targets)
            state._targetVisible[target.Id] = QuestMapHelper.IsTargetVisibleOnMap(session, target.Id, forPlayerDisplay);

        foreach (var region in session.Regions)
            state._regionVisible[region.Id] = QuestMapHelper.IsRegionVisibleOnMap(session, region.Id, forPlayerDisplay);

        var selected = session.SelectedQuest;
        if (selected is not null && selected.Status != QuestStatus.Completed)
        {
            if (selected.TurnInTargetId is { } turnInId &&
                QuestMapHelper.IsTargetVisibleForQuest(selected, turnInId, forPlayerDisplay))
                state._highlightedTargets.Add(turnInId);

            foreach (var objectiveId in selected.ObjectiveTargetIds)
            {
                if (QuestMapHelper.IsTargetVisibleForQuest(selected, objectiveId, forPlayerDisplay))
                    state._highlightedTargets.Add(objectiveId);
            }

            if (QuestMapHelper.IsRegionVisibleForQuest(selected, forPlayerDisplay))
            {
                foreach (var regionId in selected.RegionIds)
                    state._highlightedRegions.Add(regionId);
            }
        }

        return state;
    }

    public bool IsTargetVisible(Guid targetId) =>
        !_targetVisible.TryGetValue(targetId, out var visible) || visible;

    public bool IsRegionVisible(Guid regionId) =>
        !_regionVisible.TryGetValue(regionId, out var visible) || visible;

    public bool IsTargetHighlighted(Guid targetId) => _highlightedTargets.Contains(targetId);

    public bool IsRegionHighlighted(Guid regionId) => _highlightedRegions.Contains(regionId);
}
