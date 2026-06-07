using System.Windows;

namespace DnDMapHelper.Models;

public sealed class EncounterPoint
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Point Position { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public EncounterPoint() { }

    public EncounterPoint(Guid id, Point position, string title, string description)
    {
        Id = id;
        Position = position;
        Title = title;
        Description = description;
    }
}
