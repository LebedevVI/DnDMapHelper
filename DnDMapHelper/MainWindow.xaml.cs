using System.Windows;
using DnDMapHelper.Views;

namespace DnDMapHelper;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OpenMaster_Click(object sender, RoutedEventArgs e)
    {
        var master = new MasterWindow { Owner = this };
        master.Show();
    }

    private void OpenPlayer_Click(object sender, RoutedEventArgs e)
    {
        var player = new PlayerWindow { Owner = this };
        player.Show();
    }
}
