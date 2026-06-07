using System.Windows;

namespace DnDMapHelper.Models;

public sealed class MapRegion
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public List<Point> Outline { get; set; } = [];
    public string Description { get; set; } = string.Empty;
    public string Title { get; set; } = "Описание земель";
    public bool VisibleToPlayers { get; set; }

    public MapRegion() { }

    public MapRegion(Guid id, List<Point> outline, string title, string description, bool visibleToPlayers)
    {
        Id = id;
        Outline = outline;
        Title = title;
        Description = description;
        VisibleToPlayers = visibleToPlayers;
    }

    public Rect Bounds => Helpers.RegionGeometryHelper.GetBounds(Outline);
}
