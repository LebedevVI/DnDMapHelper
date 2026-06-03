using System.Windows;

namespace DnDMapHelper.Views;

public partial class RegionTextDialog : Window
{
    public RegionTextDialog(
        string title,
        string description,
        bool visibleToPlayers = false,
        bool showPlayerVisibilityOption = false)
    {
        InitializeComponent();
        TitleBox.Text = title;
        DescriptionBox.Text = description;

        if (showPlayerVisibilityOption)
        {
            ShowToPlayersCheckBox.Visibility = Visibility.Visible;
            ShowToPlayersCheckBox.IsChecked = visibleToPlayers;
        }

        Loaded += OnLoaded;
    }

    public string RegionTitle => TitleBox.Text.Trim();
    public string RegionDescription => DescriptionBox.Text.Trim();
    public bool VisibleToPlayers => ShowToPlayersCheckBox.IsChecked == true;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        TitleBox.Focus();
        TitleBox.CaretIndex = TitleBox.Text.Length;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TitleBox.Text))
        {
            MessageBox.Show(this, "Введите заголовок области.", "Описание области",
                MessageBoxButton.OK, MessageBoxImage.Information);
            TitleBox.Focus();
            return;
        }

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
