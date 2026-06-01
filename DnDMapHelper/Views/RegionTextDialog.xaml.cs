using System.Windows;

namespace DnDMapHelper.Views;

public partial class RegionTextDialog : Window
{
    public RegionTextDialog(string title, string description)
    {
        InitializeComponent();
        TitleBox.Text = title;
        DescriptionBox.Text = description;
    }

    public string RegionTitle => TitleBox.Text.Trim();
    public string RegionDescription => DescriptionBox.Text.Trim();

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
