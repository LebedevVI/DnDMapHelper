using System.Windows;
using DnDMapHelper.Helpers;

namespace DnDMapHelper.Views;

public partial class ScrollPopupWindow : Window
{
    public ScrollPopupWindow(string title, string body, Window? owner = null, bool playOpenSound = true)
    {
        InitializeComponent();
        if (owner is not null)
        {
            Owner = owner;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }

        TitleText.Text = string.IsNullOrWhiteSpace(title) ? "Свиток земель" : title;
        BodyText.Text = string.IsNullOrWhiteSpace(body) ? "Здесь пока нет описания." : body;

        if (playOpenSound)
            Loaded += (_, _) => ScrollSoundHelper.PlayOpen();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
