namespace MonitorFusion.Core.Models;

/// <summary>
/// Saves a complete monitor configuration (resolutions, positions, refresh rates)
/// that can be restored later.
/// </summary>
public class MonitorProfile
{
    public string Name { get; set; } = "Default";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Per-monitor display settings</summary>
    public List<MonitorDisplayConfig> Displays { get; set; } = new();

    /// <summary>Optional: linked wallpaper profile to auto-apply</summary>
    public string? LinkedWallpaperProfile { get; set; }

    /// <summary>Optional: linked window position profile</summary>
    public string? LinkedWindowProfile { get; set; }
}

public class MonitorDisplayConfig
{
    public string DeviceId { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int RefreshRate { get; set; }
    public int BitsPerPixel { get; set; }
    public int Orientation { get; set; }  // 0, 90, 180, 270
    public int PositionX { get; set; }
    public int PositionY { get; set; }
}

/// <summary>
/// Saves all window positions for later restoration.
/// </summary>
public class WindowPositionProfile
{
    public string Name { get; set; } = "Default";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<SavedWindowPosition> Windows { get; set; } = new();
}

public class SavedWindowPosition
{
    /// <summary>Process name (e.g., "chrome", "code")</summary>
    public string ProcessName { get; set; } = string.Empty;

    /// <summary>Window title (for matching when multiple instances exist)</summary>
    public string WindowTitle { get; set; } = string.Empty;

    /// <summary>Window class name for more precise matching</summary>
    public string ClassName { get; set; } = string.Empty;

    /// <summary>Full path to the executable, allowing cold relaunch</summary>
    public string ExecutablePath { get; set; } = string.Empty;

    public int Left { get; set; }
    public int Top { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }

    /// <summary>Window state: Normal, Minimized, Maximized</summary>
    public int ShowState { get; set; }

    /// <summary>Which monitor the window was on</summary>
    public string MonitorDeviceId { get; set; } = string.Empty;
}
