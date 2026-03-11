using System.Windows;
using System.Windows.Controls;
using MonitorFusion.App.Services;
using MonitorFusion.Core.Models;
using MonitorFusion.Core.Services;

namespace MonitorFusion.App.Views;

public partial class TaskbarSettingsView : UserControl
{
    private TaskbarSettings _settings = new();
    private bool _isInitializing = true;

    public TaskbarSettingsView()
    {
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        _isInitializing = true;
        
        var appSettings = App.SettingsService.Load();
        _settings = appSettings.Taskbar ?? new TaskbarSettings();

        EnabledCheck.IsChecked = _settings.Enabled;
        ShowAllMonitorsCheck.IsChecked = _settings.ShowOnAllMonitors;
        ShowClockCheck.IsChecked = _settings.ShowClock;
        HeightSlider.Value = _settings.Height;

        foreach (ComboBoxItem item in PositionCombo.Items)
        {
            if (item.Content?.ToString() == _settings.Position)
            {
                PositionCombo.SelectedItem = item;
                break;
            }
        }

        if (PositionCombo.SelectedItem == null && PositionCombo.Items.Count > 0)
        {
            PositionCombo.SelectedIndex = 0;
        }

        _isInitializing = false;
    }

    private void Setting_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;

        UpdateSettingsFromUI();
    }

    private void PositionCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;
        UpdateSettingsFromUI();
    }

    private void UpdateSettingsFromUI()
    {
        _settings.Enabled = EnabledCheck.IsChecked == true;
        _settings.ShowOnAllMonitors = ShowAllMonitorsCheck.IsChecked == true;
        _settings.ShowClock = ShowClockCheck.IsChecked == true;
        _settings.Height = (int)HeightSlider.Value;

        if (PositionCombo.SelectedItem is ComboBoxItem item)
        {
            _settings.Position = item.Content?.ToString() ?? "Bottom";
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        UpdateSettingsFromUI();

        var appSettings = App.SettingsService.Load();
        appSettings.Taskbar = _settings;
        App.SettingsService.Save(appSettings);
        
        // Notify TaskbarService to reload settings and instantly spawn/destroy taskbars
        App.TaskbarService.ReloadSettings();
        
        MessageBox.Show("Taskbar settings saved and applied!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
