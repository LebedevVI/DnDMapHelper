using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DnDMapHelper.Helpers;
using DnDMapHelper.Models;

namespace DnDMapHelper.Views;

public partial class RouteParametersDialog : Window
{
    public RouteParametersDialog(MovementRoute route, RouteTravelEstimate estimate)
    {
        InitializeComponent();
        TitleText.Text = string.IsNullOrWhiteSpace(route.TargetLabel)
            ? $"Маршрут #{route.Order}"
            : $"Маршрут → {route.TargetLabel}";

        if (estimate.HasScale)
            DistanceText.Text = $"Протяжённость: {RouteTravelHelper.FormatDistanceKm(estimate.DistanceKm)}";
        else
            DistanceText.Text = "Протяжённость: задайте сетку (размер клетки и км), чтобы посчитать километры.";

        FillTerrainBlock(RoadPanel, estimate.Road, estimate.HasScale);
        FillTerrainBlock(CrossCountryPanel, estimate.CrossCountry, estimate.HasScale);
        FillTerrainBlock(DifficultPanel, estimate.Difficult, estimate.HasScale);

        RulesText.Text =
            $"Скорость: дорога {TravelRules.RoadKmPerDay:0} км/день, пересечённая {TravelRules.CrossCountryKmPerDay:0} км/день, " +
            $"труднопроходимая {TravelRules.DifficultKmPerDay:0} км/день.\n" +
            $"Еда: {TravelRules.FoodPoundsPerPersonPerDay:0} фунт/чел./день " +
            $"({TravelRules.FoodKgPerPersonPerDay:0.###} кг) = 1 паёк.\n" +
            $"Вода: {TravelRules.WaterGallonsPerPersonPerDay:0} галлон/чел./день " +
            $"({TravelRules.WaterLitersPerPersonPerDay:0.##} л).";
    }

    private static void FillTerrainBlock(Panel panel, TerrainTravelEstimate terrain, bool hasScale)
    {
        panel.Children.Clear();
        panel.Children.Add(MakeHeading(terrain.Name));

        if (!hasScale)
        {
            panel.Children.Add(MakeLine("Нет данных — настройте сетку карты."));
            return;
        }

        panel.Children.Add(MakeLine($"Срок: {terrain.DurationText} ({terrain.SpeedKmPerDay:0} км/день)"));
        panel.Children.Add(MakeLine(
            $"Еда на 1 чел.: {RouteTravelHelper.FormatRations(terrain.FoodRationsPerPerson)} " +
            $"({FormatNumber(terrain.FoodPoundsPerPerson)} фунт / {FormatNumber(terrain.FoodKgPerPerson)} кг)"));
        panel.Children.Add(MakeLine(
            $"Вода на 1 чел.: {FormatNumber(terrain.WaterGallonsPerPerson)} гал. / {FormatNumber(terrain.WaterLitersPerPerson)} л"));
    }

    private static TextBlock MakeHeading(string text) => new()
    {
        Text = text,
        FontWeight = FontWeights.SemiBold,
        FontSize = 13,
        Margin = new Thickness(0, 0, 0, 4),
        Foreground = (Brush)Application.Current.FindResource("InkBrush")
    };

    private static TextBlock MakeLine(string text) => new()
    {
        Text = text,
        FontSize = 12,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 0, 0, 2),
        Foreground = (Brush)Application.Current.FindResource("InkBrush")
    };

    private static string FormatNumber(double value) =>
        value < 10
            ? value.ToString("0.##", CultureInfo.CurrentCulture)
            : value.ToString("0.#", CultureInfo.CurrentCulture);

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
