using System.Windows;

namespace DnDMapHelper.Views;

public partial class ScrollPopupWindow : Window
{
    public ScrollPopupWindow(string title, string body, Window? owner = null)
    {
        InitializeComponent();
        if (owner is not null)
            Owner = owner;

        TitleText.Text = string.IsNullOrWhiteSpace(title) ? "Свиток земель" : title;
        BodyText.Text = string.IsNullOrWhiteSpace(body) ? "Здесь пока нет описания." : body;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
