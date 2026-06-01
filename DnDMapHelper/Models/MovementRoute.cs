using System.Windows;

namespace DnDMapHelper.Models;

public sealed class MovementRoute
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public int Order { get; set; }
    public Guid TargetId { get; set; }
    public string TargetLabel { get; set; } = string.Empty;
    public List<Point> Points { get; set; } = [];

    public Point StartPoint => Points.Count > 0 ? Points[0] : default;
    public Point EndPoint => Points.Count > 0 ? Points[^1] : default;

    public string DisplayName => string.IsNullOrWhiteSpace(TargetLabel)
        ? $"Маршрут {Order}"
        : $"→ {TargetLabel}";
}
