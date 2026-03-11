using System.Windows;
using System.Windows.Controls;
using MonitorFusion.Core.Models;
using MonitorFusion.Core.Services;

namespace MonitorFusion.App.Views;

public partial class SnappingSettingsView : UserControl
{
    private SnappingSettings _settings = new();
    private bool _isInitializing = true;

    public SnappingSettingsView()
    {
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        _isInitializing = true;
        
        var appSettings = App.SettingsService.Load();
        _settings = appSettings.Snapping ?? new SnappingSettings();

        EnabledCheck.IsChecked = _settings.Enabled;
        SnapToEdgesCheck.IsChecked = _settings.SnapToMonitorEdges;
        SnapToWindowsCheck.IsChecked = _settings.SnapToOtherWindows;
        StickySnappingCheck.IsChecked = _settings.StickySnapping;
        BypassShiftCheck.IsChecked = _settings.BypassWithShift;
        DistanceSlider.Value = _settings.SnapDistance;
        
        if (_settings.IgnoredProcesses == null)
            _settings.IgnoredProcesses = new List<string>();
            
        IgnoredList.ItemsSource = _settings.IgnoredProcesses;

        _isInitializing = false;
    }

    private void Setting_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;

        _settings.Enabled = EnabledCheck.IsChecked == true;
        _settings.SnapToMonitorEdges = SnapToEdgesCheck.IsChecked == true;
        _settings.SnapToOtherWindows = SnapToWindowsCheck.IsChecked == true;
        _settings.StickySnapping = StickySnappingCheck.IsChecked == true;
        _settings.BypassWithShift = BypassShiftCheck.IsChecked == true;
        _settings.SnapDistance = (int)DistanceSlider.Value;

        Save_Click(null, null);
    }

    private void AddProcess_Click(object sender, RoutedEventArgs e)
    {
        string process = ProcessInput.Text.Trim();
        if (!string.IsNullOrEmpty(process) && !_settings.IgnoredProcesses.Contains(process, StringComparer.OrdinalIgnoreCase))
        {
            var newList = new List<string>(_settings.IgnoredProcesses);
            newList.Add(process);
            _settings.IgnoredProcesses = newList;
            IgnoredList.ItemsSource = _settings.IgnoredProcesses;
            ProcessInput.Clear();
            
            // Auto save
            Save_Click(null, null);
        }
    }

    private void RemoveProcess_Click(object sender, RoutedEventArgs e)
    {
        if (IgnoredList.SelectedItem is string process)
        {
            var newList = new List<string>(_settings.IgnoredProcesses);
            newList.Remove(process);
            _settings.IgnoredProcesses = newList;
            IgnoredList.ItemsSource = _settings.IgnoredProcesses;
            
            // Auto save
            Save_Click(null, null);
        }
    }

    private void Save_Click(object? sender, RoutedEventArgs? e)
    {
        // Explicitly update settings from UI just to be safe
        _settings.Enabled = EnabledCheck.IsChecked == true;
        _settings.SnapToMonitorEdges = SnapToEdgesCheck.IsChecked == true;
        _settings.SnapToOtherWindows = SnapToWindowsCheck.IsChecked == true;
        _settings.StickySnapping = StickySnappingCheck.IsChecked == true;
        _settings.BypassWithShift = BypassShiftCheck.IsChecked == true;
        _settings.SnapDistance = (int)DistanceSlider.Value;

        var appSettings = App.SettingsService.Load();
        appSettings.Snapping = _settings;
        App.SettingsService.Save(appSettings);
        
        // Notify WindowService to reload settings if active
        App.WindowService.ReloadSettings(_settings);

        if (sender != null)
        {
            MessageBox.Show("Snapping settings saved successfully!", "Settings Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
