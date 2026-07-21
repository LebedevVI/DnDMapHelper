using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using DnDMapHelper.Help;

namespace DnDMapHelper.Views;

public partial class AboutWindow : Window
{
    private static readonly FontFamily BodyFont = new("Georgia, Palatino Linotype");

    public AboutWindow()
    {
        InitializeComponent();
        Title = AboutContent.WindowTitle;
        AppNameText.Text = AboutContent.AppName;
        TaglineText.Text = AboutContent.Tagline;
        VersionText.Text = AboutContent.VersionLabel;
        SummaryText.Text = AboutContent.Summary;
        ContactLabelRun.Text = AboutContent.ContactLabel + " ";
        ContactDisplayRun.Text = AboutContent.ContactEmail;
        DedicationText.Text = AboutContent.Dedication;
        BuildHighlights();
    }

    private void BuildHighlights()
    {
        var ink = (Brush)FindResource("InkBrush");
        var leather = (Brush)FindResource("LeatherBrush");

        foreach (var line in AboutContent.Highlights)
        {
            var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            row.Children.Add(new TextBlock
            {
                Text = "◆",
                FontSize = 9,
                Foreground = leather,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 4, 8, 0)
            });

            var text = new TextBlock
            {
                Text = line,
                FontFamily = BodyFont,
                FontSize = 13,
                LineHeight = 20,
                TextWrapping = TextWrapping.Wrap,
                Foreground = ink
            };
            Grid.SetColumn(text, 1);
            row.Children.Add(text);
            HighlightsPanel.Children.Add(row);
        }
    }

    private void ContactLink_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo(AboutContent.ContactEmailUrl) { UseShellExecute = true });
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
