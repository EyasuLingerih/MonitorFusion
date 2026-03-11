using System.Windows;
using System.Windows.Controls;
using System.Linq;

namespace MonitorFusion.App.Views;

public partial class WindowLayoutsView : UserControl
{
    public WindowLayoutsView()
    {
        InitializeComponent();
        LoadLayouts();
    }

    private void LoadLayouts()
    {
        var settings = App.SettingsService.Load();
        var viewModels = settings.WindowProfiles.Select(p => new LayoutViewModel
        {
            Name = p.Name,
            WindowCount = $"{p.Windows.Count} apps active"
        }).OrderBy(x => x.Name).ToList();

        LayoutsList.ItemsSource = viewModels;
    }

    private async void Restore_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string name)
        {
            var settings = App.SettingsService.Load();
            var profile = settings.WindowProfiles.FirstOrDefault(p => p.Name == name);
            if (profile != null)
            {
                if (Window.GetWindow(this) is MainWindow mainWindow)
                {
                    mainWindow.TrayIcon.ShowBalloonTip(
                        "Restoring Layout...",
                        $"Applying '{profile.Name}'.",
                        Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Info);
                }

                await App.WindowService.RestorePositionsAsync(profile);
            }
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string name)
        {
            var result = MessageBox.Show($"Are you sure you want to delete the layout '{name}'?", 
                                         "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                var settings = App.SettingsService.Load();
                settings.WindowProfiles.RemoveAll(p => p.Name == name);
                App.SettingsService.Save(settings);
                LoadLayouts(); // Refresh UI
                
                if (Window.GetWindow(this) is MainWindow mainWindow)
                {
                    mainWindow.RefreshWindowLayoutsMenu();
                }
            }
        }
    }
}

public class LayoutViewModel
{
    public string Name { get; set; } = string.Empty;
    public string WindowCount { get; set; } = string.Empty;
}
