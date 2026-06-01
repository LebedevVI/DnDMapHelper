using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DnDMapHelper.Help;

namespace DnDMapHelper.Views;

public partial class HelpWindow : Window
{
    public HelpWindow()
    {
        InitializeComponent();
        TitleText.Text = HelpContent.Title;
        BuildSections();
    }

    private void BuildSections()
    {
        foreach (var section in HelpContent.Sections)
        {
            SectionsPanel.Children.Add(new TextBlock
            {
                Text = section.Heading,
                FontFamily = new FontFamily("Palatino Linotype, Georgia"),
                FontSize = 17,
                FontWeight = FontWeights.Bold,
                Foreground = (Brush)FindResource("InkBrush"),
                Margin = new Thickness(0, 16, 0, 8)
            });

            SectionsPanel.Children.Add(new TextBlock
            {
                Text = section.Body.Trim(),
                FontFamily = new FontFamily("Georgia, Palatino Linotype"),
                FontSize = 14,
                LineHeight = 22,
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)FindResource("InkBrush"),
                Margin = new Thickness(0, 0, 0, 4)
            });
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
