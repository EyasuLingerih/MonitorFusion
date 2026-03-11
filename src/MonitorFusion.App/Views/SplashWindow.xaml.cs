using System;
using System.Windows;
using System.Windows.Threading;

namespace MonitorFusion.App.Views;

public partial class SplashWindow : Window
{
    private readonly DispatcherTimer _timer;

    public SplashWindow()
    {
        InitializeComponent();

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2.0)
        };
        _timer.Tick += Timer_Tick;
        Loaded += SplashWindow_Loaded;
    }

    private void SplashWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _timer.Start();
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        _timer.Stop();
        
        // Launch MainWindow
        var mainWindow = new MainWindow();
        
        // Setup background hooks and tray hotkeys immediately
        mainWindow.InitializeBackgroundServices();
        
        // Use AppSettings to determine if it should start minimized to tray
        var settings = App.SettingsService.Load();
        if (!settings.General.StartMinimized)
        {
            mainWindow.Show();
        }

        // Close splash screen
        this.Close();
    }
}
