namespace DnDMapHelper.Helpers;

/// <summary>Правила путешествия (D&amp;D-ориентир): скорость, еда и вода на человека.</summary>
public static class TravelRules
{
    public const double FoodPoundsPerPersonPerDay = 1.0;
    public const double PoundToKilogram = 0.45359237;
    public const double FoodKgPerPersonPerDay = FoodPoundsPerPersonPerDay * PoundToKilogram;

    public const double WaterGallonsPerPersonPerDay = 1.0;
    public const double GallonToLiter = 3.785411784;
    public const double WaterLitersPerPersonPerDay = WaterGallonsPerPersonPerDay * GallonToLiter;

    public const double RoadKmPerDay = 32;
    public const double CrossCountryKmPerDay = 24;
    public const double DifficultKmPerDay = 16;
}

public enum TravelTerrain
{
    Road,
    CrossCountry,
    Difficult
}

public sealed class TerrainTravelEstimate
{
    public required string Name { get; init; }
    public required TravelTerrain Terrain { get; init; }
    public required double SpeedKmPerDay { get; init; }
    public required double TotalDays { get; init; }
    public required int WholeDays { get; init; }
    public required int Hours { get; init; }
    public required double FoodRationsPerPerson { get; init; }
    public required double FoodPoundsPerPerson { get; init; }
    public required double FoodKgPerPerson { get; init; }
    public required double WaterGallonsPerPerson { get; init; }
    public required double WaterLitersPerPerson { get; init; }

    public string DurationText =>
        WholeDays == 0 && Hours == 0
            ? "меньше часа"
            : WholeDays > 0
                ? $"{WholeDays} дн. {Hours} ч."
                : $"{Hours} ч.";
}

public sealed class RouteTravelEstimate
{
    public required double DistancePixels { get; init; }
    public required double DistanceKm { get; init; }
    public required bool HasScale { get; init; }
    public required TerrainTravelEstimate Road { get; init; }
    public required TerrainTravelEstimate CrossCountry { get; init; }
    public required TerrainTravelEstimate Difficult { get; init; }
}

public static class RouteTravelHelper
{
    public static RouteTravelEstimate Estimate(
        IReadOnlyList<System.Windows.Point> routePoints,
        double gridCellSizePixels,
        double kilometersPerCell)
    {
        var distancePixels = PathGeometryHelper.GetSmoothPathLength(routePoints);
        var hasScale = gridCellSizePixels > 0 && kilometersPerCell > 0;
        var distanceKm = hasScale
            ? distancePixels / gridCellSizePixels * kilometersPerCell
            : 0;

        return new RouteTravelEstimate
        {
            DistancePixels = distancePixels,
            DistanceKm = distanceKm,
            HasScale = hasScale,
            Road = BuildTerrain("По дороге", TravelTerrain.Road, TravelRules.RoadKmPerDay, distanceKm, hasScale),
            CrossCountry = BuildTerrain("По пересечённой местности", TravelTerrain.CrossCountry, TravelRules.CrossCountryKmPerDay, distanceKm, hasScale),
            Difficult = BuildTerrain("По труднопроходимой местности", TravelTerrain.Difficult, TravelRules.DifficultKmPerDay, distanceKm, hasScale)
        };
    }

    private static TerrainTravelEstimate BuildTerrain(
        string name,
        TravelTerrain terrain,
        double speedKmPerDay,
        double distanceKm,
        bool hasScale)
    {
        var totalDays = hasScale && speedKmPerDay > 0
            ? distanceKm / speedKmPerDay
            : 0;

        var wholeDays = (int)Math.Floor(totalDays);
        var hours = (int)Math.Round((totalDays - wholeDays) * 24);
        if (hours == 24)
        {
            wholeDays++;
            hours = 0;
        }

        // 1 паёк ≈ суточный рацион еды на человека (1 фунт).
        var rations = totalDays;

        return new TerrainTravelEstimate
        {
            Name = name,
            Terrain = terrain,
            SpeedKmPerDay = speedKmPerDay,
            TotalDays = totalDays,
            WholeDays = wholeDays,
            Hours = hours,
            FoodRationsPerPerson = rations,
            FoodPoundsPerPerson = rations * TravelRules.FoodPoundsPerPersonPerDay,
            FoodKgPerPerson = rations * TravelRules.FoodKgPerPersonPerDay,
            WaterGallonsPerPerson = rations * TravelRules.WaterGallonsPerPersonPerDay,
            WaterLitersPerPerson = rations * TravelRules.WaterLitersPerPersonPerDay
        };
    }

    public static string FormatDistanceKm(double km) =>
        km < 10 ? $"{km:0.##} км" : $"{km:0.#} км";

    public static string FormatRations(double rations) =>
        rations < 10 ? $"{rations:0.##} пайк." : $"{rations:0.#} пайк.";
}
