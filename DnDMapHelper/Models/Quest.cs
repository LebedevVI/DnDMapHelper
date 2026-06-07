namespace DnDMapHelper.Models;

public sealed class Quest
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Title { get; set; } = "Новый квест";
    public string Conditions { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Reward { get; set; } = string.Empty;
    public QuestStatus Status { get; set; } = QuestStatus.Active;

    public Guid? TurnInTargetId { get; set; }
    public List<Guid> ObjectiveTargetIds { get; set; } = [];
    public List<Guid> RegionIds { get; set; } = [];
    public List<Guid> VisitedObjectiveTargetIds { get; set; } = [];

    public Quest() { }

    public Quest(
        Guid id,
        string title,
        string conditions,
        string description,
        string reward,
        QuestStatus status,
        Guid? turnInTargetId,
        List<Guid> objectiveTargetIds,
        List<Guid> regionIds,
        List<Guid> visitedObjectiveTargetIds)
    {
        Id = id;
        Title = title;
        Conditions = conditions;
        Description = description;
        Reward = reward;
        Status = status;
        TurnInTargetId = turnInTargetId;
        ObjectiveTargetIds = objectiveTargetIds;
        RegionIds = regionIds;
        VisitedObjectiveTargetIds = visitedObjectiveTargetIds;
    }

    public string StatusLabel => Status switch
    {
        QuestStatus.ReadyToTurnIn => "Готов к сдаче",
        QuestStatus.Completed => "Выполнен",
        _ => "Активен"
    };

    public string JournalDisplayName => $"{Title} — {StatusLabel}";

    public bool ReferencesTarget(Guid targetId) =>
        TurnInTargetId == targetId || ObjectiveTargetIds.Contains(targetId);

    public bool ReferencesRegion(Guid regionId) => RegionIds.Contains(regionId);

    public bool HasRemainingObjectives() =>
        ObjectiveTargetIds.Any(id => !VisitedObjectiveTargetIds.Contains(id));

    public void ResetProgress()
    {
        VisitedObjectiveTargetIds.Clear();
        Status = QuestStatus.Active;
    }
}
