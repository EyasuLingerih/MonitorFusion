using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace MonitorFusion.App.Views;

public partial class GeneralSettingsView : UserControl
{
    private bool _isInitializing = true;

    public GeneralSettingsView()
    {
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        _isInitializing = true;
        var settings = App.SettingsService.Load();

        StartWithWindowsCheck.IsChecked = settings.General.StartWithWindows;
        StartMinimizedCheck.IsChecked   = settings.General.StartMinimized;
        ShowTrayIconCheck.IsChecked     = settings.General.ShowTrayIcon;
        CheckForUpdatesCheck.IsChecked  = settings.General.CheckForUpdates;

        _isInitializing = false;
    }

    private void Setting_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;

        var settings = App.SettingsService.Load();
        settings.General.StartWithWindows = StartWithWindowsCheck.IsChecked == true;
        settings.General.StartMinimized   = StartMinimizedCheck.IsChecked   == true;
        settings.General.ShowTrayIcon     = ShowTrayIconCheck.IsChecked     == true;
        settings.General.CheckForUpdates  = CheckForUpdatesCheck.IsChecked  == true;
        App.SettingsService.Save(settings);

        // Apply startup registry entry immediately
        ApplyStartWithWindows(settings.General.StartWithWindows);
    }

    private static void ApplyStartWithWindows(bool enable)
    {
        const string keyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        const string valueName = "MonitorFusion";

        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(keyPath, writable: true);
            if (key == null) return;

            if (enable)
            {
                string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";
                if (!string.IsNullOrEmpty(exePath))
                    key.SetValue(valueName, $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue(valueName, throwOnMissingValue: false);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to update startup registry: {ex.Message}");
        }
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export MonitorFusion Settings",
            Filter = "JSON Settings|*.json",
            FileName = "monitorfusion-settings.json"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                App.SettingsService.Export(dialog.FileName);
                ShowStatus($"Settings exported to {System.IO.Path.GetFileName(dialog.FileName)}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "Export Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import MonitorFusion Settings",
            Filter = "JSON Settings|*.json"
        };

        if (dialog.ShowDialog() == true)
        {
            var result = MessageBox.Show(
                "Importing settings will replace all your current settings.\n\nAre you sure?",
                "Import Settings",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                App.SettingsService.Import(dialog.FileName);
                ShowStatus("Settings imported successfully. Some changes take effect on next start.");
                LoadSettings(); // Refresh UI
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Import failed: {ex.Message}", "Import Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "This will reset ALL settings to their defaults.\n\n" +
            "Your wallpaper profiles, hotkeys, and all configuration will be lost.\n\n" +
            "Are you sure?",
            "Reset Settings",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        App.SettingsService.Reset();
        ShowStatus("All settings have been reset to defaults.");
        LoadSettings();
    }

    private void ShowStatus(string message)
    {
        BackupStatusText.Text = message;
        BackupStatusText.Visibility = Visibility.Visible;

        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(4)
        };
        timer.Tick += (s, _) => { BackupStatusText.Visibility = Visibility.Collapsed; timer.Stop(); };
        timer.Start();
    }
}
