namespace DnDMapHelper.Models.Persistence;

using DnDMapHelper.Models;

public sealed class SessionSaveData
{
    public int FormatVersion { get; set; } = SessionFileFormat.CurrentVersion;
    public PointDto? PartyPosition { get; set; }
    public Guid? SelectedTargetId { get; set; }
    public Guid? SelectedRegionId { get; set; }
    public Guid? SelectedEncounterId { get; set; }
    public Guid? SelectedQuestId { get; set; }
    public int SelectedRouteIndex { get; set; } = -1;

    public List<TargetMarkerDto> Targets { get; set; } = [];
    public List<MapRegionDto> Regions { get; set; } = [];
    public List<EncounterPointDto> Encounters { get; set; } = [];
    public List<MovementRouteDto> Routes { get; set; } = [];
    public List<QuestDto> Quests { get; set; } = [];
}

public sealed class PointDto
{
    public double X { get; set; }
    public double Y { get; set; }

    public static PointDto FromPoint(System.Windows.Point point) => new() { X = point.X, Y = point.Y };

    public System.Windows.Point ToPoint() => new(X, Y);
}

public sealed class TargetMarkerDto
{
    public Guid Id { get; set; }
    public PointDto Position { get; set; } = new();
    public string Label { get; set; } = string.Empty;
}

public sealed class MapRegionDto
{
    public Guid Id { get; set; }
    public List<PointDto> Outline { get; set; } = [];
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool VisibleToPlayers { get; set; }
}

public sealed class EncounterPointDto
{
    public Guid Id { get; set; }
    public PointDto Position { get; set; } = new();
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public sealed class MovementRouteDto
{
    public Guid Id { get; set; }
    public int Order { get; set; }
    public Guid TargetId { get; set; }
    public string TargetLabel { get; set; } = string.Empty;
    public List<PointDto> Points { get; set; } = [];
}

public sealed class QuestDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Conditions { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Reward { get; set; } = string.Empty;
    public string Status { get; set; } = nameof(QuestStatus.Active);
    public Guid? TurnInTargetId { get; set; }
    public List<Guid> ObjectiveTargetIds { get; set; } = [];
    public List<Guid> RegionIds { get; set; } = [];
    public List<Guid> VisitedObjectiveTargetIds { get; set; } = [];
}

public static class SessionFileFormat
{
    public const string Extension = ".dndmap";
    public const int CurrentVersion = 1;
    public const string SessionEntryName = "session.json";
    public const string MapEntryName = "map.png";
    public const string FileDialogFilter =
        "Карта приключений (*.dndmap)|*.dndmap|Все файлы|*.*";
}
