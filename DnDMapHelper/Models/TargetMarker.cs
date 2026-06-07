using System.Windows;

namespace DnDMapHelper.Models;

public sealed class TargetMarker
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Point Position { get; set; }
    public string Label { get; set; } = string.Empty;

    public TargetMarker() { }

    public TargetMarker(Guid id, Point position, string label)
    {
        Id = id;
        Position = position;
        Label = label;
    }
}
