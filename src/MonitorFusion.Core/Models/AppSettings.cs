namespace MonitorFusion.Core.Models;

/// <summary>
/// Root settings object, serialized to JSON.
/// Stored in %APPDATA%/MonitorFusion/settings.json
/// </summary>
public class AppSettings
{
    public GeneralSettings General { get; set; } = new();
    public SnappingSettings Snapping { get; set; } = new();
    public HotkeySettings Hotkeys { get; set; } = new();
    public TaskbarSettings Taskbar { get; set; } = new();
    public FadingSettings Fading { get; set; } = new();

    /// <summary>All saved wallpaper profiles</summary>
    public List<WallpaperProfile> WallpaperProfiles { get; set; } = new();

    /// <summary>All saved monitor profiles</summary>
    public List<MonitorProfile> MonitorProfiles { get; set; } = new();

    /// <summary>All saved window position profiles</summary>
    public List<WindowPositionProfile> WindowProfiles { get; set; } = new();

    /// <summary>Active wallpaper profile name</summary>
    public string ActiveWallpaperProfile { get; set; } = "Default";

    /// <summary>Active monitor profile name</summary>
    public string ActiveMonitorProfile { get; set; } = "Default";

    /// <summary>User-defined monitor labels keyed by DeviceId (e.g. \\.\DISPLAY1 → "Gaming Monitor")</summary>
    public Dictionary<string, string> MonitorNicknames { get; set; } = new();

    /// <summary>Zone layout settings — virtual monitor splitting</summary>
    public ZoneSettings Zones { get; set; } = new();
}

public class GeneralSettings
{
    public bool StartWithWindows { get; set; } = true;
    public bool StartMinimized { get; set; } = true;
    public bool CheckForUpdates { get; set; } = true;
    public string Language { get; set; } = "en";  // Support "am" for Amharic!
    public bool ShowTrayIcon { get; set; } = true;
    /// <summary>Add "Open MonitorFusion" to the desktop right-click context menu.</summary>
    public bool DesktopContextMenu { get; set; } = false;
}

public class SnappingSettings
{
    public bool Enabled { get; set; } = true;
    public bool SnapToMonitorEdges { get; set; } = true;
    public bool SnapToOtherWindows { get; set; } = true;  // Pro feature
    public int SnapDistance { get; set; } = 20;  // Pixels
    public bool StickySnapping { get; set; } = false;  // Snap immediately
    public bool BypassWithShift { get; set; } = true;
    public List<string> IgnoredProcesses { get; set; } = new();
}

public class HotkeySettings
{
    public List<HotkeyBinding> Bindings { get; set; } = new()
    {
        // Default hotkeys
        new() { Action = "MoveWindowNextMonitor", Key = "Right", Modifiers = "Ctrl+Win" },
        new() { Action = "MoveWindowPrevMonitor", Key = "Left", Modifiers = "Ctrl+Win" },
        new() { Action = "MaximizeWindow", Key = "Up", Modifiers = "Ctrl+Win" },
        new() { Action = "MinimizeWindow", Key = "Down", Modifiers = "Ctrl+Win" },
        new() { Action = "SpanWindow", Key = "S", Modifiers = "Ctrl+Win" },
        new() { Action = "CenterWindow", Key = "C", Modifiers = "Ctrl+Win" },
        new() { Action = "SaveWindowPositions", Key = "F5", Modifiers = "Ctrl+Win" },
        new() { Action = "RestoreWindowPositions", Key = "F6", Modifiers = "Ctrl+Win" },
        new() { Action = "NextWallpaper", Key = "W", Modifiers = "Ctrl+Win" },
        new() { Action = "ToggleFocusMode", Key = "Z", Modifiers = "Ctrl+Win" },
    };
}

public class HotkeyBinding
{
    public string Action { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Modifiers { get; set; } = string.Empty;  // "Ctrl+Alt+Shift+Win"
    public bool Enabled { get; set; } = true;
}

public class TaskbarSettings
{
    public bool Enabled { get; set; } = false;  // Off by default (Pro feature)
    public bool ShowOnAllMonitors { get; set; } = true;
    public bool ShowClock { get; set; } = true;
    public bool ShowTrayIcons { get; set; } = false;
    public bool AutoHide { get; set; } = false;
    public string Position { get; set; } = "Bottom";  // Top, Bottom, Left, Right
    public int Height { get; set; } = 48;
    public bool ShowOnlyCurrentMonitorWindows { get; set; } = true;
    public bool GroupButtons { get; set; } = true;
}

public class FadingSettings
{
    public bool Enabled { get; set; } = false;
    public double Opacity { get; set; } = 0.7; // 0.0 (clear) to 1.0 (black)
    public string Mode { get; set; } = "InactiveMonitors"; // "InactiveMonitors", "AllExceptActiveWindow"
}
