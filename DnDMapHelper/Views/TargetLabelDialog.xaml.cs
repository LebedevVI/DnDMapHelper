using System.Windows;

namespace DnDMapHelper.Views;

public partial class TargetLabelDialog : Window
{
    public TargetLabelDialog(string currentLabel)
    {
        InitializeComponent();
        LabelBox.Text = currentLabel;
        LabelBox.SelectAll();
        LabelBox.Focus();
    }

    public string Label => LabelBox.Text.Trim();

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(LabelBox.Text))
        {
            MessageBox.Show(this, "Введите название цели.", "Подпись цели",
                MessageBoxButton.OK, MessageBoxImage.Information);
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
