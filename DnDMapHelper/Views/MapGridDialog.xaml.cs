using System.Globalization;
using System.Windows;
using DnDMapHelper.Services;

namespace DnDMapHelper.Views;

public partial class MapGridDialog : Window
{
    private readonly GameSession _session = GameSession.Current;

    public MapGridDialog()
    {
        InitializeComponent();
        ShowGridCheckBox.IsChecked = _session.ShowMapGrid;
        CellSizeBox.Text = _session.GridCellSizePixels.ToString("0.##", CultureInfo.CurrentCulture);
        KilometersBox.Text = _session.KilometersPerCell.ToString("0.####", CultureInfo.CurrentCulture);
        Loaded += (_, _) => CellSizeBox.Focus();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!TryParsePositive(CellSizeBox.Text, out var cellSize) || cellSize < 4)
        {
            MessageBox.Show(this,
                "Размер клетки в пикселях должен быть числом не меньше 4.",
                "Сетка карты",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            CellSizeBox.Focus();
            return;
        }

        if (!TryParsePositive(KilometersBox.Text, out var kmPerCell))
        {
            MessageBox.Show(this,
                "Укажите, сколько километров соответствует одной клетке (положительное число).",
                "Сетка карты",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            KilometersBox.Focus();
            return;
        }

        _session.ShowMapGrid = ShowGridCheckBox.IsChecked == true;
        _session.GridCellSizePixels = cellSize;
        _session.KilometersPerCell = kmPerCell;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private static bool TryParsePositive(string text, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var normalized = text.Trim().Replace(',', '.');
        if (!double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value) &&
            !double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.CurrentCulture, out value))
            return false;

        return value > 0 && !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
