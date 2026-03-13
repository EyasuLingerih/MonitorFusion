using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using MonitorFusion.Core.Models;
using MonitorFusion.Core.Services;

namespace MonitorFusion.App.Views;

public partial class WallpaperSettingsView : UserControl
{
    private WallpaperProfile _currentProfile = new WallpaperProfile { Name = "Default" };
    private MonitorInfo? _selectedMonitor;
    private bool _isInitializing = true;

    public WallpaperSettingsView()
    {
        InitializeComponent();
        
        // Setup Sizing Combo
        SizingCombo.ItemsSource = Enum.GetValues(typeof(WallpaperSizing));
        
        LoadSettings();
    }

    private void LoadSettings()
    {
        _isInitializing = true;
        
        var settings = App.SettingsService.Load();
        _currentProfile = settings.WallpaperProfiles.FirstOrDefault(p => p.Name == settings.ActiveWallpaperProfile) 
                          ?? new WallpaperProfile { Name = "Default" };

        var monitors = App.MonitorService.GetAllMonitors();
        MonitorSelector.ItemsSource = monitors;
        
        if (monitors.Count > 0)
        {
            MonitorSelector.SelectedIndex = 0;
        }

        if (_currentProfile.Rotation == null)
            _currentProfile.Rotation = new WallpaperRotation();

        RotationEnabledCheck.IsChecked = _currentProfile.Rotation.Enabled;
        RotationIntervalBox.Text = _currentProfile.Rotation.IntervalMinutes.ToString();
        RotationFoldersList.ItemsSource = _currentProfile.Rotation.Folders;

        _isInitializing = false;
        UpdateUIFromConfig();
    }

    private void MonitorSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedMonitor = MonitorSelector.SelectedItem as MonitorInfo;
        UpdateUIFromConfig();
    }

    private void UpdateUIFromConfig()
    {
        if (_selectedMonitor == null || _isInitializing) return;
        _isInitializing = true;

        if (!_currentProfile.Monitors.TryGetValue(_selectedMonitor.DeviceId, out var config))
        {
            config = new WallpaperMonitorConfig();
            _currentProfile.Monitors[_selectedMonitor.DeviceId] = config;
        }

        SizingCombo.SelectedItem = config.Sizing;
        BrightnessSlider.Value = config.Adjustments.Brightness;
        ContrastSlider.Value = config.Adjustments.Contrast;
        BlurSlider.Value = config.Adjustments.Blur;
        GrayscaleCheck.IsChecked = config.Adjustments.Grayscale;
        SepiaCheck.IsChecked = config.Adjustments.Sepia;
        InvertCheck.IsChecked = config.Adjustments.Invert;

        SetPreviewImage(config.Source);

        _isInitializing = false;
    }

    private void Setting_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing || _selectedMonitor == null) return;

        if (!_currentProfile.Monitors.TryGetValue(_selectedMonitor.DeviceId, out var config)) return;

        config.Sizing = (WallpaperSizing)SizingCombo.SelectedItem;
        config.Adjustments.Brightness = (float)BrightnessSlider.Value;
        config.Adjustments.Contrast = (float)ContrastSlider.Value;
        config.Adjustments.Blur = (float)BlurSlider.Value;
        config.Adjustments.Grayscale = GrayscaleCheck.IsChecked == true;
        config.Adjustments.Sepia = SepiaCheck.IsChecked == true;
        config.Adjustments.Invert = InvertCheck.IsChecked == true;
    }

    private void ImagePreview_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_selectedMonitor == null) return;

        var dialog = new OpenFileDialog
        {
            Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.webp",
            Title = "Select Wallpaper"
        };

        if (dialog.ShowDialog() == true)
        {
            if (!_currentProfile.Monitors.TryGetValue(_selectedMonitor.DeviceId, out var config))
            {
                config = new WallpaperMonitorConfig();
                _currentProfile.Monitors[_selectedMonitor.DeviceId] = config;
            }

            config.Source = dialog.FileName;
            SetPreviewImage(config.Source);
        }
    }

    private void SetPreviewImage(string? path)
    {
        if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
        {
            ImagePreview.Source = null;
            ImagePreview.Visibility = Visibility.Collapsed;
            ImagePreviewPlaceholder.Text = "Click to select image";
            ImagePreviewPlaceholder.Visibility = Visibility.Visible;
            return;
        }

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(path);
            bitmap.DecodePixelWidth = 400;   // decode a thumbnail — don't load full res
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();

            ImagePreview.Source = bitmap;
            ImagePreview.Visibility = Visibility.Visible;
            ImagePreviewPlaceholder.Visibility = Visibility.Collapsed;
        }
        catch
        {
            ImagePreview.Source = null;
            ImagePreview.Visibility = Visibility.Collapsed;
            ImagePreviewPlaceholder.Text = "Cannot load image";
            ImagePreviewPlaceholder.Visibility = Visibility.Visible;
        }
    }

    private void ApplyToAll_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedMonitor == null || !_currentProfile.Monitors.TryGetValue(_selectedMonitor.DeviceId, out var sourceConfig))
            return;

        var allMonitors = App.MonitorService.GetAllMonitors();
        
        foreach (var monitor in allMonitors)
        {
            if (monitor.DeviceId == _selectedMonitor.DeviceId)
                continue;

            // Deep copy the config to other monitors
            _currentProfile.Monitors[monitor.DeviceId] = new WallpaperMonitorConfig
            {
                Source = sourceConfig.Source,
                Sizing = sourceConfig.Sizing,
                Adjustments = new ImageAdjustments
                {
                    Brightness = sourceConfig.Adjustments.Brightness,
                    Contrast = sourceConfig.Adjustments.Contrast,
                    Blur = sourceConfig.Adjustments.Blur,
                    Grayscale = sourceConfig.Adjustments.Grayscale,
                    Sepia = sourceConfig.Adjustments.Sepia,
                    Invert = sourceConfig.Adjustments.Invert
                }
            };
        }

        MessageBox.Show("Current wallpaper settings copied to all monitors!\nDon't forget to click Save Profile.", 
                        "Applied to All", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Preview_Click(object sender, RoutedEventArgs e)
    {
        App.WallpaperService.ApplyProfile(_currentProfile);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var settings = App.SettingsService.Load();
        
        var existingIndex = settings.WallpaperProfiles.FindIndex(p => p.Name == _currentProfile.Name);
        if (existingIndex >= 0)
        {
            settings.WallpaperProfiles[existingIndex] = _currentProfile;
        }
        else
        {
            settings.WallpaperProfiles.Add(_currentProfile);
        }

        settings.ActiveWallpaperProfile = _currentProfile.Name;
        App.SettingsService.Save(settings);

        // Apply immediately
        App.WallpaperService.ApplyProfile(_currentProfile);

        if (_currentProfile.Rotation != null && _currentProfile.Rotation.Enabled)
            App.WallpaperService.StartRotation(_currentProfile.Rotation);
        else
            App.WallpaperService.StopRotation();

        // Confirm to the user via tray notification
        if (Application.Current.MainWindow is MainWindow mainWindow)
        {
            mainWindow.TrayIcon.ShowBalloonTip(
                "Wallpaper Saved",
                $"Profile \"{_currentProfile.Name}\" saved and applied.",
                Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Info);
        }
    }

    private void RotationSetting_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing || _currentProfile.Rotation == null) return;
        _currentProfile.Rotation.Enabled = RotationEnabledCheck.IsChecked == true;
    }

    private void RotationInterval_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isInitializing || _currentProfile.Rotation == null) return;
        if (int.TryParse(RotationIntervalBox.Text, out int minutes) && minutes >= 1)
        {
            _currentProfile.Rotation.IntervalMinutes = minutes;
        }
    }

    private void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Folder for Wallpaper Rotation"
        };

        if (dialog.ShowDialog() == true)
        {
            if (_currentProfile.Rotation == null) _currentProfile.Rotation = new WallpaperRotation();
            
            if (_currentProfile.Rotation.Folders != null && !_currentProfile.Rotation.Folders.Contains(dialog.FolderName))
            {
                var newFolders = new List<string>(_currentProfile.Rotation.Folders);
                newFolders.Add(dialog.FolderName);
                _currentProfile.Rotation.Folders = newFolders;
                RotationFoldersList.ItemsSource = _currentProfile.Rotation.Folders;
            }
        }
    }

    private void RemoveFolder_Click(object sender, RoutedEventArgs e)
    {
        if (RotationFoldersList.SelectedItem is string folder && _currentProfile.Rotation?.Folders != null)
        {
            var newFolders = new List<string>(_currentProfile.Rotation.Folders);
            newFolders.Remove(folder);
            _currentProfile.Rotation.Folders = newFolders;
            RotationFoldersList.ItemsSource = _currentProfile.Rotation.Folders;
        }
    }
}
