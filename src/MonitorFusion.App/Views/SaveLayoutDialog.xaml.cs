using System.Windows;

namespace MonitorFusion.App.Views;

public partial class SaveLayoutDialog : Window
{
    public string LayoutName { get; private set; } = string.Empty;

    public SaveLayoutDialog(string defaultName = "")
    {
        InitializeComponent();
        NameTextBox.Text = defaultName;
        NameTextBox.SelectAll();
        NameTextBox.Focus();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameTextBox.Text))
        {
            MessageBox.Show("Please enter a name for the layout.", "Name Required", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        LayoutName = NameTextBox.Text.Trim();
        DialogResult = true;
        Close();
    }
}
