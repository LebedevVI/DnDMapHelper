using DnDMapHelper.Models;
using DnDMapHelper.Services;

namespace DnDMapHelper.Helpers;

public static class QuestMapHelper
{
    public static IEnumerable<Quest> GetQuestsForTarget(GameSession session, Guid targetId) =>
        session.Quests.Where(q => q.ReferencesTarget(targetId));

    public static IEnumerable<Quest> GetQuestsForRegion(GameSession session, Guid regionId) =>
        session.Quests.Where(q => q.ReferencesRegion(regionId));

    public static bool IsTargetLinkedToQuests(GameSession session, Guid targetId) =>
        session.Quests.Any(q => q.ReferencesTarget(targetId));

    public static bool IsRegionLinkedToQuests(GameSession session, Guid regionId) =>
        session.Quests.Any(q => q.ReferencesRegion(regionId));

    public static bool IsTargetVisibleOnMap(GameSession session, Guid targetId, bool forPlayerDisplay = false)
    {
        var referenced = false;
        foreach (var quest in session.Quests)
        {
            if (!quest.ReferencesTarget(targetId))
                continue;

            referenced = true;
            if (IsTargetVisibleForQuest(quest, targetId, forPlayerDisplay))
                return true;
        }

        return !referenced;
    }

    public static bool IsRegionVisibleOnMap(GameSession session, Guid regionId, bool forPlayerDisplay = false)
    {
        var referenced = false;
        foreach (var quest in session.Quests)
        {
            if (!quest.ReferencesRegion(regionId))
                continue;

            referenced = true;
            if (IsRegionVisibleForQuest(quest, forPlayerDisplay))
                return true;
        }

        return !referenced;
    }

    public static bool IsTargetQuestHighlighted(GameSession session, Guid targetId, bool forPlayerDisplay = false)
    {
        var quest = session.SelectedQuest;
        if (quest is null || quest.Status == QuestStatus.Completed)
            return false;

        return quest.ReferencesTarget(targetId)
               && IsTargetVisibleForQuest(quest, targetId, forPlayerDisplay);
    }

    public static bool IsRegionQuestHighlighted(GameSession session, Guid regionId, bool forPlayerDisplay = false)
    {
        var quest = session.SelectedQuest;
        if (quest is null || quest.Status == QuestStatus.Completed)
            return false;

        return quest.ReferencesRegion(regionId) && IsRegionVisibleForQuest(quest, forPlayerDisplay);
    }

    public static bool IsTargetVisibleForQuest(Quest quest, Guid targetId, bool forPlayerDisplay)
    {
        if (quest.Status == QuestStatus.Completed)
            return false;

        if (quest.Status == QuestStatus.ReadyToTurnIn)
            return quest.TurnInTargetId == targetId;

        if (quest.TurnInTargetId == targetId)
            return !forPlayerDisplay;

        if (!quest.ObjectiveTargetIds.Contains(targetId))
            return false;

        if (quest.VisitedObjectiveTargetIds.Contains(targetId))
            return !forPlayerDisplay;

        return true;
    }

    public static bool IsRegionVisibleForQuest(Quest quest, bool forPlayerDisplay = false)
    {
        if (quest.Status == QuestStatus.Completed)
            return false;

        if (quest.Status == QuestStatus.ReadyToTurnIn)
            return false;

        return quest.Status == QuestStatus.Active;
    }

    public static void TryPromoteQuestToReadyToTurnIn(Quest quest)
    {
        if (quest.Status != QuestStatus.Active)
            return;

        if (quest.HasRemainingObjectives())
            return;

        if (quest.TurnInTargetId is null)
            return;

        quest.Status = QuestStatus.ReadyToTurnIn;
    }
}
