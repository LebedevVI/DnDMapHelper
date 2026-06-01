using System.Windows;

namespace DnDMapHelper.Models;

public sealed class MapRegion
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Rect Bounds { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Title { get; set; } = "Описание земель";
}
