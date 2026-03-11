using System.Text.Json.Serialization;

namespace MonitorFusion.Core.Models;

/// <summary>
/// Defines how wallpaper should be displayed on each monitor.
/// </summary>
public class WallpaperProfile
{
    public string Name { get; set; } = "Default";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Per-monitor wallpaper settings, keyed by DeviceId</summary>
    public Dictionary<string, WallpaperMonitorConfig> Monitors { get; set; } = new();

    /// <summary>Rotation settings for automatic wallpaper changes</summary>
    public WallpaperRotation? Rotation { get; set; }
}

public class WallpaperMonitorConfig
{
    /// <summary>Path to image file, URL, or special source identifier</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>How the image is sized to fit the monitor</summary>
    public WallpaperSizing Sizing { get; set; } = WallpaperSizing.Fill;

    /// <summary>Image adjustments</summary>
    public ImageAdjustments Adjustments { get; set; } = new();

    /// <summary>For "span" mode: which monitors to span across</summary>
    public List<string>? SpanMonitors { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WallpaperSizing
{
    Fill,       // Scale to fill, crop excess
    Fit,        // Scale to fit, letterbox
    Stretch,    // Stretch to exact size (distorts)
    Tile,       // Repeat at original size
    Center,     // Original size, centered
    Span,       // Span across multiple monitors
    CropToFill  // Smart crop focusing on center
}

public class ImageAdjustments
{
    public float Brightness { get; set; } = 0f;   // -1.0 to 1.0
    public float Contrast { get; set; } = 0f;     // -1.0 to 1.0
    public bool Grayscale { get; set; } = false;
    public bool Sepia { get; set; } = false;
    public bool Invert { get; set; } = false;
    public float Blur { get; set; } = 0f;         // 0 = no blur
    public int Rotation { get; set; } = 0;        // 0, 90, 180, 270
    public bool FlipHorizontal { get; set; } = false;
    public bool FlipVertical { get; set; } = false;
}

public class WallpaperRotation
{
    public bool Enabled { get; set; } = false;
    public int IntervalMinutes { get; set; } = 30;
    public List<string> Folders { get; set; } = new();
    public bool IncludeSubfolders { get; set; } = true;

    /// <summary>Which source to pull random images from</summary>
    public WallpaperSource Source { get; set; } = WallpaperSource.LocalFolder;
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WallpaperSource
{
    LocalFolder,
    Unsplash,
    BingDaily,
    Reddit,
    Url
}
