using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using MonitorFusion.Core.Services;

namespace MonitorFusion.App.Views;

/// <summary>
/// Main settings window. Minimizes to system tray, shows monitor info,
/// and provides access to all settings panels.
/// </summary>
public partial class MainWindow : Window
{
    private HotkeyService? _hotkeyService;

    public MainWindow()
    {
        InitializeComponent();
        RefreshWindowLayoutsMenu();
        StateChanged += OnStateChanged;
        Closing += OnClosing;
    }

    /// <summary>
    /// Re-registers all global hotkeys from current settings.
    /// Call this after the user changes hotkey bindings.
    /// </summary>
    public void ReloadHotkeys()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        _hotkeyService?.Dispose();
        _hotkeyService = null;
        InitializeHotkeys(hwnd);
    }

    public void InitializeBackgroundServices()
    {
        // Force hWnd creation so hotkeys can work without the window being visible
        var hwnd = new WindowInteropHelper(this).EnsureHandle();

        // Load and display monitors
        RefreshMonitorList();

        // Initialize hotkeys
        InitializeHotkeys(hwnd);
        
        // Start background hooks (Snapping, etc)
        var settings = App.SettingsService.Load();
        App.WindowService.StartBackgroundServices(settings.Snapping);
    }



    private void RefreshMonitorList()
    {
        var monitors = App.MonitorService.GetAllMonitors();
        System.Diagnostics.Debug.WriteLine($"Detected {monitors.Count} monitor(s).");
        MonitorList.ItemsSource = monitors;
    }

    private void InitializeHotkeys(IntPtr hwnd)
    {
        try
        {
            _hotkeyService = new HotkeyService(hwnd);

            // Hook into window messages to receive WM_HOTKEY
            var source = HwndSource.FromHwnd(hwnd);
            source?.AddHook(WndProc);

            // Register default hotkeys
            var settings = App.SettingsService.Load();
            foreach (var binding in settings.Hotkeys.Bindings.Where(b => b.Enabled))
            {
                try
                {
                    Action action = binding.Action switch
                    {
                        "MoveWindowNextMonitor" => App.WindowService.MoveActiveWindowToNextMonitor,
                        "MoveWindowPrevMonitor" => App.WindowService.MoveActiveWindowToPreviousMonitor,
                        "SpanWindow" => App.WindowService.SpanActiveWindowAcrossMonitors,
                        "CenterWindow" => App.WindowService.CenterActiveWindow,
                        "NextWallpaper" => () => NextWallpaper_Click(null!, null!),
                        "SaveWindowPositions" => () => SavePositions_Click(null!, null!),
                        "RestoreWindowPositions" => () => RestorePositions_Click(null!, null!),
                        "ToggleFocusMode" => () => ToggleFocusMode_Click(null!, null!),
                        _ => () => { } // Unknown action
                    };

                    _hotkeyService.Register(binding, action);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"Failed to register hotkey {binding.Action}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Hotkey init failed: {ex.Message}");
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == HotkeyService.WM_HOTKEY)
        {
            _hotkeyService?.ProcessHotkeyMessage(wParam.ToInt32());
            handled = true;
        }
        return IntPtr.Zero;
    }

    #region Window State Management

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void Maximize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized 
            ? WindowState.Normal 
            : WindowState.Maximized;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close(); // This will trigger OnClosing to minimize to tray
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        // Minimize to tray instead of taskbar
        if (WindowState == WindowState.Minimized)
        {
            Hide();
        }
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Minimize to tray instead of closing
        e.Cancel = true;
        Hide();
    }

    #endregion

    #region Tray Icon Events

    private void ShowSettings_Click(object sender, RoutedEventArgs e)
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void MonitorsMenu_Click(object sender, RoutedEventArgs e)
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        
        // Trigger the internal navigation logic
        NavMonitors_Click(this, new RoutedEventArgs());
    }

    private void ToggleFocusMode_Click(object sender, RoutedEventArgs e)
    {
        var settings = App.SettingsService.Load();
        settings.Fading.Enabled = !settings.Fading.Enabled;
        App.SettingsService.Save(settings);
        App.FadingService.ReloadSettings();
        
        TrayIcon.ShowBalloonTip(
            "Focus Mode",
            settings.Fading.Enabled ? "Focus Mode is now ON" : "Focus Mode is now OFF",
            Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Info);
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        _hotkeyService?.Dispose();
        TrayIcon.Dispose();
        Application.Current.Shutdown();
    }

    #endregion

    #region Navigation Events

    private void NavMonitors_Click(object sender, RoutedEventArgs e)
    {
        ContentPanel.Children.Clear();
        ContentPanel.Children.Add(new MonitorProfileSettingsView());
    }

    private void NavWallpaper_Click(object sender, RoutedEventArgs e)
    {
        ContentPanel.Children.Clear();
        ContentPanel.Children.Add(new WallpaperSettingsView());
    }

    private void NavSnapping_Click(object sender, RoutedEventArgs e)
    {
        ContentPanel.Children.Clear();
        ContentPanel.Children.Add(new SnappingSettingsView());
    }

    private void NavHotkeys_Click(object sender, RoutedEventArgs e)
    {
        ContentPanel.Children.Clear();
        ContentPanel.Children.Add(new HotkeySettingsView());
    }

    private void NavTaskbar_Click(object sender, RoutedEventArgs e)
    {
        ContentPanel.Children.Clear();
        ContentPanel.Children.Add(new TaskbarSettingsView());
    }

    private void NavWindowLayouts_Click(object sender, RoutedEventArgs e)
    {
        ContentPanel.Children.Clear();
        ContentPanel.Children.Add(new WindowLayoutsView());
    }

    private void NavFading_Click(object sender, RoutedEventArgs e)
    {
        ContentPanel.Children.Clear();
        ContentPanel.Children.Add(new FadingSettingsView());
    }

    private void NavSettings_Click(object sender, RoutedEventArgs e)
    {
        ContentPanel.Children.Clear();
        ContentPanel.Children.Add(new GeneralSettingsView());
    }

    #endregion

    #region Quick Actions

    private void IdentifyMonitors_Click(object sender, RoutedEventArgs e)
    {
        var monitors = App.MonitorService.GetAllMonitors();
        foreach (var monitor in monitors)
        {
            var label = new System.Windows.Controls.TextBlock
            {
                Text = (monitor.Index + 1).ToString(),
                FontSize = 96,
                FontWeight = System.Windows.FontWeights.Bold,
                Foreground = System.Windows.Media.Brushes.White,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };

            var subLabel = new System.Windows.Controls.TextBlock
            {
                Text = monitor.FriendlyName,
                FontSize = 18,
                Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromArgb(200, 200, 200, 200)),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                Margin = new Thickness(0, 8, 0, 0)
            };

            var stack = new System.Windows.Controls.StackPanel
            {
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };
            stack.Children.Add(label);
            stack.Children.Add(subLabel);

            var overlay = new Window
            {
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                Topmost = true,
                ShowInTaskbar = false,
                AllowsTransparency = true,
                Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromArgb(210, 15, 15, 30)),
                Left = monitor.Bounds.Left,
                Top = monitor.Bounds.Top,
                Width = monitor.Width,
                Height = monitor.Height,
                Content = stack
            };
            overlay.Show();

            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            timer.Tick += (s, _) => { overlay.Close(); timer.Stop(); };
            timer.Start();
        }
    }

    private void SavePositions_Click(object? sender, RoutedEventArgs e)
    {
        var profile = App.WindowService.SaveCurrentPositions("Quick Save");
        var settings = App.SettingsService.Load();

        // Replace existing quick save or add new one
        var existing = settings.WindowProfiles.FindIndex(p => p.Name == "Quick Save");
        if (existing >= 0)
            settings.WindowProfiles[existing] = profile;
        else
            settings.WindowProfiles.Add(profile);

        App.SettingsService.Save(settings);

        TrayIcon.ShowBalloonTip(
            "Positions Saved",
            $"Saved the geometry of {profile.Windows.Count} active windows.",
            Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Info);
    }

    private void SavePositionsAs_Click(object? sender, RoutedEventArgs e)
    {
        var settings = App.SettingsService.Load();
        int count = settings.WindowProfiles.Count(p => p.Name != "Quick Save");
        string defaultName = $"Profile {count + 1}";

        var dialog = new SaveLayoutDialog(defaultName);
        if (dialog.ShowDialog() == true)
        {
            string name = dialog.LayoutName;
            var profile = App.WindowService.SaveCurrentPositions(name);
            
            var existing = settings.WindowProfiles.FindIndex(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (existing >= 0)
                settings.WindowProfiles[existing] = profile;
            else
                settings.WindowProfiles.Add(profile);

            App.SettingsService.Save(settings);
            RefreshWindowLayoutsMenu();

            TrayIcon.ShowBalloonTip(
                "Layout Saved",
                $"Saved {profile.Windows.Count} windows to '{name}'.",
                Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Info);
        }
    }

    public void RefreshWindowLayoutsMenu()
    {
        // First 2 items are "Save Current Layout As..." and Separator. Remove everything after index 1.
        while (WindowLayoutsMenu.Items.Count > 2)
        {
            WindowLayoutsMenu.Items.RemoveAt(2);
        }

        var settings = App.SettingsService.Load();
        if (settings.WindowProfiles.Count == 0)
        {
            var emptyItem = new MenuItem { Header = "No saved layouts", IsEnabled = false };
            WindowLayoutsMenu.Items.Add(emptyItem);
            return;
        }

        foreach (var profile in settings.WindowProfiles.OrderBy(p => p.Name))
        {
            var item = new MenuItem { Header = profile.Name };
            item.Click += async (s, args) => 
            {
                TrayIcon.ShowBalloonTip(
                    "Restoring Layout...",
                    $"Applying '{profile.Name}'.",
                    Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Info);
                    
                await App.WindowService.RestorePositionsAsync(profile);
            };
            WindowLayoutsMenu.Items.Add(item);
        }
    }

    private async void RestorePositions_Click(object? sender, RoutedEventArgs e)
    {
        var settings = App.SettingsService.Load();
        var profile = settings.WindowProfiles.FirstOrDefault(p => p.Name == "Quick Save");

        if (profile != null)
        {
            TrayIcon.ShowBalloonTip(
                "Restoring Positions...",
                $"Moving apps and launching missing executables.",
                Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Info);

            await App.WindowService.RestorePositionsAsync(profile);
        }
    }

    private void NextWallpaper_Click(object? sender, RoutedEventArgs e)
    {
        var settings = App.SettingsService.Load();
        var activeProfile = settings.WallpaperProfiles
            .FirstOrDefault(p => p.Name == settings.ActiveWallpaperProfile);

        if (activeProfile?.Rotation != null)
        {
            App.WallpaperService.SetRandomWallpaper(
                activeProfile.Rotation.Folders,
                activeProfile.Rotation.IncludeSubfolders);
        }
    }

    private void ViewOnGitHub_Click(object sender, RoutedEventArgs e)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "https://github.com/EyasuLingerih/MonitorFusion",
            UseShellExecute = true
        });
    }

    #endregion
}
