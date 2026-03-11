using System.Threading;
using System.Windows;
using MonitorFusion.App.Services;
using MonitorFusion.Core.Services;

namespace MonitorFusion.App;

/// <summary>
/// Application entry point. Ensures single instance, initializes services,
/// and manages the system tray icon.
/// </summary>
public partial class App : Application
{
    private Mutex? _singleInstanceMutex;

    // Core services (will be accessed by ViewModels)
    public static MonitorDetectionService MonitorService { get; private set; } = null!;
    public static WallpaperService WallpaperService { get; private set; } = null!;
    public static WindowManagementService WindowService { get; private set; } = null!;
    public static SettingsService SettingsService { get; private set; } = null!;
    public static MonitorProfileService MonitorProfileService { get; private set; } = null!;
    public static TaskbarService TaskbarService { get; private set; } = null!;
    public static FadingService FadingService { get; private set; } = null!;

    // Shared log directory used by crash handlers (created on first use)
    private static string LogDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "MonitorFusion", "logs");

    private static void WriteCrashLog(string fileName, string content)
    {
        try
        {
            Directory.CreateDirectory(LogDir);
            File.WriteAllText(Path.Combine(LogDir, fileName), content);
        }
        catch { /* best effort — never crash inside the crash handler */ }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        this.DispatcherUnhandledException += (s, args) =>
        {
            var logPath = Path.Combine(LogDir, "crash.log");
            WriteCrashLog("crash.log", args.Exception.ToString());
            MessageBox.Show(
                $"MonitorFusion encountered an unexpected error and needs to close.\n\n" +
                $"{args.Exception.Message}\n\n" +
                $"A crash log has been saved to:\n{logPath}",
                "MonitorFusion — Unexpected Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
            Shutdown();
        };
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            WriteCrashLog("crash_fatal.log", args.ExceptionObject.ToString());
        };

        // Ensure only one instance runs
        _singleInstanceMutex = new Mutex(true, "MonitorFusion_SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show("MonitorFusion is already running!", "MonitorFusion",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);

        // Initialize services
        SettingsService = new SettingsService();
        MonitorService = new MonitorDetectionService();
        WallpaperService = new WallpaperService(MonitorService);
        WindowService = new WindowManagementService(MonitorService);
        MonitorProfileService = new MonitorProfileService(MonitorService, SettingsService);
        TaskbarService = new TaskbarService(MonitorService, SettingsService);
        FadingService = new FadingService(MonitorService, SettingsService);

        // Load settings
        var settings = SettingsService.Load();

        if (settings.WallpaperProfiles.Count > 0)
        {
            var profile = settings.WallpaperProfiles.Find(p => p.Name == settings.ActiveWallpaperProfile);
            if (profile != null)
            {
                WallpaperService.ApplyProfile(profile);
                if (profile.Rotation != null && profile.Rotation.Enabled)
                {
                    WallpaperService.StartRotation(profile.Rotation);
                }
            }
        }
        
        if (settings.Taskbar.Enabled)
        {
            TaskbarService.Start();
        }
        
        if (settings.Fading.Enabled)
        {
            FadingService.Start();
        }

        // Show splash screen
        var splash = new Views.SplashWindow();
        splash.Show();

        // Apply dark theme globally (basic approach)
        Current.Resources["TextBrush"] = new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFFFFF"));
        Current.Resources["TextSecondaryBrush"] = new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#A0A0B0"));
        Current.Resources["AccentBrush"] = new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#00A2ED"));
        Current.Resources["SurfaceBrush"] = new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#252538"));
    }

    protected override void OnExit(ExitEventArgs e)
    {
        WindowService?.StopBackgroundServices();
        WallpaperService?.StopRotation();
        TaskbarService?.Stop();
        FadingService?.Stop();
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
