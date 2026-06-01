using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DnDMapHelper.Help;

namespace DnDMapHelper.Views;

public partial class HelpWindow : Window
{
    private static readonly FontFamily TitleFont = new("Palatino Linotype, Georgia");
    private static readonly FontFamily BodyFont = new("Georgia, Palatino Linotype");

    public HelpWindow()
    {
        InitializeComponent();
        TitleText.Text = HelpContent.Title;
        BuildSections();
    }

    private void BuildSections()
    {
        var ink = (Brush)FindResource("InkBrush");
        var leather = (Brush)FindResource("LeatherBrush");
        var isFirst = true;

        foreach (var section in HelpContent.Sections)
        {
            if (!isFirst)
                SectionsPanel.Children.Add(new Border
                {
                    Height = 1,
                    Background = leather,
                    Opacity = 0.35,
                    Margin = new Thickness(0, 20, 0, 4)
                });
            isFirst = false;

            SectionsPanel.Children.Add(new TextBlock
            {
                Text = section.Heading,
                FontFamily = TitleFont,
                FontSize = 17,
                FontWeight = FontWeights.Bold,
                Foreground = ink,
                Margin = new Thickness(0, 8, 0, 10)
            });

            RenderBody(section.Body.Trim(), ink, leather);
        }
    }

    private void RenderBody(string body, Brush ink, Brush leather)
    {
        foreach (var line in body.Split('\n'))
        {
            var text = line.TrimEnd();
            if (string.IsNullOrWhiteSpace(text))
            {
                SectionsPanel.Children.Add(new Border { Height = 8 });
                continue;
            }

            if (text.StartsWith("▸ "))
            {
                SectionsPanel.Children.Add(new TextBlock
                {
                    Text = text[2..],
                    FontFamily = TitleFont,
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = ink,
                    Margin = new Thickness(0, 10, 0, 4)
                });
                continue;
            }

            if (text.StartsWith("• "))
            {
                var row = new Grid { Margin = new Thickness(8, 2, 0, 2) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                row.Children.Add(new TextBlock
                {
                    Text = "◆",
                    FontSize = 9,
                    Foreground = leather,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0, 5, 8, 0)
                });

                var content = new TextBlock
                {
                    Text = text[2..],
                    FontFamily = BodyFont,
                    FontSize = 14,
                    LineHeight = 22,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = ink
                };
                Grid.SetColumn(content, 1);
                row.Children.Add(content);
                SectionsPanel.Children.Add(row);
                continue;
            }

            if (char.IsDigit(text[0]) && text.Contains(". "))
            {
                var dot = text.IndexOf(". ", StringComparison.Ordinal);
                var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                row.Children.Add(new TextBlock
                {
                    Text = text[..(dot + 1)],
                    FontFamily = TitleFont,
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    Foreground = leather,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0, 0, 8, 0),
                    MinWidth = 22
                });

                var content = new TextBlock
                {
                    Text = text[(dot + 2)..],
                    FontFamily = BodyFont,
                    FontSize = 14,
                    LineHeight = 22,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = ink
                };
                Grid.SetColumn(content, 1);
                row.Children.Add(content);
                SectionsPanel.Children.Add(row);
                continue;
            }

            SectionsPanel.Children.Add(new TextBlock
            {
                Text = text,
                FontFamily = BodyFont,
                FontSize = 14,
                LineHeight = 22,
                TextWrapping = TextWrapping.Wrap,
                Foreground = ink,
                Margin = new Thickness(0, 0, 0, 2)
            });
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
