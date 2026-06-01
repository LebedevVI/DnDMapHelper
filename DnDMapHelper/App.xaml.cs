using System.Windows;
using DnDMapHelper.Views;

namespace DnDMapHelper;

public partial class App : Application
{
    private void OnStartup(object sender, StartupEventArgs e)
    {
        var master = new MasterWindow();
        master.Show();
    }
}
